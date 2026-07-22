namespace ReportingService.Domain.Entities;

/// <summary>
/// Per-paper feedback projection built from <c>ScoreSubmitted</c>/<c>ScoreOverridden</c> events. Backs
/// the per-subject feedback export (a submitted paper is considered "releasable"). Keyed on
/// <see cref="PaperId"/> with latest-wins semantics on <see cref="OccurredAt"/>. The per-question
/// breakdown is stored as JSON (<see cref="QuestionsJson"/>) to keep the projection flat.
/// </summary>
public class FeedbackRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaperId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
    public int? AliasNumber { get; set; }
    public string? StudentAlias { get; set; }
    public decimal TotalScore { get; set; }
    public decimal MaxScore { get; set; }
    public string? PaperComment { get; set; }

    /// <summary>Serialized array of per-question feedback (question number, label, score, max, comment).</summary>
    public string QuestionsJson { get; set; } = "[]";
    public DateTime OccurredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
