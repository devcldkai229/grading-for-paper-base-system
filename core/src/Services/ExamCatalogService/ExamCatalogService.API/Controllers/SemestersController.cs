using ExamCatalogService.API.Authorization;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamCatalogService.API.Controllers;

[Route("api/semesters")]
[ApiController]
[Authorize]
public class SemestersController : ControllerBase
{
    private readonly IExamCatalogRepository _repository;

    public SemestersController(IExamCatalogRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// List semesters with optional active filter and pagination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetSemesters(
        [FromQuery] bool? active,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var (items, totalCount) = await _repository.GetSemestersAsync(active, page, pageSize, ct);

        var result = new PagedResult<Application.DTOs.SemesterDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(new ApiResponse<PagedResult<Application.DTOs.SemesterDto>>
        {
            StatusCode = 200,
            Message = "Semesters retrieved successfully",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get a single semester by id.
    /// </summary>
    [HttpGet("{semesterId:guid}")]
    public async Task<IActionResult> GetSemester(Guid semesterId, CancellationToken ct = default)
    {
        var semester = await _repository.GetSemesterByIdAsync(semesterId, ct);
        if (semester is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Semester not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<Application.DTOs.SemesterDto>
        {
            StatusCode = 200,
            Message = "Semester retrieved successfully",
            Data = semester,
            ResponsedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// List exams within a semester (paginated).
    /// </summary>
    [HttpGet("{semesterId:guid}/exams")]
    public async Task<IActionResult> GetExamsBySemester(
        Guid semesterId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

        var (items, totalCount) = await _repository.GetExamsBySemesterAsync(
            semesterId, page, pageSize, ct);

        var result = new PagedResult<Application.DTOs.ExamDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(new ApiResponse<PagedResult<Application.DTOs.ExamDto>>
        {
            StatusCode = 200,
            Message = "Exams retrieved successfully",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPost]
    [AdminOnly]
    public async Task<IActionResult> CreateSemester(
        [FromBody] CreateSemesterRequest request,
        CancellationToken ct = default)
    {
        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "End date must be after start date",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        try
        {
            var semester = await _repository.CreateSemesterAsync(request, ct);
            return CreatedAtAction(nameof(GetSemester), new { semesterId = semester.Id }, new ApiResponse<SemesterDto>
            {
                StatusCode = 201,
                Message = "Semester created successfully",
                Data = semester,
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Semester code already exists",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpPut("{semesterId:guid}")]
    [AdminOnly]
    public async Task<IActionResult> UpdateSemester(
        Guid semesterId,
        [FromBody] UpdateSemesterRequest request,
        CancellationToken ct = default)
    {
        if (request.EndDate <= request.StartDate)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "End date must be after start date",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        try
        {
            var semester = await _repository.UpdateSemesterAsync(semesterId, request, ct);
            if (semester is null)
            {
                return NotFound(new ApiResponse<object>
                {
                    StatusCode = 404,
                    Message = "Semester not found",
                    Data = null!,
                    ResponsedAt = DateTime.UtcNow
                });
            }

            return Ok(new ApiResponse<SemesterDto>
            {
                StatusCode = 200,
                Message = "Semester updated successfully",
                Data = semester,
                ResponsedAt = DateTime.UtcNow
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new ApiResponse<object>
            {
                StatusCode = 409,
                Message = "Semester code already exists",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }
    }

    [HttpDelete("{semesterId:guid}")]
    [AdminOnly]
    public async Task<IActionResult> DeleteSemester(Guid semesterId, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteSemesterAsync(semesterId, ct);
        if (!deleted)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Semester not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<object>
        {
            StatusCode = 200,
            Message = "Semester deleted successfully",
            Data = null!,
            ResponsedAt = DateTime.UtcNow
        });
    }
}
