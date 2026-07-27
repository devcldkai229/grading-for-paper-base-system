using ExamCatalogService.API.Authorization;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ExamCatalogService.API.Controllers;

/// <summary>
/// Reusable, subject-agnostic score grid templates: an Admin saves a named question/criteria
/// structure once and applies it to any subject by loading its rows into that subject's own
/// grid (via the existing PUT /api/subjects/{id}/questions) — applied rows are then ordinary,
/// independently-editable subject questions with no lingering link back to the template.
/// </summary>
[Route("api/score-grid-templates")]
[ApiController]
[Authorize]
[AdminOnly]
public class ScoreGridTemplatesController : ControllerBase
{
    private readonly IScoreGridTemplateRepository _repository;
    private readonly ISubjectAdminService _subjectAdminService;

    public ScoreGridTemplatesController(
        IScoreGridTemplateRepository repository,
        ISubjectAdminService subjectAdminService)
    {
        _repository = repository;
        _subjectAdminService = subjectAdminService;
    }

    [HttpGet]
    public async Task<IActionResult> ListTemplates(CancellationToken ct = default)
    {
        var templates = await _repository.ListAsync(ct);

        return Ok(new ApiResponse<IReadOnlyList<ScoreGridTemplateSummaryDto>>
        {
            StatusCode = 200,
            Message = "Score grid templates retrieved successfully",
            Data = templates,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpGet("{templateId:guid}")]
    public async Task<IActionResult> GetTemplate(Guid templateId, CancellationToken ct = default)
    {
        var template = await _repository.GetByIdAsync(templateId, ct);
        if (template is null)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Template not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<ScoreGridTemplateDetailDto>
        {
            StatusCode = 200,
            Message = "Template retrieved successfully",
            Data = template,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] CreateScoreGridTemplateRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = "Template name is required",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var (errors, _) = _subjectAdminService.ValidateTemplateQuestions(request.Questions);
        if (errors.Count > 0)
        {
            return BadRequest(new ApiResponse<object>
            {
                StatusCode = 400,
                Message = string.Join("; ", errors),
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        var createdBy = GetUserId();
        var template = await _repository.CreateAsync(request.Name, createdBy, request.Questions, ct);

        return Ok(new ApiResponse<ScoreGridTemplateSummaryDto>
        {
            StatusCode = 200,
            Message = "Template saved successfully",
            Data = template,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> DeleteTemplate(Guid templateId, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteAsync(templateId, ct);
        if (!deleted)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Template not found",
                Data = null!,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<object>
        {
            StatusCode = 200,
            Message = "Template deleted successfully",
            Data = null!,
            ResponsedAt = DateTime.UtcNow
        });
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
        return Guid.Parse(raw);
    }
}
