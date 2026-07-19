using System.Text.Json;
using ExamCatalogService.Application.DTOs;
using ExamCatalogService.Application.Interfaces;
using ExamCatalogService.Domain.Entities;
using ExamCatalogService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExamCatalogService.Infrastructure.Repositories;

public class ScoreGridTemplateRepository : IScoreGridTemplateRepository
{
    private readonly ExamCatalogDbContext _context;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ScoreGridTemplateRepository(ExamCatalogDbContext context)
    {
        _context = context;
    }

    public async Task<ScoreGridTemplateSummaryDto> CreateAsync(
        string name, Guid createdBy, IReadOnlyList<QuestionInputDto> questions, CancellationToken ct = default)
    {
        var template = new ScoreGridTemplate
        {
            Name = name.Trim(),
            CreatedBy = createdBy,
            QuestionsJson = JsonSerializer.Serialize(questions, JsonOptions),
            QuestionCount = questions.Count
        };

        _context.ScoreGridTemplates.Add(template);
        await _context.SaveChangesAsync(ct);

        return new ScoreGridTemplateSummaryDto(
            template.Id, template.Name, template.CreatedBy, template.QuestionCount, template.CreatedAt);
    }

    public async Task<IReadOnlyList<ScoreGridTemplateSummaryDto>> ListAsync(CancellationToken ct = default) =>
        await _context.ScoreGridTemplates
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new ScoreGridTemplateSummaryDto(t.Id, t.Name, t.CreatedBy, t.QuestionCount, t.CreatedAt))
            .ToListAsync(ct);

    public async Task<ScoreGridTemplateDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _context.ScoreGridTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return null;

        var questions = JsonSerializer.Deserialize<List<QuestionInputDto>>(template.QuestionsJson, JsonOptions)
            ?? new List<QuestionInputDto>();

        return new ScoreGridTemplateDetailDto(template.Id, template.Name, template.CreatedBy, questions, template.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _context.ScoreGridTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return false;

        _context.ScoreGridTemplates.Remove(template);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
