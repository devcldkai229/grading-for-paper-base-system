using ReportingService.Application.DTOs;
using ReportingService.Application.Interfaces;

namespace ReportingService.Application.Services;

public class GlobalAuditLogService : IGlobalAuditLogService
{
    // Merging independently-paginated sources requires fetching enough of each source's top
    // entries (already sorted newest-first) before the combined sort. Capped so a very deep page
    // request can't fan out an unbounded per-source fetch; deep pagination past this cap may miss
    // entries from a source with a long tail, though TotalCount always stays accurate.
    private const int MaxFetchPerSource = 500;

    private readonly IAuditRecordReadRepository _auditRepository;
    private readonly ISubmissionServiceClient _submissionClient;

    public GlobalAuditLogService(
        IAuditRecordReadRepository auditRepository, ISubmissionServiceClient submissionClient)
    {
        _auditRepository = auditRepository;
        _submissionClient = submissionClient;
    }

    public async Task<GlobalAuditLogResultDto> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default)
    {
        var fetchCount = Math.Min(page * pageSize, MaxFetchPerSource);

        // Grading + Iam audit entries now come from the local audit_record projection. Submission's audit
        // log stays REST (its store is Mongo without a safe outbox), so it can still degrade gracefully.
        var localTask = _auditRepository.QueryAsync(userId, entityType, action, fetchCount, ct);
        var submissionTask = _submissionClient.GetAuditLogsAsync(userId, entityType, action, 1, fetchCount, ct);
        await Task.WhenAll(localTask, submissionTask);

        var unavailable = new List<string>();
        var entries = new List<AuditLogEntryDto>();
        var totalCount = 0;

        var (localItems, localTotal) = localTask.Result;
        totalCount += localTotal;
        entries.AddRange(localItems.Select(a => new AuditLogEntryDto(
            ToSourceLabel(a.SourceService), a.UserId, a.Action, a.EntityType ?? string.Empty,
            a.EntityId?.ToString(), FormatDetails(a.OldValue, a.NewValue, a.Reason), a.OccurredAt)));

        var submissionResult = submissionTask.Result;
        if (submissionResult is null)
        {
            unavailable.Add("SubmissionService");
        }
        else
        {
            totalCount += submissionResult.TotalCount;
            entries.AddRange(submissionResult.Items.Select(l => new AuditLogEntryDto(
                "Submission", l.PerformedBy, l.Action, l.EntityType, l.EntityId.ToString(),
                l.Details, l.PerformedAt)));
        }

        var pageItems = entries
            .OrderByDescending(e => e.PerformedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new GlobalAuditLogResultDto(pageItems, page, pageSize, totalCount, unavailable);
    }

    private static string ToSourceLabel(string sourceService) => sourceService switch
    {
        "grading" => "Grading",
        "iam" => "Iam",
        _ => string.IsNullOrEmpty(sourceService)
            ? "Unknown"
            : char.ToUpperInvariant(sourceService[0]) + sourceService[1..]
    };

    private static string? FormatDetails(string? oldValue, string? newValue, string? reason)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(oldValue) || !string.IsNullOrWhiteSpace(newValue))
        {
            parts.Add($"{oldValue ?? "—"} → {newValue ?? "—"}");
        }
        if (!string.IsNullOrWhiteSpace(reason))
        {
            parts.Add($"Reason: {reason}");
        }
        return parts.Count > 0 ? string.Join(" | ", parts) : null;
    }
}
