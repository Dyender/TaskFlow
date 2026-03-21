using TaskFlow.Application.DTOs.Columns;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.DTOs.Users;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Boards;

public sealed record CreateBoardRequest(
    string Name,
    string Description,
    string? Color,
    BoardVisibility Visibility,
    bool CreateDefaultColumns = true);

public sealed record UpdateBoardRequest(
    string? Name,
    string? Description,
    string? Color,
    BoardVisibility? Visibility,
    bool? IsArchived);

public sealed record AddBoardMemberRequest(Guid UserId, WorkspaceRole Role);

public sealed record BoardMemberResponse(Guid UserId, string Name, string Email, string? AvatarUrl, WorkspaceRole Role, DateTime JoinedAtUtc);

public sealed record BoardResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Description,
    string Color,
    bool IsArchived,
    BoardVisibility Visibility,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record BoardDetailsResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Description,
    string Color,
    bool IsArchived,
    BoardVisibility Visibility,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<BoardMemberResponse> Members,
    IReadOnlyCollection<ColumnWithCardsResponse> Columns,
    IReadOnlyCollection<LabelResponse> Labels);

public sealed record BoardCardsQuery(
    string? Search,
    Guid? AssigneeId,
    TaskPriority? Priority,
    Guid[]? LabelIds,
    DateTime? DueBeforeUtc,
    bool OnlyOverdue = false,
    bool IncludeArchived = false);
