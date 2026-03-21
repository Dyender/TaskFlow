namespace TaskFlow.Api.Background;

public sealed class DeadlineReminderSettings
{
    public const string SectionName = "DeadlineReminders";

    public int ScanIntervalSeconds { get; init; } = 60;
    public int DueSoonWindowMinutes { get; init; } = 180;
}
