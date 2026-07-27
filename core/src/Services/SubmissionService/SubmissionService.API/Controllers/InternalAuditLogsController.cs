using Microsoft.AspNetCore.Mvc;
using SubmissionService.Application.DTOs;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.API.Controllers;

/// <summary>
/// Internal (service-to-service) endpoint for ReportingService's global audit log viewer.
/// Protected by InternalApiKeyMiddleware (path prefix /api/internal) — no JWT/[Authorize] here.
/// </summary>
[Route("api/internal/audit-logs")]
[ApiController]
public class InternalAuditLogsController : ControllerBase
{
    private readonly ISubmissionAuditLogRepository _auditLogRepository;

    public InternalAuditLogsController(ISubmissionAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 50;

        var (items, totalCount) = await _auditLogRepository.ListAsync(userId, entityType, action, page, pageSize, ct);

        var dtos = items.Select(l => new SubmissionAuditLogEntryDto(
            l.Id, l.Action, l.EntityType, l.EntityId, l.SubjectId, l.PerformedBy, l.Details, l.PerformedAt)).ToList();

        var result = new SubmissionAuditLogPageDto(dtos, totalCount);

        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }
}
