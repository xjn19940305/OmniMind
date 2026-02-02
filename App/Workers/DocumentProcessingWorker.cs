using Microsoft.Extensions.Options;
using OmniMind.Messaging.RabbitMQ;
using OmniMind.Messaging.RabbitMQ.Consumers;

namespace App.Workers
{
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DocumentProcessingWorker> _logger;
        private readonly IOptions<RabbitMQOptions> _options;

        private DocumentProcessingConsumer? _consumer;
        private int _inFlight = 0;

        public DocumentProcessingWorker(
            IServiceProvider serviceProvider,
            ILogger<DocumentProcessingWorker> logger,
            IOptions<RabbitMQOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[DocumentProcessingWorker] 启动文档处理消费者服务");

            var delay = TimeSpan.FromSeconds(2);
            var maxDelay = TimeSpan.FromSeconds(30);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _consumer?.Dispose();
                    _consumer = new DocumentProcessingConsumer(_options, _serviceProvider);

                    _logger.LogInformation("[DocumentProcessingWorker] 开始监听队列: {Queue}",
                        _options.Value.DocumentUploadQueue);

                    // 启动消费（非阻塞）
                    _consumer.StartConsuming(
                        getInFlight: () => Volatile.Read(ref _inFlight),
                        setInFlight: v => Volatile.Write(ref _inFlight, v),
                        stoppingToken: stoppingToken);

                    // 常驻直到停止信号（或你想通过某些机制让它返回并重连）
                    await Task.Delay(Timeout.Infinite, stoppingToken);

                    delay = TimeSpan.FromSeconds(2);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("[DocumentProcessingWorker] 收到停止信号，退出监听");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[DocumentProcessingWorker] 消费者异常退出，将在 {Delay}s 后重连",
                        delay.TotalSeconds);

                    try
                    {
                        await Task.Delay(delay, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("[DocumentProcessingWorker] 正在停止消费者服务...");

            // 先取消订阅，避免再拉新消息
            _consumer?.Stop();

            // 等待最多 10 秒让在途处理完
            var until = DateTime.UtcNow.AddSeconds(10);
            while (Volatile.Read(ref _inFlight) > 0 && DateTime.UtcNow < until)
            {
                await Task.Delay(200, cancellationToken);
            }

            _consumer?.Dispose();
            await base.StopAsync(cancellationToken);
        }
    }
}
