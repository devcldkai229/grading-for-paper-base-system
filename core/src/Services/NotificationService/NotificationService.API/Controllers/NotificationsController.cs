using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;

namespace NotificationService.API.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationQueryService _notifications;

    public NotificationsController(INotificationQueryService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _notifications.GetMyNotificationsAsync(GetUserId(), page, pageSize, ct);

        return Ok(new ApiResponse<NotificationPageDto>
        {
            StatusCode = 200,
            Message = "Notifications retrieved",
            Data = result,
            ResponsedAt = DateTime.UtcNow
        });
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct = default)
    {
        var updated = await _notifications.MarkAsReadAsync(id, GetUserId(), ct);

        if (!updated)
        {
            return NotFound(new ApiResponse<object>
            {
                StatusCode = 404,
                Message = "Notification not found",
                Data = null,
                ResponsedAt = DateTime.UtcNow
            });
        }

        return Ok(new ApiResponse<object>
        {
            StatusCode = 200,
            Message = "Notification marked as read",
            Data = null,
            ResponsedAt = DateTime.UtcNow
        });
    }

    private Guid GetUserId()
    {
        var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("User ID not found in claims");
        return Guid.Parse(raw);
    }
}
