namespace IamService.Application.Interfaces;

/// <summary>
/// Publishes integration events. Backed by the MassTransit bus outbox, so a published message is only
/// dispatched after the DbContext's SaveChanges commits — call it BEFORE the save that persists the
/// related state change to keep both atomic (N7).
/// </summary>
public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class;
}
