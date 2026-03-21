using TaskFlow.Application.DTOs.Boards;
using TaskFlow.Application.DTOs.Users;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Workspaces;

public sealed record CreateWorkspaceRequest(string Name, string Description);

public sealed record AddWorkspaceMemberRequest(Guid UserId, WorkspaceRole Role);

public sealed record WorkspaceMemberResponse(Guid UserId, string Name, string Email, string? AvatarUrl, WorkspaceRole Role, DateTime JoinedAtUtc);

public sealed record WorkspaceResponse(Guid Id, string Name, string Description, Guid OwnerId, DateTime CreatedAtUtc, int BoardCount);

public sealed record WorkspaceDetailsResponse(
    Guid Id,
    string Name,
    string Description,
    Guid OwnerId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<WorkspaceMemberResponse> Members,
    IReadOnlyCollection<BoardResponse> Boards);
