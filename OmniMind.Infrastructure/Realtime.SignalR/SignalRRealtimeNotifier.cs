using Microsoft.AspNetCore.SignalR;
using OmniMind.Abstractions.SignalR;

namespace OmniMind.Realtime.SignalR
{
    public class SignalRRealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<IngestionHub> hubContext;

        public SignalRRealtimeNotifier(IHubContext<IngestionHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        public Task NotifyDocumentProgressAsync(string userId, string documentId, DocumentProgress progress, CancellationToken cancellationToken = default)
        {
            return hubContext.Clients
                .Group(IngestionHub.GetUserGroup(userId))
                .SendAsync("DocumentProgress", progress, cancellationToken);
        }

        public Task NotifyChatMessageAsync(string userId, string conversationId, SignalRChatMessage message, CancellationToken cancellationToken = default)
        {
            return hubContext.Clients
                .Group(IngestionHub.GetUserGroup(userId))
                .SendAsync("ChatMessage", new { conversationId, message }, cancellationToken);
        }
    }
}
