using Contracts.Domain;

namespace GradingService.Domain.Entities;

public class GradingResumePointer : Entity
{
    public Guid TeacherId { get; set; }

    public Guid BatchId { get; set; }

    public Guid AssignmentId { get; set; }

    public void UpdateAssignment(Guid assignmentId)
    {
        AssignmentId = assignmentId;
        UpdatedAt = DateTime.UtcNow;
    }
}
