using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Notifications;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<NotificationResponse>> GetMine() =>
        Ok(notificationService.GetForUser(CurrentUserId));

    [HttpPut("{id:guid}/read")]
    public ActionResult<NotificationResponse> MarkAsRead(Guid id) =>
        Ok(notificationService.MarkAsRead(CurrentUserId, id));
}
