using Contracts.Messages;
using GradingService.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace GradingService.Infrastructure.Consumers;

/// <summary>
/// Keeps the local lecturer_marker_code_view projection in sync with IamService. Upsert is keyed on the
/// source user id, so reprocessing the same event (redelivery) is a no-op — idempotent by design (N8).
/// This removes the synchronous bulk pull from IamService that grade export used to depend on (N5).
/// </summary>
public class LecturerProfileChangedConsumer : IConsumer<LecturerProfileChanged>
{
    private readonly ILecturerMarkerCodeRepository _repository;
    private readonly ILogger<LecturerProfileChangedConsumer> _logger;

    public LecturerProfileChangedConsumer(
        ILecturerMarkerCodeRepository repository,
        ILogger<LecturerProfileChangedConsumer> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LecturerProfileChanged> context)
    {
        var msg = context.Message;

        await _repository.UpsertAsync(
            msg.UserId, msg.MarkerCode, msg.FullName, msg.OccurredAt, context.CancellationToken);

        _logger.LogInformation(
            "LecturerMarkerCodeView upserted: user={UserId} markerCode={MarkerCode}",
            msg.UserId, msg.MarkerCode);
    }
}
