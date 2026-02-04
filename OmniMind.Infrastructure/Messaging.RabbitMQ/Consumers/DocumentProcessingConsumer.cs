using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.PostgreSql;

namespace OmniMind.Messaging.RabbitMQ.Consumers
{
    public class DocumentProcessingConsumer : RabbitMQMessageConsumer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MessageRetryPolicy retryPolicy = new();

        public DocumentProcessingConsumer(
            IOptions<RabbitMQOptions> options,
            IServiceProvider serviceProvider)
            : base(options, options.Value.DocumentUploadQueue)
        {
            _serviceProvider = serviceProvider;
        }

        public void StartConsuming(
            Func<int> getInFlight,
            Action<int> setInFlight,
            CancellationToken stoppingToken)
        {
            StartConsuming<Messages.DocumentUploadMessage>(
                HandleDocumentUploadAsync,
                getInFlight,
                setInFlight,
                stoppingToken);
        }

        private async Task HandleDocumentUploadAsync(Messages.DocumentUploadMessage message, CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();
            var logger = scope.ServiceProvider.GetService<ILogger<DocumentProcessingConsumer>>();

            var document = await dbContext.Documents
                .FirstOrDefaultAsync(x => x.Id == message.DocumentId, token);

            if (document == null)
            {
                logger?.LogWarning("文档不存在 DocumentId={DocumentId}", message.DocumentId);
                return;
            }

            // 执行带重试的处理
            var result = await retryPolicy.ExecuteAsync(
                documentId: document.Id,
                currentRetryCount: document.RetryCount,
                processAction: () => ProcessWithRetryAsync(scope, document, dbContext, logger, token),
                republishAction: () => RepublishMessageAsync(document, scope, logger, token),
                logger: logger,
                cancellationToken: token
            );

            // 处理结果
            await HandleResultAsync(result, document, dbContext, logger, token);
        }

        /// <summary>
        /// 处理文档（更新重试次数并执行处理逻辑）
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

            // 执行实际处理
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
            Document document,
            IServiceScope scope,
            ILogger? logger,
            CancellationToken token)
        {
            var messagePublisher = scope.ServiceProvider.GetService<IMessagePublisher>();
            if (messagePublisher == null)
            {
                logger?.LogWarning("IMessagePublisher 服务未找到，无法重新发布消息 DocumentId={DocumentId}", document.Id);
                return;
            }

            await messagePublisher.PublishDocumentUploadAsync(new Messages.DocumentUploadMessage
            {
                DocumentId = document.Id,
                KnowledgeBaseId = document.KnowledgeBaseId ?? string.Empty,
                ObjectKey = document.ObjectKey ?? string.Empty,
                FileName = document.Title,
                ContentType = document.ContentType
            });

            logger?.LogInformation("文档已重新入队 DocumentId={DocumentId}, RetryCount={RetryCount}",
                document.Id, document.RetryCount);
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
                    ? $"已重试 {retryPolicy.MaxRetryCount} 次仍失败"
                    : "处理失败";

                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, errorMsg)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);

                logger?.LogError("文档处理失败，标记为失败状态 DocumentId={DocumentId}", document.Id);
            }
        }
    }
}
