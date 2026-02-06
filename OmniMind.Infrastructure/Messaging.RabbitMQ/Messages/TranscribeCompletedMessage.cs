namespace OmniMind.Messages
{
    /// <summary>
    /// 音视频转写完成消息
    /// Python 服务完成转写后将结果发送到此队列
    /// </summary>
    public record TranscribeCompletedMessage
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; init; } = string.Empty;

        /// <summary>
        /// 转写结果文本的 OSS 路径
        /// </summary>
        public string TranscribedTextObjectKey { get; init; } = string.Empty;

        /// <summary>
        /// 转写服务（用于标记来源）
        /// </summary>
        public string? Provider { get; init; }

        /// <summary>
        /// 转写耗时（毫秒）
        /// </summary>
        public long? DurationMs { get; init; }

        /// <summary>
        /// 转写状态
        /// </summary>
        public TranscribeStatus Status { get; init; } = TranscribeStatus.Success;

        /// <summary>
        /// 错误信息（失败时）
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// 消息创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// 转写状态
    /// </summary>
    public enum TranscribeStatus
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success = 0,

        /// <summary>
        /// 失败
        /// </summary>
        Failed = 1,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout = 2
    }
}
