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
    }
}
