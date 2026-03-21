using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.DTOs.Workspaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Services;

public sealed class WorkspaceService(
    ITaskFlowRepository repository,
    TimeProvider timeProvider) : IWorkspaceService
{
    public IReadOnlyCollection<WorkspaceResponse> GetForUser(Guid userId)
    {
        EnsureUserExists(userId);

        return repository.GetWorkspacesForUser(userId)
            .OrderBy(workspace => workspace.Name)
            .Select(workspace => workspace.ToResponse(repository.GetBoardsByWorkspace(workspace.Id).Count))
            .ToArray();
    }

    public WorkspaceDetailsResponse GetById(Guid userId, Guid workspaceId)
    {
        var workspace = repository.GetWorkspace(workspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        AccessGuard.RequireWorkspaceMember(workspace, userId);
        return BuildWorkspaceDetails(workspace);
    }

    public WorkspaceDetailsResponse Create(Guid userId, CreateWorkspaceRequest request)
    {
        EnsureUserExists(userId);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Workspace name is required.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var workspace = new Workspace
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            OwnerId = userId,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Members =
            {
                new WorkspaceMember
                {
                    UserId = userId,
                    Role = WorkspaceRole.Owner,
                    JoinedAtUtc = utcNow
                }
            }
        };

        repository.AddWorkspace(workspace);
        repository.SaveChanges();

        return BuildWorkspaceDetails(workspace);
    }

    public WorkspaceDetailsResponse AddMember(Guid userId, Guid workspaceId, AddWorkspaceMemberRequest request)
    {
        var workspace = repository.GetWorkspace(workspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        AccessGuard.EnsureWorkspaceManager(workspace, userId);
        _ = repository.GetUserById(request.UserId)
            ?? throw new NotFoundException("User to add was not found.");

        if (workspace.Members.FirstOrDefault(member => member.UserId == request.UserId) is { } existingMembership)
        {
            existingMembership.Role = request.Role;
        }
        else
        {
            var utcNow = timeProvider.GetUtcNow().UtcDateTime;
            workspace.Members.Add(new WorkspaceMember
            {
                UserId = request.UserId,
                Role = request.Role,
                JoinedAtUtc = utcNow
            });

            repository.AddNotification(new Notification
            {
                UserId = request.UserId,
                Type = NotificationType.AddedToWorkspace,
                Message = $"You were added to workspace '{workspace.Name}' as {request.Role}.",
                RelatedEntityId = workspace.Id,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            });
        }

        workspace.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateWorkspace(workspace);
        repository.SaveChanges();

        return BuildWorkspaceDetails(workspace);
    }

    public IReadOnlyCollection<LabelResponse> GetLabels(Guid userId, Guid workspaceId)
    {
        var workspace = repository.GetWorkspace(workspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        AccessGuard.RequireWorkspaceMember(workspace, userId);

        return repository.GetLabelsByWorkspace(workspaceId)
            .OrderBy(label => label.Name)
            .Select(label => label.ToResponse())
            .ToArray();
    }

    public LabelResponse CreateLabel(Guid userId, Guid workspaceId, CreateLabelRequest request)
    {
        var workspace = repository.GetWorkspace(workspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        AccessGuard.EnsureWorkspaceContributor(workspace, userId);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Label name is required.");
        }

        if (repository.GetLabelsByWorkspace(workspaceId).Any(label =>
                string.Equals(label.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException("A label with this name already exists in the workspace.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var label = new Label
        {
            WorkspaceId = workspaceId,
            Name = request.Name.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#F59E0B" : request.Color.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        repository.AddLabel(label);
        repository.SaveChanges();
        return label.ToResponse();
    }

    private WorkspaceDetailsResponse BuildWorkspaceDetails(Workspace workspace)
    {
        var boards = repository.GetBoardsByWorkspace(workspace.Id)
            .OrderBy(board => board.Name)
            .Select(board => board.ToResponse())
            .ToArray();

        return new WorkspaceDetailsResponse(
            workspace.Id,
            workspace.Name,
            workspace.Description,
            workspace.OwnerId,
            workspace.CreatedAtUtc,
            workspace.UpdatedAtUtc,
            workspace.Members
                .OrderByDescending(member => member.Role)
                .ThenBy(member => repository.GetUserById(member.UserId)?.Name)
                .Select(member => member.ToResponse(repository.GetUserById))
                .ToArray(),
            boards);
    }

    private void EnsureUserExists(Guid userId)
    {
        _ = repository.GetUserById(userId)
            ?? throw new NotFoundException("User not found.");
    }
}
