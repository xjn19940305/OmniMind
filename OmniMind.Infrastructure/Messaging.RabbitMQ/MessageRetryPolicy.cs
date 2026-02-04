using Microsoft.Extensions.Logging;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// 消息处理重试策略
    /// </summary>
    public class MessageRetryPolicy
    {
        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 是否启用指数退避
        /// </summary>
        public bool UseExponentialBackoff { get; set; } = true;

        /// <summary>
        /// 基础延迟时间（秒）
        /// </summary>
        public int BaseDelaySeconds { get; set; } = 2;

        /// <summary>
        /// 执行带重试的消息处理
        /// </summary>
        public async Task<RetryResult> ExecuteAsync(
            string documentId,
            int currentRetryCount,
            Func<Task> processAction,
            Func<Task> republishAction,
            ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            // 检查是否已超过最大重试次数
            if (currentRetryCount >= MaxRetryCount)
            {
                logger?.LogError("文档处理已达到最大重试次数 DocumentId={DocumentId}, RetryCount={RetryCount}",
                    documentId, currentRetryCount);
                return RetryResult.MaxRetriesExceeded;
            }

            try
            {
                logger?.LogInformation("开始处理文档 DocumentId={DocumentId}, RetryCount={RetryCount}",
                    documentId, currentRetryCount + 1);

                // 执行处理逻辑
                await processAction();

                logger?.LogInformation("文档处理完成 DocumentId={DocumentId}", documentId);
                return RetryResult.Success;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "文档处理失败 DocumentId={DocumentId}, RetryCount={RetryCount}/{MaxRetryCount}",
                    documentId, currentRetryCount + 1, MaxRetryCount);

                // 还可以重试
                if (currentRetryCount + 1 < MaxRetryCount)
                {
                    var delaySeconds = UseExponentialBackoff
                        ? (int)Math.Pow(BaseDelaySeconds, currentRetryCount + 1)
                        : BaseDelaySeconds;

                    logger?.LogInformation("文档将在 {DelaySeconds} 秒后重试 DocumentId={DocumentId}",
                        delaySeconds, documentId);

                    // 延迟后重新发布消息
                    await Task.Delay(delaySeconds * 1000, cancellationToken);
                    await republishAction();

                    return RetryResult.WillRetry;
                }

                // 达到最大重试次数
                return RetryResult.Failed;
            }
        }
    }

    /// <summary>
    /// 重试结果
    /// </summary>
    public enum RetryResult
    {
        /// <summary>
        /// 成功
        /// </summary>
        Success,

        /// <summary>
        /// 将重试
        /// </summary>
        WillRetry,

        /// <summary>
        /// 失败（已达到最大重试次数）
        /// </summary>
        Failed,

        /// <summary>
        /// 已超过最大重试次数
        /// </summary>
        MaxRetriesExceeded
    }
}
