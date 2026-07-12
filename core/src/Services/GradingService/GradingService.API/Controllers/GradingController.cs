using GradingService.API;
using GradingService.Application.DTOs;
using GradingService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace GradingService.API.Controllers;

[Route("api/grading")]
[ApiController]
[Authorize]
public class GradingController : ControllerBase
{
    private readonly IGradingSessionService _gradingSessionService;

    public GradingController(IGradingSessionService gradingSessionService)
    {
        _gradingSessionService = gradingSessionService;
    }

    [HttpPost("batches/{batchId:guid}/start")]
    public async Task<IActionResult> StartBatch(Guid batchId, CancellationToken ct = default)
    {
        var teacherId = GetUserId();
        var result = await _gradingSessionService.StartBatchAsync(batchId, teacherId, ct);

        if (result is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Batch not found or access denied",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<StartBatchResultDto>
        {
            StatusCode = 200,
            Message = "Grading session started",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpGet("sessions/{assignmentId:guid}")]
    public async Task<IActionResult> GetSession(Guid assignmentId, CancellationToken ct = default)
    {
        var teacherId = GetUserId();
        var session = await _gradingSessionService.GetSessionAsync(assignmentId, teacherId, ct);

        if (session is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Session not found",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<GradingSessionDto>
        {
            StatusCode = 200,
            Message = "Session retrieved",
            Data = session,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPut("sessions/{assignmentId:guid}/marks")]
    public async Task<IActionResult> SaveMarks(
        Guid assignmentId,
        [FromBody] SaveMarksRequest request,
        CancellationToken ct = default)
    {
        var teacherId = GetUserId();
        var (result, conflict, error) = await _gradingSessionService.SaveMarksAsync(
            assignmentId, teacherId, request, ct);

        if (conflict)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Grading form was modified by another session. Please reload.",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (error is not null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = error,
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (result is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Session not found or already submitted",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<SaveMarksResultDto>
        {
            StatusCode = 200,
            Message = "Marks saved",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPost("sessions/{assignmentId:guid}/submit")]
    public async Task<IActionResult> Submit(Guid assignmentId, CancellationToken ct = default)
    {
        var teacherId = GetUserId();
        var (result, forbidden, notFound) = await _gradingSessionService.SubmitAsync(
            assignmentId, teacherId, ct);

        if (notFound)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Session not found",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (forbidden)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "This paper has already been submitted",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<SubmitResultDto>
        {
            StatusCode = 200,
            Message = "Submitted successfully",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Admin-only: look up any assignment (regardless of marker/status) to review before overriding.
    /// Powers the Admin override tool, since the normal grading queue (GetSession) is scoped to
    /// the caller's own assignments.
    /// </summary>
    [HttpGet("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> GetAssignmentForOverride(Guid assignmentId, CancellationToken ct = default)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var session = await _gradingSessionService.GetAssignmentForOverrideAsync(assignmentId, ct);

        if (session is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Assignment not found",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<GradingSessionDto>
        {
            StatusCode = 200,
            Message = "Assignment retrieved",
            Data = session,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Admin overrides any assignment's scores; a Lecturer may only override an assignment
    /// they are the marker for. Unlike SaveMarks, this works even after the form was Submitted.
    /// A Reason is mandatory and every call is fully audited (old/new value snapshot).
    /// </summary>
    [HttpPut("assignments/{assignmentId:guid}/override")]
    public async Task<IActionResult> OverrideMarks(
        Guid assignmentId,
        [FromBody] OverrideMarksRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "A reason is required to override scores.",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var userId = GetUserId();
        var isAdmin = IsAdmin();

        var (result, notFound, error) = await _gradingSessionService.OverrideMarksAsync(
            assignmentId, userId, isAdmin, request, ct);

        if (notFound)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Session not found or access denied",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        if (error is not null)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = error,
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<OverrideMarksResultDto>
        {
            StatusCode = 200,
            Message = "Scores overridden",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Full audit history (old/new value snapshots) for an assignment's grading form.
    /// Same access rule as override: Admin sees any, Lecturer only their own.
    /// </summary>
    [HttpGet("assignments/{assignmentId:guid}/audit-log")]
    public async Task<IActionResult> GetAuditLog(Guid assignmentId, CancellationToken ct = default)
    {
        var userId = GetUserId();
        var isAdmin = IsAdmin();

        var (result, notFound) = await _gradingSessionService.GetAuditTrailAsync(
            assignmentId, userId, isAdmin, ct);

        if (notFound)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Session not found or access denied",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<IReadOnlyList<AuditLogEntryDto>>
        {
            StatusCode = 200,
            Message = "Audit log retrieved",
            Data = result!,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Aggregate grading progress for the caller: done/total counters, upcoming exam deadlines
    /// for subjects that still have ungraded papers, and a quick-link assignment id to resume.
    /// </summary>
    [HttpGet("my-progress")]
    public async Task<IActionResult> GetMyProgress(CancellationToken ct = default)
    {
        var teacherId = GetUserId();
        var result = await _gradingSessionService.GetMyProgressAsync(teacherId, ct);

        return Ok(new ApiResponse<MyProgressDto>
        {
            StatusCode = 200,
            Message = "Progress retrieved",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpGet("subjects/{subjectId:guid}/export")]
    public async Task<IActionResult> ExportGrades(Guid subjectId, CancellationToken ct = default)
    {
        var bytes = await _gradingSessionService.ExportGradesAsync(subjectId, ct);
        if (bytes is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "No grades found for this subject",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Subject_Grades_{subjectId}.xlsx");
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
        return Guid.Parse(raw);
    }

    private bool IsAdmin()
    {
        // TokenService issues the role claim with literal Type "Role" (not the ClaimTypes.Role URI,
        // and not lowercase "role") — .NET's default inbound claim map only remaps "role" (lowercase),
        // so neither ClaimTypes.Role nor "role" ever matches a real token. Must match the exact case.
        var role = User.FindFirst("Role")?.Value;
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }
}

