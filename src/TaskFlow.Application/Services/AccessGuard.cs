using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Services;

internal static class AccessGuard
{
    public static WorkspaceMember RequireWorkspaceMember(Workspace workspace, Guid userId)
    {
        return workspace.Members.FirstOrDefault(member => member.UserId == userId)
            ?? throw new ForbiddenException("You are not a member of this workspace.");
    }

    public static WorkspaceRole EnsureWorkspaceContributor(Workspace workspace, Guid userId)
    {
        var membership = RequireWorkspaceMember(workspace, userId);
        if (membership.Role == WorkspaceRole.Viewer)
        {
            throw new ForbiddenException("Viewer role cannot create or update content.");
        }

        return membership.Role;
    }

    public static WorkspaceRole EnsureWorkspaceManager(Workspace workspace, Guid userId)
    {
        var membership = RequireWorkspaceMember(workspace, userId);
        if (!IsAdminLike(membership.Role))
        {
            throw new ForbiddenException("Only workspace admins or owners can manage this resource.");
        }

        return membership.Role;
    }

    public static WorkspaceRole EnsureCanViewBoard(Workspace workspace, Board board, Guid userId)
    {
        var workspaceMembership = RequireWorkspaceMember(workspace, userId);
        if (board.Visibility == BoardVisibility.Workspace)
        {
            return workspaceMembership.Role;
        }

        if (IsAdminLike(workspaceMembership.Role))
        {
            return workspaceMembership.Role;
        }

        var boardMembership = board.Members.FirstOrDefault(member => member.UserId == userId);
        if (boardMembership is null)
        {
            throw new ForbiddenException("You do not have access to this private board.");
        }

        return boardMembership.Role;
    }

    public static WorkspaceRole EnsureBoardContributor(Workspace workspace, Board board, Guid userId)
    {
        var role = EnsureCanViewBoard(workspace, board, userId);
        if (role == WorkspaceRole.Viewer)
        {
            throw new ForbiddenException("Viewer role cannot create or update board content.");
        }

        return role;
    }

    public static WorkspaceRole EnsureBoardManager(Workspace workspace, Board board, Guid userId)
    {
        var workspaceMembership = RequireWorkspaceMember(workspace, userId);
        if (IsAdminLike(workspaceMembership.Role))
        {
            return workspaceMembership.Role;
        }

        var boardMembership = board.Members.FirstOrDefault(member => member.UserId == userId);
        if (boardMembership is null || !IsAdminLike(boardMembership.Role))
        {
            throw new ForbiddenException("Only board admins or higher can manage this board.");
        }

        return boardMembership.Role;
    }

    public static void EnsureCanEditCard(Workspace workspace, Board board, TaskCard card, Guid userId)
    {
        var role = EnsureBoardContributor(workspace, board, userId);
        if (IsAdminLike(role))
        {
            return;
        }

        if (card.AuthorId != userId && card.AssigneeId != userId)
        {
            throw new ForbiddenException("Members can edit only their own or assigned cards.");
        }
    }

    public static void EnsureCanModerateComment(Workspace workspace, Board board, Comment comment, Guid userId)
    {
        var role = EnsureCanViewBoard(workspace, board, userId);
        if (comment.AuthorId != userId && !IsAdminLike(role))
        {
            throw new ForbiddenException("Only the author or admins can modify this comment.");
        }
    }

    public static bool IsWorkspaceMember(Workspace workspace, Guid userId) =>
        workspace.Members.Any(member => member.UserId == userId);

    public static bool HasBoardAccess(Workspace workspace, Board board, Guid userId)
    {
        try
        {
            EnsureCanViewBoard(workspace, board, userId);
            return true;
        }
        catch (ForbiddenException)
        {
            return false;
        }
    }

    public static bool IsAdminLike(WorkspaceRole role) => role is WorkspaceRole.Admin or WorkspaceRole.Owner;
}
