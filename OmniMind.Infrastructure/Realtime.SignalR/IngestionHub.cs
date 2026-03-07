using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace OmniMind.Realtime.SignalR
{
    [Authorize]
    public class IngestionHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirstValue("sub")
                ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirstValue("sub")
                ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroup(userId));
            }

            await base.OnDisconnectedAsync(exception);
        }

        public static string GetUserGroup(string userId) => $"user_{userId}";
    }
}
