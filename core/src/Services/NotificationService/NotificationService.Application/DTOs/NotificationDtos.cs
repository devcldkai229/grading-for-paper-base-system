namespace NotificationService.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    bool IsRead,
    string Status,
    DateTime? SentAt,
    DateTime CreatedAt,
    Guid? AssignmentId,
    Guid? BatchId = null
);

public record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages
);
