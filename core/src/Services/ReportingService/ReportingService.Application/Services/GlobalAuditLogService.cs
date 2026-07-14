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

    private readonly IIamServiceClient _iamClient;
    private readonly IGradingServiceClient _gradingClient;
    private readonly ISubmissionServiceClient _submissionClient;

    public GlobalAuditLogService(
        IIamServiceClient iamClient, IGradingServiceClient gradingClient, ISubmissionServiceClient submissionClient)
    {
        _iamClient = iamClient;
        _gradingClient = gradingClient;
        _submissionClient = submissionClient;
    }

    public async Task<GlobalAuditLogResultDto> GetAuditLogsAsync(
        Guid? userId, string? entityType, string? action, int page, int pageSize, CancellationToken ct = default)
    {
        var fetchCount = Math.Min(page * pageSize, MaxFetchPerSource);

        var iamTask = _iamClient.GetAuditLogsAsync(userId, entityType, action, 1, fetchCount, ct);
        var gradingTask = _gradingClient.GetAuditLogsAsync(userId, entityType, action, 1, fetchCount, ct);
        var submissionTask = _submissionClient.GetAuditLogsAsync(userId, entityType, action, 1, fetchCount, ct);
        await Task.WhenAll(iamTask, gradingTask, submissionTask);

        var unavailable = new List<string>();
        var entries = new List<AuditLogEntryDto>();
        var totalCount = 0;

        var iamResult = iamTask.Result;
        if (iamResult is null)
        {
            unavailable.Add("IamService");
        }
        else
        {
            totalCount += iamResult.TotalCount;
            entries.AddRange(iamResult.Items.Select(l => new AuditLogEntryDto(
                "Iam", l.UserId, l.Action, l.EntityType, l.EntityId.ToString(),
                FormatDetails(l.OldValue, l.NewValue, reason: null), l.CreatedAt)));
        }

        var gradingResult = gradingTask.Result;
        if (gradingResult is null)
        {
            unavailable.Add("GradingService");
        }
        else
        {
            totalCount += gradingResult.TotalCount;
            entries.AddRange(gradingResult.Items.Select(l => new AuditLogEntryDto(
                "Grading", l.UserId, l.Action, l.EntityType, l.EntityId?.ToString(),
                FormatDetails(l.OldValue, l.NewValue, l.Reason), l.CreatedAt)));
        }

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
