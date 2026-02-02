using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Enums;
using OmniMind.Messaging.RabbitMQ;
using OmniMind.Messaging.RabbitMQ.Consumers;
using OmniMind.Persistence.MySql;
using Quartz;

namespace OmniMind.QuartZ
{
    /// <summary>
    /// 文档处理Job
    /// </summary>
    [DisallowConcurrentExecution] // 禁止并发执行，确保只有一个实例在运行
    public class DocumentProcessingJob : IJob
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<DocumentProcessingJob> logger;

        public DocumentProcessingJob(
            IServiceProvider serviceProvider,
            ILogger<DocumentProcessingJob> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobKey = context.JobDetail.Key;
            logger.LogInformation("[{JobKey}] DocumentProcessingJob 开始执行", jobKey);
            // 获取Job配置
            var batchSize = context.MergedJobDataMap.GetInt("batchSize");
            var timeoutSeconds = context.MergedJobDataMap.GetInt("timeoutSeconds");

            await ExecuteBatchMode(context, batchSize);


            logger.LogInformation("[{JobKey}] DocumentProcessingJob 执行完成", jobKey);
        }

        /// <summary>
        /// 批量处理模式
        /// 定时从数据库查询Status=Uploaded的文档进行处理
        /// </summary>
        private async Task ExecuteBatchMode(IJobExecutionContext context, int batchSize)
        {
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            logger.LogInformation("[批量模式] 开始处理文档，批次大小: {BatchSize}", batchSize);

            try
            {
                // 1. 查询待处理的文档
                var documents = await dbContext.Documents
                    .Where(d => d.Status == DocumentStatus.Uploaded)
                    .OrderBy(d => d.CreatedAt)
                    .Take(batchSize)
                    .ToListAsync();

                if (documents.Count == 0)
                {
                    logger.LogInformation("[批量模式] 没有待处理的文档");
                    return;
                }

                logger.LogInformation("[批量模式] 找到 {Count} 个待处理文档", documents.Count);

                // 2. 使用DocumentProcessor处理每个文档
                foreach (var document in documents)
                {
                    try
                    {
                        await DocumentProcessor.ProcessDocumentAsync(scope, document, dbContext, logger);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[批量模式] 处理文档失败: DocumentId={DocumentId}", document.Id);
                    }
                }

                logger.LogInformation("[批量模式] 批次处理完成，成功处理 {Count} 个文档", documents.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[批量模式] 批处理失败");
                throw;
            }
        }

    }
}
