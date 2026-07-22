using Contracts.Domain;

namespace GradingService.Domain.Entities;

public class MarkerAssignment : Entity
{
    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    public int AliasStart { get; set; }

    public int AliasEnd { get; set; }

    /// <summary>When set, this assignment covers every paper in the uploaded ZIP batch.</summary>
    public Guid? BatchId { get; set; }

    public string? ZipFileName { get; set; }

    public Guid AssignedBy { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
