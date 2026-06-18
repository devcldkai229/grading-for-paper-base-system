using Contracts.Domain;
using ReportingService.Domain.Enums;

namespace ReportingService.Domain.Entities;

public class ExportJob : Entity
{
    public Guid SubjectId { get; set; }

    public Guid RequestedBy { get; set; }

    public string? ResultS3Key { get; set; }

    public string? ResultFileName { get; set; }

    public ExportStatus Status { get; set; } = ExportStatus.Queued;

    public string? ErrorMessage { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public ExportJob()
    {
        RequestedAt = DateTime.UtcNow;
    }
}
