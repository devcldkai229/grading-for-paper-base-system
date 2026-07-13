using ReportingService.Domain.Entities;

namespace ReportingService.Application.Interfaces;

public interface IExportJobRepository
{
    Task AddAsync(ExportJob job, CancellationToken ct = default);
}
