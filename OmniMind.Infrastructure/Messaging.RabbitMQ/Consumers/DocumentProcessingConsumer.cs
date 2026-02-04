using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;
using System;
using System.Reflection;

namespace OmniMind.Messaging.RabbitMQ.Consumers
{
    public class DocumentProcessingConsumer : RabbitMQMessageConsumer
    {
        private readonly IServiceProvider _serviceProvider;

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
            var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<DocumentProcessingConsumer>>();
            try
            {
                var document = await dbContext.Documents
                    .FirstOrDefaultAsync(x => x.Id == message.DocumentId, token);
                if (document == null)
                {
                    logger?.LogWarning("文档不存在 DocumentId={DocumentId}",
                        message.DocumentId);
                    return;
                }
                await DocumentProcessor.ProcessDocumentAsync(scope, document, dbContext, logger);
                logger?.LogInformation("文档处理完成 DocumentId={DocumentId}",
                    message.DocumentId);

            }
            catch
            {
                try
                {
                    await dbContext.Documents
                        .Where(x => x.Id == message.DocumentId)
                        .ExecuteUpdateAsync(d => d
                            .SetProperty(x => x.Status, DocumentStatus.Failed)
                            .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);
                }
                catch { }

                throw;
            }
        }
    }
}
