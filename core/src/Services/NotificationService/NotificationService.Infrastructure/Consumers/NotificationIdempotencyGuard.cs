using StackExchange.Redis;

namespace NotificationService.Infrastructure.Consumers;

/// <summary>
/// Redis-backed dedup claim shared by every event consumer here, mirroring the pattern used by
/// SubmissionService's ParseBatchConsumer: claim a key for MessageId before processing, release
/// it on failure so MassTransit's retry/redelivery can safely re-run. Without Redis configured,
/// every call is treated as new (no dedup) rather than blocking processing.
/// </summary>
public class NotificationIdempotencyGuard
{
    private readonly IConnectionMultiplexer? _redis;

    public NotificationIdempotencyGuard(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    public async Task<bool> TryClaimAsync(string key)
    {
        var db = _redis?.GetDatabase();
        if (db is null) return true;

        return await db.StringSetAsync(key, "1", TimeSpan.FromHours(24), When.NotExists);
    }

    public async Task ReleaseAsync(string key)
    {
        var db = _redis?.GetDatabase();
        if (db is null) return;

        try { await db.KeyDeleteAsync(key); }
        catch { /* best-effort */ }
    }
}
