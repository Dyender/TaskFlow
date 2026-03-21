using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public sealed class Board : Entity
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Color { get; set; } = "#2563EB";
    public bool IsArchived { get; set; }
    public BoardVisibility Visibility { get; set; } = BoardVisibility.Workspace;
    public List<BoardMember> Members { get; set; } = [];
}

public sealed class BoardMember
{
    public Guid UserId { get; set; }
    public WorkspaceRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BoardColumn : Entity
{
    public Guid BoardId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Position { get; set; }
    public bool IsArchived { get; set; }
}
