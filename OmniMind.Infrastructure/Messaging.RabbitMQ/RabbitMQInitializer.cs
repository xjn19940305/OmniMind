using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 队列初始化器
    /// 确保队列和交换机在应用启动时被声明
    /// </summary>
    public class RabbitMQInitializer : IHostedService
    {
        private readonly RabbitMQOptions options;
        private readonly ILogger<RabbitMQInitializer> logger;

        public RabbitMQInitializer(
            IOptions<RabbitMQOptions> options,
            ILogger<RabbitMQInitializer> logger)
        {
            this.options = options.Value;
            this.logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = options.HostName,
                    Port = options.Port,
                    UserName = options.UserName,
                    Password = options.Password,
                    VirtualHost = options.VirtualHost,
                    AutomaticRecoveryEnabled = true
                };

                using var connection = factory.CreateConnection();
                using var channel = connection.CreateModel();

                // 声明交换机
                channel.ExchangeDeclare(
                    exchange: options.DocumentExchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                logger.LogInformation("RabbitMQ 交换机已声明: {Exchange}", options.DocumentExchange);

                // 声明队列
                channel.QueueDeclare(
                    queue: options.DocumentUploadQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                logger.LogInformation("RabbitMQ 队列已声明: {Queue}", options.DocumentUploadQueue);

                // 绑定队列到交换机
                channel.QueueBind(
                    queue: options.DocumentUploadQueue,
                    exchange: options.DocumentExchange,
                    routingKey: options.DocumentUploadRoutingKey);

                logger.LogInformation("RabbitMQ 队列绑定完成: {Queue} -> {Exchange} ({RoutingKey})",
                    options.DocumentUploadQueue,
                    options.DocumentExchange,
                    options.DocumentUploadRoutingKey);

                #region 声明转写相关队列

                // 转写请求队列
                channel.QueueDeclare(
                    queue: options.TranscribeRequestQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                channel.QueueBind(
                    queue: options.TranscribeRequestQueue,
                    exchange: options.DocumentExchange,
                    routingKey: options.TranscribeRequestRoutingKey);

                logger.LogInformation("RabbitMQ 转写请求队列已声明: {Queue}", options.TranscribeRequestQueue);

                // 转写完成队列
                channel.QueueDeclare(
                    queue: options.TranscribeCompletedQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);

                channel.QueueBind(
                    queue: options.TranscribeCompletedQueue,
                    exchange: options.DocumentExchange,
                    routingKey: options.TranscribeCompletedRoutingKey);

                logger.LogInformation("RabbitMQ 转写完成队列已声明: {Queue}", options.TranscribeCompletedQueue);

                #endregion
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RabbitMQ 队列初始化失败");
                throw;
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
