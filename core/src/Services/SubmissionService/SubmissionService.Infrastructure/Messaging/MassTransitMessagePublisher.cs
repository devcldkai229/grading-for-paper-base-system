using MassTransit;
using SubmissionService.Application.Interfaces;

namespace SubmissionService.Infrastructure.Messaging;

public class MassTransitMessagePublisher : IMessagePublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitMessagePublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class =>
        _publishEndpoint.Publish(message, ct);
}
