using Contracts.Domain;

namespace ReportingService.Domain.Entities;

public class SubjectProgress : Entity
{
    public Guid SubjectId { get; set; }

    public int TotalPapers { get; set; }

    public int CompletedPapers { get; set; }

    public int InProgress { get; set; }

    public int NotStarted { get; set; }

    public decimal? ScoreAvg { get; set; }

    public decimal? ScoreMin { get; set; }

    public decimal? ScoreMax { get; set; }

    public DateTime? EstimatedFinish { get; set; }

    public SubjectProgress()
    {
        UpdatedAt = DateTime.UtcNow;
    }
}
