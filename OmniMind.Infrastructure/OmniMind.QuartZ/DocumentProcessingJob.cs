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
    /// 支持两种模式：
    /// 1. 定时批量处理模式：按Cron表达式定时从数据库查询并处理文档
    /// 2. 持续监听模式：使用DocumentProcessingConsumer监听RabbitMQ队列
    /// </summary>
    [DisallowConcurrentExecution] // 禁止并发执行，确保只有一个实例在运行
    public class DocumentProcessingJob : IJob
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<DocumentProcessingJob> logger;
        private readonly RabbitMQOptions options;

        // 用于控制持续模式运行的标志
        private static volatile bool _isRunning = false;
        private static DocumentProcessingConsumer? _consumer;
        private static CancellationTokenSource? _consumerCts;

        public DocumentProcessingJob(
            IServiceProvider serviceProvider,
            ILogger<DocumentProcessingJob> logger,
            IOptions<RabbitMQOptions> options)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.options = options.Value;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var jobKey = context.JobDetail.Key;
            logger.LogInformation("[{JobKey}] DocumentProcessingJob 开始执行", jobKey);

            // 获取Job配置
            var mode = context.MergedJobDataMap.GetString("mode") ?? "batch"; // batch 或 continuous
            var batchSize = context.MergedJobDataMap.GetInt("batchSize");
            var timeoutSeconds = context.MergedJobDataMap.GetInt("timeoutSeconds");

            if (mode == "continuous")
            {
                // 持续监听模式：使用DocumentProcessingConsumer监听队列
                await ExecuteContinuousMode(context);
            }
            else
            {
                // 定时批量处理模式：直接从数据库查询并处理文档
                await ExecuteBatchMode(context, batchSize);
            }

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
                        await DocumentProcessor.ProcessDocumentAsync(document, dbContext, logger);
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

        /// <summary>
        /// 持续监听模式
        /// 使用DocumentProcessingConsumer持续监听RabbitMQ队列
        /// </summary>
        private async Task ExecuteContinuousMode(IJobExecutionContext context)
        {
            // 如果已经在运行，直接返回
            if (_isRunning)
            {
                logger.LogWarning("[持续模式] 消费者已在运行中，跳过本次执行");
                return;
            }

            _isRunning = true;
            _consumerCts = new CancellationTokenSource();

            logger.LogInformation("[持续模式] 启动RabbitMQ消费者，持续监听队列");

            try
            {
                // 创建DocumentProcessingConsumer实例
                _consumer = new DocumentProcessingConsumer(
                    Microsoft.Extensions.Options.Options.Create(options),
                    serviceProvider);

                // 在后台线程中启动消费者
                await Task.Run(() =>
                {
                    try
                    {
                        _consumer.StartConsuming();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[持续模式] 消费者运行异常");
                    }
                }, _consumerCts.Token);

                // 等待被中断
                while (!_consumerCts.Token.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, context.CancellationToken);
                }

                logger.LogInformation("[持续模式] 收到中断信号，正在停止消费者...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[持续模式] 消费者启动异常");
            }
            finally
            {
                _isRunning = false;
                logger.LogInformation("[持续模式] 消费者已停止");
            }
        }
    }
}
