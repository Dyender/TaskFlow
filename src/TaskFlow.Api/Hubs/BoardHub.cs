using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace TaskFlow.Api.Hubs;

[Authorize]
public sealed class BoardHub : Hub
{
    public Task JoinBoard(Guid boardId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));

    public Task LeaveBoard(Guid boardId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));

    public static string GroupName(Guid boardId) => $"board:{boardId}";
}
