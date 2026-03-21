using Microsoft.AspNetCore.SignalR;
using TaskFlow.Api.Hubs;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.Realtime;

public sealed class SignalRBoardRealtimeNotifier(IHubContext<BoardHub> hubContext) : IBoardRealtimeNotifier
{
    public Task PublishBoardEventAsync(Guid boardId, string eventName, object payload, CancellationToken cancellationToken = default) =>
        hubContext.Clients.Group(BoardHub.GroupName(boardId)).SendAsync(eventName, payload, cancellationToken);
}
