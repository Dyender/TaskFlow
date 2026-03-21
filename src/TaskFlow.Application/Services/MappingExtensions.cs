using TaskFlow.Application.DTOs.Activity;
using TaskFlow.Application.DTOs.Boards;
using TaskFlow.Application.DTOs.Cards;
using TaskFlow.Application.DTOs.Columns;
using TaskFlow.Application.DTOs.Comments;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.DTOs.Notifications;
using TaskFlow.Application.DTOs.Users;
using TaskFlow.Application.DTOs.Workspaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;

internal static class MappingExtensions
{
    public static UserSummaryResponse ToSummary(this User user) =>
        new(user.Id, user.Name, user.Email, user.AvatarUrl);

    public static UserProfileResponse ToProfileResponse(this User user) =>
        new(user.Id, user.Name, user.Email, user.AvatarUrl, user.CreatedAtUtc, user.UpdatedAtUtc);

    public static WorkspaceResponse ToResponse(this Workspace workspace, int boardCount) =>
        new(workspace.Id, workspace.Name, workspace.Description, workspace.OwnerId, workspace.CreatedAtUtc, boardCount);

    public static WorkspaceMemberResponse ToResponse(this WorkspaceMember member, Func<Guid, User?> userResolver)
    {
        var user = userResolver(member.UserId);
        return new WorkspaceMemberResponse(
            member.UserId,
            user?.Name ?? "Unknown",
            user?.Email ?? string.Empty,
            user?.AvatarUrl,
            member.Role,
            member.JoinedAtUtc);
    }

    public static BoardResponse ToResponse(this Board board) =>
        new(board.Id, board.WorkspaceId, board.Name, board.Description, board.Color, board.IsArchived, board.Visibility, board.CreatedAtUtc, board.UpdatedAtUtc);

    public static BoardMemberResponse ToResponse(this BoardMember member, Func<Guid, User?> userResolver)
    {
        var user = userResolver(member.UserId);
        return new BoardMemberResponse(
            member.UserId,
            user?.Name ?? "Unknown",
            user?.Email ?? string.Empty,
            user?.AvatarUrl,
            member.Role,
            member.JoinedAtUtc);
    }

    public static ColumnResponse ToResponse(this BoardColumn column) =>
        new(column.Id, column.BoardId, column.Title, column.Position, column.IsArchived, column.CreatedAtUtc, column.UpdatedAtUtc);

    public static LabelResponse ToResponse(this Label label) =>
        new(label.Id, label.WorkspaceId, label.Name, label.Color, label.CreatedAtUtc);

    public static ChecklistItemResponse ToResponse(this ChecklistItem item) =>
        new(item.Id, item.Title, item.IsCompleted, item.Position, item.CreatedAtUtc, item.UpdatedAtUtc);

    public static CardResponse ToResponse(this TaskCard card, Func<Guid, User?> userResolver)
    {
        var author = userResolver(card.AuthorId)?.ToSummary()
            ?? new UserSummaryResponse(card.AuthorId, "Unknown", string.Empty, null);
        var assignee = card.AssigneeId is null
            ? null
            : userResolver(card.AssigneeId.Value)?.ToSummary() ?? new UserSummaryResponse(card.AssigneeId.Value, "Unknown", string.Empty, null);

        return new CardResponse(
            card.Id,
            card.BoardId,
            card.ColumnId,
            card.Title,
            card.Description,
            card.Priority,
            card.DeadlineUtc,
            assignee,
            author,
            card.Position,
            card.IsArchived,
            card.LabelIds.ToArray(),
            card.ChecklistItems.Count(item => item.IsCompleted),
            card.ChecklistItems.Count,
            card.CreatedAtUtc,
            card.UpdatedAtUtc);
    }

    public static CardDetailsResponse ToDetailsResponse(this TaskCard card, IReadOnlyCollection<Comment> comments, Func<Guid, User?> userResolver)
    {
        var author = userResolver(card.AuthorId)?.ToSummary()
            ?? new UserSummaryResponse(card.AuthorId, "Unknown", string.Empty, null);
        var assignee = card.AssigneeId is null
            ? null
            : userResolver(card.AssigneeId.Value)?.ToSummary() ?? new UserSummaryResponse(card.AssigneeId.Value, "Unknown", string.Empty, null);

        return new CardDetailsResponse(
            card.Id,
            card.BoardId,
            card.ColumnId,
            card.Title,
            card.Description,
            card.Priority,
            card.DeadlineUtc,
            assignee,
            author,
            card.Position,
            card.IsArchived,
            card.LabelIds.ToArray(),
            card.ChecklistItems
                .OrderBy(item => item.Position)
                .Select(item => item.ToResponse())
                .ToArray(),
            comments
                .OrderBy(comment => comment.CreatedAtUtc)
                .Select(comment => comment.ToResponse(userResolver))
                .ToArray(),
            card.CreatedAtUtc,
            card.UpdatedAtUtc);
    }

    public static CommentResponse ToResponse(this Comment comment, Func<Guid, User?> userResolver)
    {
        var author = userResolver(comment.AuthorId)?.ToSummary()
            ?? new UserSummaryResponse(comment.AuthorId, "Unknown", string.Empty, null);

        return new CommentResponse(comment.Id, comment.CardId, author, comment.Content, comment.CreatedAtUtc, comment.UpdatedAtUtc);
    }

    public static NotificationResponse ToResponse(this Notification notification) =>
        new(notification.Id, notification.Type, notification.Message, notification.RelatedEntityId, notification.IsRead, notification.CreatedAtUtc);

    public static ActivityResponse ToResponse(this ActivityLog activity) =>
        new(activity.Id, activity.ActorId, activity.Type, activity.Description, activity.CreatedAtUtc);
}
