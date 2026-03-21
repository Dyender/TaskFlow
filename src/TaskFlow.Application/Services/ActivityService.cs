using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Activity;

namespace TaskFlow.Application.Services;

public sealed class ActivityService(ITaskFlowRepository repository) : IActivityService
{
    public IReadOnlyCollection<ActivityResponse> GetCardActivity(Guid userId, Guid cardId)
    {
        var card = repository.GetCard(cardId)
            ?? throw new NotFoundException("Card not found.");
        var board = repository.GetBoard(card.BoardId)
            ?? throw new NotFoundException("Board not found.");
        var workspace = repository.GetWorkspace(board.WorkspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        AccessGuard.EnsureCanViewBoard(workspace, board, userId);

        return repository.GetActivitiesByCard(cardId)
            .OrderByDescending(activity => activity.CreatedAtUtc)
            .Select(activity => activity.ToResponse())
            .ToArray();
    }
}
