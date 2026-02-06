using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Messaging.RabbitMQ;
using OmniMind.Messaging.RabbitMQ.Consumers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Works
{
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<DocumentProcessingWorker> logger;
        private readonly IOptions<RabbitMQOptions> options;
        private DocumentProcessingConsumer? _documentUploadConsumer;
        private TranscribeCompletedConsumer? _transcribeCompletedConsumer;
        private int _inFlightMessages = 0;

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
                _documentUploadConsumer = new DocumentProcessingConsumer(options, serviceProvider);
                _transcribeCompletedConsumer = new TranscribeCompletedConsumer(options, serviceProvider);

                // 同时启动两个消费者
                var tasks = new List<Task>
                {
                    Task.Run(() => RunDocumentUploadConsumer(stoppingToken), stoppingToken),
                    Task.Run(() => RunTranscribeCompletedConsumer(stoppingToken), stoppingToken)
                };

                // 等待任一任务完成（通常意味着停止）
                await Task.WhenAny(tasks);
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

        /// <summary>
        /// 运行文档上传消费者
        /// </summary>
        private void RunDocumentUploadConsumer(CancellationToken stoppingToken)
        {
            try
            {
                logger.LogInformation("[DocumentProcessingWorker] 开始监听队列: {Queue}", options.Value.DocumentUploadQueue);
                _documentUploadConsumer!.StartConsuming(
                    () => _inFlightMessages,
                    count => _inFlightMessages = count,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[DocumentProcessingWorker] 文档上传消费者已停止");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DocumentProcessingWorker] 文档上传消费者运行异常，将在 5 秒后重启");
                Thread.Sleep(5000);
                throw; // 重新启动
            }
        }

        /// <summary>
        /// 运行转写完成消费者
        /// </summary>
        private void RunTranscribeCompletedConsumer(CancellationToken stoppingToken)
        {
            try
            {
                logger.LogInformation("[DocumentProcessingWorker] 开始监听队列: {Queue}", options.Value.TranscribeCompletedQueue);
                _transcribeCompletedConsumer!.StartConsuming(
                    () => _inFlightMessages,
                    count => _inFlightMessages = count,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[DocumentProcessingWorker] 转写完成消费者已停止");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[DocumentProcessingWorker] 转写完成消费者运行异常，将在 5 秒后重启");
                Thread.Sleep(5000);
                throw; // 重新启动
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("[DocumentProcessingWorker] 正在停止消费者服务...");

            _documentUploadConsumer?.Dispose();
            _transcribeCompletedConsumer?.Dispose();

            await base.StopAsync(cancellationToken);
        }
    }
}