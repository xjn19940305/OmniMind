using Microsoft.Extensions.Options;
using OmniMind.Messaging.RabbitMQ;
using OmniMind.Messaging.RabbitMQ.Consumers;

namespace OmniMind.Workers
{
    /// <summary>
    /// 文档处理消费者后台服务
    /// 持续监听 RabbitMQ 队列并处理文档上传消息
    /// </summary>
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<DocumentProcessingWorker> logger;
        private readonly IOptions<RabbitMQOptions> options;
        private DocumentProcessingConsumer? _consumer;

        public DocumentProcessingWorker(
            IServiceProvider serviceProvider,
            ILogger<DocumentProcessingWorker> logger,
            IOptions<RabbitMQOptions> options)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("[DocumentProcessingWorker] 启动文档处理消费者服务");

            try
            {
                // 创建消费者实例
                _consumer = new DocumentProcessingConsumer(options, serviceProvider);

                // 开始监听队列（阻塞调用）
                // 注意：StartConsuming() 会阻塞直到连接断开或服务停止
                await Task.Run(() =>
                {
                    try
                    {
                        logger.LogInformation("[DocumentProcessingWorker] 开始监听队列: {Queue}", options.Value.DocumentUploadQueue);
                        _consumer.StartConsuming();
                    }
                    catch (OperationCanceledException)
                    {
                        // 正常停止，不记录错误
                        logger.LogInformation("[DocumentProcessingWorker] 消费者服务已停止");
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[DocumentProcessingWorker] 消费者运行异常，将在 5 秒后重启");
                        Thread.Sleep(5000);
                        throw; // 重新启动 BackgroundService
                    }
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 服务停止，正常退出
                logger.LogInformation("[DocumentProcessingWorker] 消费者服务已停止");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DocumentProcessingWorker] 消费者服务启动失败");
                throw;
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("[DocumentProcessingWorker] 正在停止消费者服务...");

            _consumer?.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}
