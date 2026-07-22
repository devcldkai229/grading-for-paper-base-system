using Contracts.Messages;
using ExamCatalogService.Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace ExamCatalogService.Infrastructure.Consumers;

/// <summary>
/// Runs the long-running barem ingestion off the HTTP request thread. When a subject's rubric file
/// changes (new version uploaded), this consumer recompiles the grading contract. Idempotent: the
/// upsert is keyed on (subject, rubric version), so a redelivery simply re-compiles the same version.
/// </summary>
public class RubricVersionChangedConsumer : IConsumer<RubricVersionChangedEvent>
{
    private readonly IGradingContractService _contractService;
    private readonly ILogger<RubricVersionChangedConsumer> _logger;

    public RubricVersionChangedConsumer(
        IGradingContractService contractService,
        ILogger<RubricVersionChangedConsumer> logger)
    {
        _contractService = contractService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<RubricVersionChangedEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation(
            "Recompiling grading contract for subject {SubjectId} v{Version}",
            msg.SubjectId, msg.RubricVersion);

        var ok = await _contractService.IngestAsync(msg.SubjectId, context.CancellationToken);
        if (!ok)
        {
            _logger.LogWarning(
                "Contract ingestion did not produce a contract for subject {SubjectId} v{Version}",
                msg.SubjectId, msg.RubricVersion);
        }
    }
}
