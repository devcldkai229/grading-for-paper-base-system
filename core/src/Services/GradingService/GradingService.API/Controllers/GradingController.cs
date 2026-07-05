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
        var (result, conflict) = await _gradingSessionService.SaveMarksAsync(
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
}

