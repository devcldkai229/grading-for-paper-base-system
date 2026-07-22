using Microsoft.EntityFrameworkCore;
using ReportingService.Domain.Entities;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Consumers;

/// <summary>
/// Applies a submitted/overridden score to the <c>score_record</c> and <c>feedback_record</c> projections.
/// Both are keyed on PaperId with latest-wins semantics on OccurredAt, so a stale (out-of-order) event is
/// ignored. Returns <c>true</c> when the projections changed (so the caller should recompute aggregates).
/// </summary>
internal static class ScoreProjectionWriter
{
    public static async Task<bool> ApplyAsync(
        ReportingDbContext db,
        Guid paperId, Guid subjectId, Guid teacherId, int? aliasNumber, string? studentAlias,
        decimal totalScore, decimal maxScore, string? paperComment, string questionsJson,
        DateTime occurredAt, CancellationToken ct)
    {
        var score = await db.ScoreRecords.FirstOrDefaultAsync(s => s.PaperId == paperId, ct);
        if (score is not null && occurredAt <= score.OccurredAt)
            return false; // a newer score already applied — ignore this out-of-order/duplicate event

        var now = DateTime.UtcNow;

        if (score is null)
        {
            score = new ScoreRecord { PaperId = paperId };
            db.ScoreRecords.Add(score);
        }
        score.SubjectId = subjectId;
        score.TeacherId = teacherId;
        score.AliasNumber = aliasNumber ?? score.AliasNumber;
        score.TotalScore = totalScore;
        score.MaxScore = maxScore;
        score.OccurredAt = occurredAt;
        score.UpdatedAt = now;

        var feedback = await db.FeedbackRecords.FirstOrDefaultAsync(f => f.PaperId == paperId, ct);
        if (feedback is null)
        {
            feedback = new FeedbackRecord { PaperId = paperId };
            db.FeedbackRecords.Add(feedback);
        }
        feedback.SubjectId = subjectId;
        feedback.TeacherId = teacherId;
        feedback.AliasNumber = aliasNumber ?? feedback.AliasNumber;
        feedback.StudentAlias = studentAlias ?? feedback.StudentAlias;
        feedback.TotalScore = totalScore;
        feedback.MaxScore = maxScore;
        feedback.PaperComment = paperComment;
        feedback.QuestionsJson = questionsJson;
        feedback.OccurredAt = occurredAt;
        feedback.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return true;
    }
}
