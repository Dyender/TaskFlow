using TaskFlow.Application.DTOs.Comments;
using TaskFlow.Application.DTOs.Users;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Cards;

public sealed record CreateCardRequest(
    string Title,
    string Description,
    TaskPriority Priority,
    DateTime? DeadlineUtc,
    Guid? AssigneeId,
    int? Position);

public sealed record UpdateCardRequest(
    string? Title,
    string? Description,
    TaskPriority? Priority,
    DateTime? DeadlineUtc,
    bool ClearDeadline = false,
    bool? IsArchived = null);

public sealed record MoveCardRequest(Guid TargetColumnId, int TargetPosition);

public sealed record AssignCardRequest(Guid? AssigneeId);

public sealed record UpdateCardLabelsRequest(IReadOnlyCollection<Guid> LabelIds);

public sealed record AddChecklistItemRequest(string Title);

public sealed record UpdateChecklistItemRequest(string? Title, bool? IsCompleted);

public sealed record ChecklistItemResponse(Guid Id, string Title, bool IsCompleted, int Position, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record CardResponse(
    Guid Id,
    Guid BoardId,
    Guid ColumnId,
    string Title,
    string Description,
    TaskPriority Priority,
    DateTime? DeadlineUtc,
    UserSummaryResponse? Assignee,
    UserSummaryResponse Author,
    int Position,
    bool IsArchived,
    IReadOnlyCollection<Guid> LabelIds,
    int ChecklistCompletedCount,
    int ChecklistTotalCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CardDetailsResponse(
    Guid Id,
    Guid BoardId,
    Guid ColumnId,
    string Title,
    string Description,
    TaskPriority Priority,
    DateTime? DeadlineUtc,
    UserSummaryResponse? Assignee,
    UserSummaryResponse Author,
    int Position,
    bool IsArchived,
    IReadOnlyCollection<Guid> LabelIds,
    IReadOnlyCollection<ChecklistItemResponse> ChecklistItems,
    IReadOnlyCollection<CommentResponse> Comments,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
