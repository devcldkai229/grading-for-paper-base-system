using ReportingService.Application.Interfaces;
using ReportingService.Domain.Entities;

namespace ReportingService.Infrastructure.Persistence.Repositories;

public class ExportJobRepository : IExportJobRepository
{
    private readonly ReportingDbContext _db;

    public ExportJobRepository(ReportingDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(ExportJob job, CancellationToken ct = default)
    {
        _db.ExportJobs.Add(job);
        await _db.SaveChangesAsync(ct);
    }
}
