namespace OmniMind.Abstractions.SignalR
{
    /// <summary>
    /// SignalR 推送进度
    /// </summary>
    public interface IRealtimeNotifier
    {
        /// <summary>
        /// 通知文档处理进度
        /// </summary>
        Task NotifyDocumentProgressAsync(string userId, string documentId, DocumentProgress progress, CancellationToken cancellationToken = default);

        /// <summary>
        /// 通知聊天消息（用于流式响应）
        /// </summary>
        Task NotifyChatMessageAsync(string userId, string conversationId, SignalRChatMessage message, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 文档处理进度
    /// </summary>
    public class DocumentProgress
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// 文档标题
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 状态
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// 进度百分比 (0-100)
        /// </summary>
        public int Progress { get; set; }

        /// <summary>
        /// 当前阶段描述
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// 错误信息（如果有）
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// SignalR 聊天消息
    /// </summary>
    public class SignalRChatMessage
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string MessageId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 角色: user/assistant
        /// </summary>
        public string Role { get; set; } = string.Empty;

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    }
}
