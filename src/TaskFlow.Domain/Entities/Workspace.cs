using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public sealed class Workspace : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public List<WorkspaceMember> Members { get; set; } = [];
}

public sealed class WorkspaceMember
{
    public Guid UserId { get; set; }
    public WorkspaceRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
