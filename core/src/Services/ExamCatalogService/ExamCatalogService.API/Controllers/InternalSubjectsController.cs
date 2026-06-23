using ExamCatalogService.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ExamCatalogService.API.Controllers;

[Route("api/internal/subjects")]
[ApiController]
public class InternalSubjectsController : ControllerBase
{
    private readonly IExamCatalogRepository _repository;

    public InternalSubjectsController(IExamCatalogRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("{subjectId:guid}/grading-grid")]
    public async Task<IActionResult> GetGradingGrid(Guid subjectId, CancellationToken ct = default)
    {
        var subject = await _repository.GetSubjectDetailAsync(subjectId, ct);
        if (subject is null)
        {
            return NotFound(new { statusCode = 404, message = "Subject not found", responsedAt = DateTime.UtcNow });
        }

        var grid = new
        {
            subjectId = subject.Id,
            maxScore = subject.MaxScore,
            rubricVersion = subject.RubricVersion,
            questions = subject.Questions
                .OrderBy(q => q.OrderIndex)
                .Select(q => new
                {
                    questionNumber = q.QuestionNumber,
                    groupLabel = q.GroupLabel,
                    label = q.Label,
                    maxScore = q.MaxScore,
                    orderIndex = q.OrderIndex
                })
        };

        return Ok(new { data = grid, responsedAt = DateTime.UtcNow });
    }
}
