using Contracts.Domain;
using GradingService.Domain.Enums;

namespace GradingService.Domain.Entities;

public class GradingAssignment : Entity
{
    public Guid StudentPaperId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    public AssignmentType AssignmentType { get; set; } = AssignmentType.FirstGrade;

    public GradingProgressStatus Status { get; set; } = GradingProgressStatus.NotStarted;

    public AiSyncStatus AiStatus { get; set; } = AiSyncStatus.NotRequested;

    /// <summary>Independent needs-review marker set by the grading lecturer — does not replace or interact with Status.</summary>
    public bool IsFlagged { get; set; }

    public GradingForm? GradingForm { get; set; }
}
