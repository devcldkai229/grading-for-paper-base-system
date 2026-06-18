using Contracts.Domain;

namespace ReportingService.Domain.Entities;

public class MarkerProgress : Entity
{
    public Guid SubjectId { get; set; }

    public Guid TeacherId { get; set; }

    public int AssignedCount { get; set; }

    public int CompletedCount { get; set; }

    public int DraftingCount { get; set; }

    public decimal? AvgScore { get; set; }

    public decimal? ThroughputPerHour { get; set; }

    public DateTime? LastActivityAt { get; set; }

    public MarkerProgress()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
