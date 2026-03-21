using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Notifications;

namespace TaskFlow.Application.Services;

public sealed class NotificationService(ITaskFlowRepository repository) : INotificationService
{
    public IReadOnlyCollection<NotificationResponse> GetForUser(Guid userId)
    {
        _ = repository.GetUserById(userId)
            ?? throw new NotFoundException("User not found.");

        return repository.GetNotificationsByUser(userId)
            .OrderByDescending(notification => notification.CreatedAtUtc)
            .Select(notification => notification.ToResponse())
            .ToArray();
    }

    public NotificationResponse MarkAsRead(Guid userId, Guid notificationId)
    {
        var notification = repository.GetNotification(notificationId)
            ?? throw new NotFoundException("Notification not found.");
        if (notification.UserId != userId)
        {
            throw new ForbiddenException("You can only modify your own notifications.");
        }

        notification.IsRead = true;
        repository.UpdateNotification(notification);
        repository.SaveChanges();

        return notification.ToResponse();
    }
}
