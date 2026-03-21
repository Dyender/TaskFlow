using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Notifications;

public sealed record NotificationResponse(Guid Id, NotificationType Type, string Message, Guid? RelatedEntityId, bool IsRead, DateTime CreatedAtUtc);
