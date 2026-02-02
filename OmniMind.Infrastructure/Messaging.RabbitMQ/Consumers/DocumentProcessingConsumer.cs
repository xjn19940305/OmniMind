using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Abstractions.Tenant;
using OmniMind.Enums;
using OmniMind.Persistence.MySql;
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
            var tenantProvider = scope.ServiceProvider.GetService<ITenantProvider>();
            var logger = scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILogger<DocumentProcessingConsumer>>();
            try
            {

                tenantProvider?.SetTenant(message.TenantId);
                var document = await dbContext.Documents.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.Id == message.DocumentId && x.TenantId == message.TenantId, token);
                if (document == null)
                {
                    logger?.LogWarning("文档不存在 DocumentId={DocumentId} TenantId={TenantId}",
                        message.DocumentId, message.TenantId);
                    return;
                }
                await DocumentProcessor.ProcessDocumentAsync(scope, document, dbContext, logger);
                logger?.LogInformation("文档处理完成 DocumentId={DocumentId} TenantId={TenantId}",
                    message.DocumentId, message.TenantId);

            }
            catch
            {
                try
                {
                    await dbContext.Documents.IgnoreQueryFilters()
                        .Where(x => x.Id == message.DocumentId && x.TenantId == message.TenantId)
                        .ExecuteUpdateAsync(d => d
                            .SetProperty(x => x.Status, DocumentStatus.Failed)
                            .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), token);
                }
                catch { }

                throw;
            }
            finally
            {
                tenantProvider?.ClearTenant();
            }
        }
    }
}
