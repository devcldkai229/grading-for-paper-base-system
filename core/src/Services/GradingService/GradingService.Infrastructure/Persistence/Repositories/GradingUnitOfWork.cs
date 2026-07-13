using GradingService.Application.Interfaces;
using GradingService.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GradingService.Infrastructure.Persistence.Repositories;

public class GradingUnitOfWork : IUnitOfWork
{
    private readonly GradingDbContext _db;

    public GradingUnitOfWork(GradingDbContext db)
    {
        _db = db;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(ex.Message, ex);
        }
    }
}
