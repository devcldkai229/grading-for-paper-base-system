using Contracts.Domain;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class NotificationTemplate : Entity
{
    public NotificationType Type { get; set; }

    public NotificationChannel Channel { get; set; }

    public string? Subject { get; set; }

    public string BodyTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
