namespace ExamCatalogService.Domain.Entities;

/// <summary>
/// Read-only local projection of marker assignments owned by GradingService's Allocation module,
/// kept up to date via MarkerAssignmentChanged events. Lets subject search scope a lecturer to the
/// subjects they're assigned to grade WITHOUT a synchronous cross-service call (N5 — no availability
/// coupling). <see cref="Id"/> mirrors the source MarkerAssignment id so upserts/deletes are exact
/// and naturally idempotent (N8).
/// </summary>
public class MarkerAssignmentView
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
    public int AliasStart { get; set; }
    public int AliasEnd { get; set; }
    public DateTime UpdatedAt { get; set; }
}
