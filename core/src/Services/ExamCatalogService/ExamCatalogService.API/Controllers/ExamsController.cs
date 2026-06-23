using ExamCatalogService.API.Authorization;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamCatalogService.API.Controllers;

[Route("api/exams")]
[ApiController]
[Authorize]
public class ExamsController : ControllerBase
{
    private readonly IExamCatalogRepository _repository;

    public ExamsController(IExamCatalogRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get a single exam by id.
    /// </summary>
    [HttpGet("{examId:guid}")]
    public async Task<IActionResult> GetExam(Guid examId, CancellationToken ct = default)
    {
        var exam = await _repository.GetExamByIdAsync(examId, ct);
        if (exam is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Exam not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<Application.DTOs.ExamDto>
        {
            StatusCode = 200,
            Message = "Exam retrieved successfully",
            Data = exam,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// List subjects within an exam (paginated).
    /// </summary>
    [HttpGet("{examId:guid}/subjects")]
    public async Task<IActionResult> GetSubjectsByExam(
        Guid examId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, totalCount) = await _repository.GetSubjectsByExamAsync(
            examId, page, pageSize, ct);

        var result = new PagedResult<Application.DTOs.SubjectSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(new ApiResponse<PagedResult<Application.DTOs.SubjectSummaryDto>>
        {
            StatusCode = 200,
            Message = "Subjects retrieved successfully",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPost]
    [AdminOnly]
    public async Task<IActionResult> CreateExam(
        [FromBody] CreateExamRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var exam = await _repository.CreateExamAsync(request, ct);
            return CreatedAtAction(nameof(GetExam), new { examId = exam.Id }, new ApiResponse<ExamDto>
            {
                StatusCode = 201,
                Message = "Exam created successfully",
                Data = exam,
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (DbUpdateException)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "Invalid semester or exam data",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpPut("{examId:guid}")]
    [AdminOnly]
    public async Task<IActionResult> UpdateExam(
        Guid examId,
        [FromBody] UpdateExamRequest request,
        CancellationToken ct = default)
    {
        var exam = await _repository.UpdateExamAsync(examId, request, ct);
        if (exam is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Exam not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<ExamDto>
        {
            StatusCode = 200,
            Message = "Exam updated successfully",
            Data = exam,
            ResponsedAt = DateTime.UtcNow
        });
    }
}
