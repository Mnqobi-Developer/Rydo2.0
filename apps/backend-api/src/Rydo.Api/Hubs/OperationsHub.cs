using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Rydo.Domain.Identity;

namespace Rydo.Api.Hubs;

[Authorize(Roles = "passenger,driver,admin")]
public sealed class OperationsHub : Hub<IOperationsClient>
{
    public override async Task OnConnectedAsync()
    {
        var subject = Context.User?.FindFirstValue("sub");
        var roleValue = Context.User?.FindFirstValue("role");

        if (!Guid.TryParse(subject, out var userId) ||
            !Enum.TryParse<UserRole>(roleValue, true, out var role) ||
            role is not UserRole.Passenger and not UserRole.Driver and not UserRole.Admin)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.User(userId),
            Context.ConnectionAborted);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            RealtimeGroups.Role(role),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }
}
