using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Nucleus.Api.Hubs;

[Authorize]
public class ProvisioningHub : Hub
{
    // Clients subscribe to a brand-specific group to receive live step updates
    public async Task Subscribe(string brandId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"brand-{brandId}");
    }

    public async Task Unsubscribe(string brandId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"brand-{brandId}");
    }
}
