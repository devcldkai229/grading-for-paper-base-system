using GradingService.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GradingService.Infrastructure.Jobs;

/// <summary>
/// Periodically sweeps for lecturers with ungraded papers whose subject deadline is approaching
/// and publishes a DeadlineReminderEvent for NotificationService to consume. Runs once at startup,
/// then on a fixed interval; a failed tick is logged and retried on the next tick rather than
/// crashing the host.
/// </summary>
public class DeadlineReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeadlineReminderBackgroundService> _logger;
    private readonly DeadlineReminderOptions _options;

    public DeadlineReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeadlineReminderBackgroundService> logger,
        IOptions<DeadlineReminderOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sessionService = scope.ServiceProvider.GetRequiredService<IGradingSessionService>();
                var published = await sessionService.RunDeadlineReminderSweepAsync(
                    _options.ReminderWindowDays, stoppingToken);

                if (published > 0)
                {
                    _logger.LogInformation("Deadline reminder sweep published {Count} reminder(s).", published);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deadline reminder sweep failed; will retry on the next tick.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
