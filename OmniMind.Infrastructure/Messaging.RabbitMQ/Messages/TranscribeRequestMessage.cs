namespace OmniMind.Messages
{
    /// <summary>
    /// 音视频转写请求消息
    /// Python 服务从队列获取此消息，根据 ObjectKey 下载文件进行转写
    /// </summary>
    public record TranscribeRequestMessage
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string? KnowledgeBaseId { get; init; }

        /// <summary>
        /// 会话ID（临时文件用）
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// 对象存储Key（用于下载原文件）
        /// </summary>
        public string ObjectKey { get; init; } = string.Empty;

        /// <summary>
        /// 原始文件名
        /// </summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>
        /// 内容类型（MIME 类型）
        /// </summary>
        public string ContentType { get; init; } = string.Empty;

        /// <summary>
        /// 用户ID（用于通知进度）
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// 消息创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
