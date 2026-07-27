using Contracts.Domain;

namespace GradingService.Domain.Entities;

public class AuditLog : Entity
{
    public Guid UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Reason { get; set; }
}
