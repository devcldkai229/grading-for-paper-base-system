namespace ReportingService.Domain.Entities;

/// <summary>
/// Per-assignment lifecycle projection built from <c>GradingAssignmentStatusChanged</c> events. It is
/// the granular source of truth from which <see cref="SubjectProgress"/> and <see cref="MarkerProgress"/>
/// counts are recomputed, so status transitions (NotStarted → Drafting → Submitted) stay correct
/// without needing to know the previous status inline. Keyed on <see cref="AssignmentId"/>.
/// </summary>
public class AssignmentProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid SubjectId { get; set; }
    public Guid TeacherId { get; set; }
    public string Status { get; set; } = "NotStarted";
    public int? AliasNumber { get; set; }

    /// <summary>Timestamp of the source event; used as an out-of-order guard (latest-wins).</summary>
    public DateTime OccurredAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
