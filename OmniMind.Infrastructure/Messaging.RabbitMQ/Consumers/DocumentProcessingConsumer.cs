using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.MySql;

namespace OmniMind.Messaging.RabbitMQ.Consumers
{
    /// <summary>
    /// 文档处理消费者
    /// 负责处理文档上传后的解析、切片、向量化等操作
    /// </summary>
    public class DocumentProcessingConsumer : RabbitMQMessageConsumer
    {
        private readonly IServiceProvider serviceProvider;

        public DocumentProcessingConsumer(
            IOptions<RabbitMQOptions> options,
            IServiceProvider serviceProvider)
            : base(options, options.Value.DocumentUploadQueue)
        {
            this.serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 开始消费文档上传消息
        /// </summary>
        public void StartConsuming()
        {
            StartConsuming<Messages.DocumentUploadMessage>(HandleDocumentUploadAsync);
        }

        /// <summary>
        /// 处理文档上传消息
        /// </summary>
        private async Task HandleDocumentUploadAsync(Messages.DocumentUploadMessage message)
        {
            Console.WriteLine($"[文档处理] 开始处理文档: DocumentId={message.DocumentId}, TenantId={message.TenantId}");

            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            try
            {
                // 1. 查找文档
                var document = await dbContext.Documents.IgnoreQueryFilters().Where(x => x.Id == message.DocumentId && x.TenantId == message.TenantId).FirstOrDefaultAsync();
                if (document == null)
                {
                    Console.WriteLine($"[文档处理] 文档不存在: {message.DocumentId}");
                    return;
                }

                // 2. 执行文档处理逻辑
                await DocumentProcessor.ProcessDocumentAsync(document, dbContext, null);

                Console.WriteLine($"[文档处理] 文档处理完成: DocumentId={message.DocumentId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[文档处理] 文档处理失败: DocumentId={message.DocumentId}, Error={ex.Message}");
                throw; // 重新抛出异常，让消息被Nack
            }
        }
    }
}
