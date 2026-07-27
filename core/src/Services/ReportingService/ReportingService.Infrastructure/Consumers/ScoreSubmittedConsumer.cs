using Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;
using ReportingService.Application.DTOs;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Consumers;

/// <summary>
/// Projects a submitted paper's score + feedback into <c>score_record</c>/<c>feedback_record</c> and
/// recomputes score aggregates. Idempotent (EF inbox) and out-of-order safe (latest-wins by OccurredAt).
/// </summary>
public class ScoreSubmittedConsumer : IConsumer<ScoreSubmitted>
{
    private readonly ReportingDbContext _db;
    private readonly ILogger<ScoreSubmittedConsumer> _logger;

    public ScoreSubmittedConsumer(ReportingDbContext db, ILogger<ScoreSubmittedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ScoreSubmitted> context)
    {
        var m = context.Message;
        var ct = context.CancellationToken;

        var questionsJson = FeedbackQuestionsJson.Serialize(m.Questions);
        var changed = await ScoreProjectionWriter.ApplyAsync(
            _db, m.PaperId, m.SubjectId, m.TeacherId, m.AliasNumber, m.StudentAlias,
            m.TotalScore, m.MaxScore, m.PaperComment, questionsJson, m.OccurredAt, ct);

        if (!changed)
        {
            _logger.LogDebug("Ignored stale ScoreSubmitted for paper {PaperId}", m.PaperId);
            return;
        }

        await ProjectionRecomputer.RecomputeSubjectAsync(_db, m.SubjectId, ct);
        await ProjectionRecomputer.RecomputeMarkerAsync(_db, m.SubjectId, m.TeacherId, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Score projected (submitted): paper={PaperId} subject={SubjectId} total={Total}",
            m.PaperId, m.SubjectId, m.TotalScore);
    }
}
