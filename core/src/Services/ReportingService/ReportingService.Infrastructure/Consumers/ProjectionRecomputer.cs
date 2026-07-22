using Microsoft.EntityFrameworkCore;
using ReportingService.Domain.Entities;
using ReportingService.Infrastructure.Persistence;

namespace ReportingService.Infrastructure.Consumers;

/// <summary>
/// Recomputes the <c>subject_progress</c> and <c>marker_progress</c> read models from the granular
/// <c>assignment_progress</c> and <c>score_record</c> projections. Doing a full recompute per affected
/// subject/marker (papers per subject are bounded) keeps the counts correct across out-of-order and
/// duplicate deliveries without having to track status deltas inline. Callers are responsible for the
/// final <c>SaveChangesAsync</c>.
/// </summary>
internal static class ProjectionRecomputer
{
    public static async Task RecomputeSubjectAsync(ReportingDbContext db, Guid subjectId, CancellationToken ct)
    {
        var total = await db.AssignmentProgress.CountAsync(a => a.SubjectId == subjectId, ct);
        var completed = await db.AssignmentProgress
            .CountAsync(a => a.SubjectId == subjectId && a.Status == "Submitted", ct);
        var drafting = await db.AssignmentProgress
            .CountAsync(a => a.SubjectId == subjectId && a.Status == "Drafting", ct);
        var notStarted = Math.Max(0, total - completed - drafting);

        var scores = await db.ScoreRecords
            .Where(s => s.SubjectId == subjectId)
            .Select(s => s.TotalScore)
            .ToListAsync(ct);

        var row = await db.SubjectProgress.FirstOrDefaultAsync(s => s.SubjectId == subjectId, ct);
        if (row is null)
        {
            row = new SubjectProgress { SubjectId = subjectId };
            db.SubjectProgress.Add(row);
        }

        row.TotalPapers = total;
        row.CompletedPapers = completed;
        row.InProgress = drafting;
        row.NotStarted = notStarted;
        row.ScoreAvg = scores.Count > 0 ? Math.Round(scores.Average(), 2) : null;
        row.ScoreMin = scores.Count > 0 ? scores.Min() : null;
        row.ScoreMax = scores.Count > 0 ? scores.Max() : null;
    }

    public static async Task RecomputeMarkerAsync(
        ReportingDbContext db, Guid subjectId, Guid teacherId, CancellationToken ct)
    {
        var assigned = await db.AssignmentProgress
            .CountAsync(a => a.SubjectId == subjectId && a.TeacherId == teacherId, ct);
        var completed = await db.AssignmentProgress
            .CountAsync(a => a.SubjectId == subjectId && a.TeacherId == teacherId && a.Status == "Submitted", ct);
        var drafting = await db.AssignmentProgress
            .CountAsync(a => a.SubjectId == subjectId && a.TeacherId == teacherId && a.Status == "Drafting", ct);

        var markerScores = await db.ScoreRecords
            .Where(s => s.SubjectId == subjectId && s.TeacherId == teacherId)
            .Select(s => new { s.TotalScore, s.OccurredAt })
            .ToListAsync(ct);

        var row = await db.MarkerProgress
            .FirstOrDefaultAsync(m => m.SubjectId == subjectId && m.TeacherId == teacherId, ct);

        // Drop the marker row once they no longer have any assignments for this subject (e.g. reassigned).
        if (assigned == 0)
        {
            if (row is not null) db.MarkerProgress.Remove(row);
            return;
        }

        if (row is null)
        {
            row = new MarkerProgress { SubjectId = subjectId, TeacherId = teacherId };
            db.MarkerProgress.Add(row);
        }

        row.AssignedCount = assigned;
        row.CompletedCount = completed;
        row.DraftingCount = drafting;
        row.AvgScore = markerScores.Count > 0 ? Math.Round(markerScores.Average(x => x.TotalScore), 2) : null;

        if (markerScores.Count > 0)
        {
            row.LastActivityAt = markerScores.Max(x => x.OccurredAt);
            var span = (markerScores.Max(x => x.OccurredAt) - markerScores.Min(x => x.OccurredAt)).TotalHours;
            row.ThroughputPerHour = span > 0
                ? Math.Round((decimal)(markerScores.Count / span), 2)
                : null;
        }
        else
        {
            row.ThroughputPerHour = null;
        }
    }
}
