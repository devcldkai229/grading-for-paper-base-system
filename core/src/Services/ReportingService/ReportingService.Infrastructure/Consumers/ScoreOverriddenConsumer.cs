using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using ReportingService.Application.DTOs;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Consumers;

/// <summary>
/// Same projection as <see cref="ScoreSubmittedConsumer"/> but for re-graded (overridden) scores. The
/// override supersedes the original submission (latest-wins by OccurredAt) on the same PaperId-keyed rows.
/// </summary>
public class ScoreOverriddenConsumer : IConsumer<ScoreOverridden>
{
    private readonly ReportingDbContext _db;
    private readonly ILogger<ScoreOverriddenConsumer> _logger;

    public ScoreOverriddenConsumer(ReportingDbContext db, ILogger<ScoreOverriddenConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ScoreOverridden> context)
    {
        var m = context.Message;
        var ct = context.CancellationToken;

        var questionsJson = FeedbackQuestionsJson.Serialize(m.Questions);
        var changed = await ScoreProjectionWriter.ApplyAsync(
            _db, m.PaperId, m.SubjectId, m.TeacherId, m.AliasNumber, m.StudentAlias,
            m.TotalScore, m.MaxScore, m.PaperComment, questionsJson, m.OccurredAt, ct);

        if (!changed)
        {
            _logger.LogDebug("Ignored stale ScoreOverridden for paper {PaperId}", m.PaperId);
            return;
        }

        await ProjectionRecomputer.RecomputeSubjectAsync(_db, m.SubjectId, ct);
        await ProjectionRecomputer.RecomputeMarkerAsync(_db, m.SubjectId, m.TeacherId, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Score projected (overridden): paper={PaperId} subject={SubjectId} total={Total}",
            m.PaperId, m.SubjectId, m.TotalScore);
    }
}
