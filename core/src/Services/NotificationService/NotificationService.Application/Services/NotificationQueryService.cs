using System.Text.Json;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services;

public class NotificationQueryService : INotificationQueryService
{
    private readonly INotificationRepository _notifications;

    public NotificationQueryService(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    public async Task<NotificationPageDto> GetMyNotificationsAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, totalCount) = await _notifications.ListByUserAsync(userId, page, pageSize, ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new NotificationPageDto(
            items.Select(ToDto).ToList(), page, pageSize, totalCount, totalPages);
    }

    public Task<bool> MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default) =>
        _notifications.MarkAsReadAsync(notificationId, userId, ct);

    /// <summary>
    /// RegradeRequest is the only notification type that currently links back to something the
    /// lecturer can act on (the grading assignment being re-graded) — its raw event is stored in
    /// Metadata, so the AssignmentId is extracted from there rather than adding a dedicated column.
    /// </summary>
    private static NotificationDto ToDto(Notification n)
    {
        Guid? assignmentId = null;
        Guid? batchId = null;
        if (!string.IsNullOrEmpty(n.Metadata))
        {
            try
            {
                using var doc = JsonDocument.Parse(n.Metadata);
                if (n.Type == NotificationType.RegradeRequest
                    && doc.RootElement.TryGetProperty("AssignmentId", out var assignmentProp)
                    && assignmentProp.TryGetGuid(out var parsedAssignment))
                {
                    assignmentId = parsedAssignment;
                }

                if (n.Type == NotificationType.Assignment
                    && doc.RootElement.TryGetProperty("BatchId", out var batchProp)
                    && batchProp.ValueKind == JsonValueKind.String
                    && batchProp.TryGetGuid(out var parsedBatch))
                {
                    batchId = parsedBatch;
                }
            }
            catch (JsonException)
            {
                // Malformed/legacy metadata — surface the notification without a link rather than fail the whole list.
            }
        }

        return new NotificationDto(
            n.Id, n.Type.ToString(), n.Title, n.Body, n.IsRead, n.Status.ToString(),
            n.SentAt, n.CreatedAt, assignmentId, batchId);
    }
}
