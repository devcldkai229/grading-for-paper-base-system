namespace GradingService.Application.Interfaces;

/// <summary>
/// Persists staged changes across the repositories sharing this unit of work's underlying scope.
/// Throws GradingService.Domain.Exceptions.ConcurrencyConflictException on an optimistic-concurrency violation.
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
