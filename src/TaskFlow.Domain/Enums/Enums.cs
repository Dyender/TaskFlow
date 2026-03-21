namespace TaskFlow.Domain.Enums;

public enum WorkspaceRole
{
    Viewer = 0,
    Member = 1,
    Admin = 2,
    Owner = 3
}

public enum BoardVisibility
{
    Workspace = 0,
    Private = 1
}

public enum TaskPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum NotificationType
{
    Assignment = 0,
    Comment = 1,
    DeadlineSoon = 2,
    CardMoved = 3,
    AddedToWorkspace = 4,
    AddedToBoard = 5
}

public enum ActivityType
{
    CardCreated = 0,
    CardUpdated = 1,
    CardMoved = 2,
    CardAssigned = 3,
    CardLabelsUpdated = 4,
    CommentAdded = 5,
    CommentUpdated = 6,
    CommentDeleted = 7,
    ChecklistItemAdded = 8,
    ChecklistItemUpdated = 9,
    ChecklistItemDeleted = 10,
    BoardCreated = 11,
    BoardUpdated = 12,
    ColumnCreated = 13,
    ColumnUpdated = 14,
    ColumnDeleted = 15,
    WorkspaceCreated = 16,
    MemberAdded = 17
}
