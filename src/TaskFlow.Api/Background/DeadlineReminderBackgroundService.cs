using Microsoft.Extensions.Hosting;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Api.Background;

public sealed class DeadlineReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    DeadlineReminderSettings settings,
    TimeProvider timeProvider,
    ILogger<DeadlineReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ScanDeadlines();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to scan card deadlines for reminders.");
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.ScanIntervalSeconds), stoppingToken);
        }
    }

    private void ScanDeadlines()
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITaskFlowRepository>();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var upperBound = utcNow.AddMinutes(settings.DueSoonWindowMinutes);

        foreach (var card in repository.GetAllCards().Where(ShouldNotify))
        {
            if (!card.AssigneeId.HasValue)
            {
                continue;
            }

            var alreadyExists = repository.GetNotificationsByUser(card.AssigneeId.Value).Any(notification =>
                notification.Type == NotificationType.DeadlineSoon &&
                notification.RelatedEntityId == card.Id &&
                notification.CreatedAtUtc > utcNow.AddHours(-12));

            if (alreadyExists)
            {
                continue;
            }

            repository.AddNotification(new Notification
            {
                UserId = card.AssigneeId.Value,
                Type = NotificationType.DeadlineSoon,
                Message = $"Deadline for '{card.Title}' is coming soon ({card.DeadlineUtc:O}).",
                RelatedEntityId = card.Id,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            });
        }

        repository.SaveChanges();
        return;

        bool ShouldNotify(TaskCard card) =>
            !card.IsArchived &&
            card.AssigneeId.HasValue &&
            card.DeadlineUtc.HasValue &&
            card.DeadlineUtc.Value >= utcNow &&
            card.DeadlineUtc.Value <= upperBound;
    }
}
