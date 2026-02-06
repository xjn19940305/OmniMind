using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Abstractions.Storage;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.PostgreSql;

namespace OmniMind.Messaging.RabbitMQ.Consumers
{
    /// <summary>
    /// 转写完成消费者
    /// 处理 Python 服务转写完成后的文档，继续执行切片和向量化
    /// </summary>
    public class TranscribeCompletedConsumer : RabbitMQMessageConsumer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MessageRetryPolicy retryPolicy = new();

        public TranscribeCompletedConsumer(
            IOptions<RabbitMQOptions> options,
            IServiceProvider serviceProvider)
            : base(options, options.Value.TranscribeCompletedQueue)
        {
            _serviceProvider = serviceProvider;
        }

        public void StartConsuming(
            Func<int> getInFlight,
            Action<int> setInFlight,
            CancellationToken stoppingToken)
        {
            StartConsuming<Messages.TranscribeCompletedMessage>(
                HandleTranscribeCompletedAsync,
                getInFlight,
                setInFlight,
                stoppingToken);
        }

        private async Task HandleTranscribeCompletedAsync(Messages.TranscribeCompletedMessage message, CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();
            var logger = scope.ServiceProvider.GetService<ILogger<TranscribeCompletedConsumer>>();

            var document = await dbContext.Documents
                .FirstOrDefaultAsync(x => x.Id == message.DocumentId, token);

            if (document == null)
            {
                logger?.LogWarning("[转写完成] 文档不存在: DocumentId={DocumentId}", message.DocumentId);
                return;
            }

            logger?.LogInformation("[转写完成] 开始处理: DocumentId={DocumentId}, Status={Status}",
                message.DocumentId, message.Status);

            if (message.Status == Messages.TranscribeStatus.Failed || message.Status == Messages.TranscribeStatus.Timeout)
            {
                // 转写失败，更新文档状态
                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, message.Error ?? "转写失败")
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);

                logger?.LogError("[转写完成] 转写失败: DocumentId={DocumentId}, Error={Error}",
                    message.DocumentId, message.Error);
                return;
            }

            // 转写成功，下载转写结果文本
            var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            string transcribedText;

            try
            {
                logger?.LogInformation("[转写完成] 正在下载转写结果: ObjectKey={ObjectKey}",
                    message.TranscribedTextObjectKey);

                using var stream = await objectStorage.GetAsync(message.TranscribedTextObjectKey);
                using var reader = new StreamReader(stream);
                transcribedText = await reader.ReadToEndAsync(token);

                if (string.IsNullOrWhiteSpace(transcribedText))
                {
                    throw new InvalidOperationException("转写结果文本为空");
                }

                logger?.LogInformation("[转写完成] 转写结果下载成功: TextLength={TextLength}",
                    transcribedText.Length);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[转写完成] 下载转写结果失败: DocumentId={DocumentId}",
                    message.DocumentId);

                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, $"下载转写结果失败: {ex.Message}")
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);
                return;
            }

            // 将转写文本保存到 Content 字段
            document.Content = transcribedText;
            document.Status = DocumentStatus.Parsed; // 标记为已解析，可以继续处理

            // 保存更改到数据库
            await dbContext.SaveChangesAsync(token);

            // 执行带重试的处理（切片和向量化）
            var result = await retryPolicy.ExecuteAsync(
                documentId: document.Id,
                currentRetryCount: document.RetryCount,
                processAction: () => ProcessWithRetryAsync(scope, document, dbContext, logger, token),
                republishAction: () => RepublishMessageAsync(message, scope, logger, token),
                logger: logger,
                cancellationToken: token
            );

            await HandleResultAsync(result, document, dbContext, logger, token);
        }

        /// <summary>
        /// 处理文档（切片和向量化）
        /// </summary>
        private async Task ProcessWithRetryAsync(
            IServiceScope scope,
            Document document,
            OmniMindDbContext dbContext,
            ILogger? logger,
            CancellationToken token)
        {
            // 更新重试次数
            document.RetryCount++;
            document.LastRetryAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(token);

            // 执行实际处理（切片和向量化）
            await DocumentProcessor.ProcessDocumentAsync(scope, document, dbContext, logger);

            // 成功后重置重试次数
            await dbContext.Documents
                .Where(x => x.Id == document.Id)
                .ExecuteUpdateAsync(d => d
                    .SetProperty(x => x.RetryCount, 0)
                    .SetProperty(x => x.LastRetryAt, (DateTimeOffset?)null), token);
        }

        /// <summary>
        /// 重新发布消息到队列
        /// </summary>
        private async Task RepublishMessageAsync(
            Messages.TranscribeCompletedMessage originalMessage,
            IServiceScope scope,
            ILogger? logger,
            CancellationToken token)
        {
            var messagePublisher = scope.ServiceProvider.GetService<IMessagePublisher>();
            if (messagePublisher == null)
            {
                logger?.LogWarning("[转写完成] IMessagePublisher 服务未找到，无法重新发布消息: DocumentId={DocumentId}",
                    originalMessage.DocumentId);
                return;
            }

            // 重新发布转写完成消息
            await messagePublisher.PublishTranscribeCompletedAsync(originalMessage, token);

            logger?.LogInformation("[转写完成] 消息已重新入队: DocumentId={DocumentId}",
                originalMessage.DocumentId);
        }

        /// <summary>
        /// 处理重试结果
        /// </summary>
        private async Task HandleResultAsync(
            RetryResult result,
            Document document,
            OmniMindDbContext dbContext,
            ILogger? logger,
            CancellationToken token)
        {
            if (result == RetryResult.Failed || result == RetryResult.MaxRetriesExceeded)
            {
                var errorMsg = result == RetryResult.MaxRetriesExceeded
                    ? $"转写后处理已重试 {retryPolicy.MaxRetryCount} 次仍失败"
                    : "转写后处理失败";

                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, errorMsg)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);

                logger?.LogError("[转写完成] 处理失败: DocumentId={DocumentId}", document.Id);
            }
        }
    }
}
