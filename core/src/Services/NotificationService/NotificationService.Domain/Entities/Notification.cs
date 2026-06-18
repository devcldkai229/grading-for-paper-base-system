using Contracts.Domain;
using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

public class Notification : Entity
{
    public Guid UserId { get; set; }

    public NotificationType Type { get; set; }

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public string? Metadata { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public bool IsRead { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
