using Microsoft.AspNetCore.SignalR;
using OmniMind.Abstractions.SignalR;

namespace OmniMind.Realtime.SignalR
{
    /// <summary>
    /// SignalR 实时通知实现
    /// </summary>
    public class SignalRRealtimeNotifier : IRealtimeNotifier
    {
        private readonly IHubContext<IngestionHub> hubContext;

        public SignalRRealtimeNotifier(IHubContext<IngestionHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        /// <summary>
        /// 通知文档处理进度
        /// </summary>
        public async Task NotifyDocumentProgressAsync(string userId, string documentId, DocumentProgress progress, CancellationToken cancellationToken = default)
        {
            // 发送给特定用户（使用用户组）
            // 注意：这里使用 tenantId 作为 userId，实际使用中可能需要从上下文获取真实 userId
            await hubContext.Clients.Group($"user_{userId}")
                .SendAsync("DocumentProgress", progress, cancellationToken);
        }

        /// <summary>
        /// 通知聊天消息（用于流式响应）
        /// </summary>
        public async Task NotifyChatMessageAsync(string userId, string conversationId, SignalRChatMessage message, CancellationToken cancellationToken = default)
        {
            // 发送给特定用户
            await hubContext.Clients.Group($"user_{userId}")
                .SendAsync("ChatMessage", new { conversationId, message }, cancellationToken);
        }
    }
}
