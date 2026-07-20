using GradingService.Application.Interfaces;
using StackExchange.Redis;

namespace GradingService.Infrastructure.Redis;

/// <summary>
/// Distributed lock per assignment to prevent duplicate concurrent AI grading runs.
/// </summary>
public interface IAiGradeLockService
{
    Task<bool> TryAcquireAsync(Guid assignmentId, TimeSpan ttl, CancellationToken ct = default);
    Task ReleaseAsync(Guid assignmentId, CancellationToken ct = default);
}

public sealed class RedisAiGradeLockService : IAiGradeLockService
{
    private const string KeyPrefix = "ai:grade:lock:";
    private readonly IConnectionMultiplexer _redis;

    public RedisAiGradeLockService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<bool> TryAcquireAsync(Guid assignmentId, TimeSpan ttl, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        return await db.StringSetAsync(
            KeyPrefix + assignmentId.ToString("N"),
            "1",
            ttl,
            When.NotExists);
    }

    public async Task ReleaseAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(KeyPrefix + assignmentId.ToString("N"));
    }
}

/// <summary>Fallback when Redis is not configured — always grants the lock.</summary>
public sealed class NoOpAiGradeLockService : IAiGradeLockService
{
    public Task<bool> TryAcquireAsync(Guid assignmentId, TimeSpan ttl, CancellationToken ct = default) =>
        Task.FromResult(true);

    public Task ReleaseAsync(Guid assignmentId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
