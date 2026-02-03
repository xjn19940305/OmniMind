using Microsoft.AspNetCore.SignalR;

namespace OmniMind.Realtime.SignalR
{
    /// <summary>
    /// SignalR Hub 用于实时通信
    /// </summary>
    public class IngestionHub : Hub
    {
        /// <summary>
        /// 客户端连接时调用
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"];

            // 将用户加入个人组（使用 userId 作为组名）
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// 客户端断开连接时调用
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.GetHttpContext()?.Request.Query["userId"];

            // 从用户组中移除
            if (!string.IsNullOrEmpty(userId))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
