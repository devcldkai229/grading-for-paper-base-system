namespace ReportingService.Domain.Entities;

/// <summary>
/// Per-paper submitted score projection built from <c>ScoreSubmitted</c>/<c>ScoreOverridden</c> events.
/// Backs the admin score-distribution and pass/fail reports. Keyed on <see cref="PaperId"/> with
/// latest-wins semantics on <see cref="OccurredAt"/> (an override supersedes the original submission).
/// </summary>
public class ScoreRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaperId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
    public int? AliasNumber { get; set; }
    public decimal TotalScore { get; set; }
    public decimal MaxScore { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
