using TaskFlow.Domain.Common;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Domain.Entities;

public sealed class TaskCard : Entity
{
    public Guid BoardId { get; set; }
    public Guid ColumnId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DeadlineUtc { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid AuthorId { get; set; }
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public List<Guid> LabelIds { get; set; } = [];
    public List<ChecklistItem> ChecklistItems { get; set; } = [];
}

public sealed class ChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Position { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
