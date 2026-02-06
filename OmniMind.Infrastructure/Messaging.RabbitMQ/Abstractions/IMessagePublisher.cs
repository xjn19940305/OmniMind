namespace OmniMind.Messaging.Abstractions
{
    /// <summary>
    /// 消息发布者接口
    /// </summary>
    public interface IMessagePublisher
    {
        /// <summary>
        /// 发布消息到指定队列
        /// </summary>
        Task PublishAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class;

        /// <summary>
        /// 发布文档上传消息
        /// </summary>
        Task PublishDocumentUploadAsync(Messages.DocumentUploadMessage message, CancellationToken ct = default);

        /// <summary>
        /// 发布音视频转写请求消息
        /// </summary>
        Task PublishTranscribeRequestAsync(Messages.TranscribeRequestMessage message, CancellationToken ct = default);

        /// <summary>
        /// 发布转写完成消息（用于重新入队）
        /// </summary>
        Task PublishTranscribeCompletedAsync(Messages.TranscribeCompletedMessage message, CancellationToken ct = default);
    }
}
