using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.AspNetCore.Observability;

/// <summary>
/// Registers the <c>outbox_pending_messages</c> gauge (§9.1b) for services that use the MassTransit
/// EF outbox. The gauge reports how many messages are still sitting in the <c>OutboxMessage</c> table
/// waiting to be dispatched — a growing value signals the broker/dispatcher is falling behind.
/// </summary>
public static class OutboxMetricsExtensions
{
    public static IServiceCollection AddOutboxPendingMetric<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddHostedService<OutboxPendingMetricRegistrar<TContext>>();
        return services;
    }
}

/// <summary>
/// Creates the observable gauge on startup. The callback runs on OpenTelemetry's collection cadence,
/// opens a short-lived scope, and counts outbox rows via a plain SQL COUNT so we avoid coupling this
/// building block to MassTransit's entity types. Failures (e.g. table not migrated yet) report 0.
/// </summary>
internal sealed class OutboxPendingMetricRegistrar<TContext> : IHostedService
    where TContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPendingMetricRegistrar<TContext>> _logger;

    public OutboxPendingMetricRegistrar(
        IServiceProvider serviceProvider,
        ILogger<OutboxPendingMetricRegistrar<TContext>> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        PlatformMetrics.Business.CreateObservableGauge(
            "outbox_pending_messages",
            CountPending,
            unit: "{messages}",
            description: "Undispatched messages currently sitting in the MassTransit EF outbox.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private long CountPending()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM \"OutboxMessage\"";
                var result = command.ExecuteScalar();
                return result is null or DBNull ? 0L : Convert.ToInt64(result);
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read outbox_pending_messages gauge");
            return 0L;
        }
    }
}
