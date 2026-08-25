using LifeLinkLanka.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LifeLinkLanka.API.Hubs;

[Authorize]
public class EmergencyHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User!.IsInRole(Roles.Donor))
            await Groups.AddToGroupAsync(Context.ConnectionId, "Donors");

        await base.OnConnectedAsync();
    }
}