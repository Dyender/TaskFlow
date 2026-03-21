using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public sealed class Comment : Entity
{
    public Guid CardId { get; set; }
    public Guid AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public sealed class Label : Entity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#F59E0B";
}

public sealed class Notification : Entity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public bool IsRead { get; set; }
}

public sealed class ActivityLog : Entity
{
    public Guid WorkspaceId { get; set; }
    public Guid? BoardId { get; set; }
    public Guid? CardId { get; set; }
    public Guid ActorId { get; set; }
    public ActivityType Type { get; set; }
    public string Description { get; set; } = string.Empty;
}
