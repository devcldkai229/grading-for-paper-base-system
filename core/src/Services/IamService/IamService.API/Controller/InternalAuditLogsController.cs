using IamService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IamService.API.Controller;

/// <summary>
/// Internal (service-to-service) endpoint for ReportingService's global audit log viewer.
/// Protected by InternalApiKeyMiddleware (path prefix /api/internal) — no JWT/[Authorize] here.
/// </summary>
[Route("api/internal/audit-logs")]
[ApiController]
public class InternalAuditLogsController : ControllerBase
{
    private readonly IUserService _userService;

    public InternalAuditLogsController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 500) pageSize = 50;

        var result = await _userService.GetAuditLogsAsync(
            page, pageSize, userId, action, entityType, startDate: null, endDate: null);

        return Ok(new { data = result, responsedAt = DateTime.UtcNow });
    }
}
