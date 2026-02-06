using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OmniMind.Messaging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 消息发布者实现
    /// </summary>
    public class RabbitMQMessagePublisher : IMessagePublisher, IDisposable
    {
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly RabbitMQOptions options;
        private readonly JsonSerializerSettings jsonSettings;

        public RabbitMQMessagePublisher(IOptions<RabbitMQOptions> options)
        {
            this.options = options.Value;

            var factory = new ConnectionFactory
            {
                HostName = this.options.HostName,
                Port = this.options.Port,
                UserName = this.options.UserName,
                Password = this.options.Password,
                VirtualHost = this.options.VirtualHost,
                AutomaticRecoveryEnabled = this.options.AutomaticRecoveryEnabled
            };

            connection = factory.CreateConnection();
            channel = connection.CreateModel();

            // 声明交换机
            channel.ExchangeDeclare(
                exchange: this.options.DocumentExchange,
                type: ExchangeType.Direct,
                durable: true,
                autoDelete: false);

            // 声明队列
            channel.QueueDeclare(
                queue: this.options.DocumentUploadQueue,
                durable: true,
                exclusive: false,
                autoDelete: false);

            // 绑定队列到交换机
            channel.QueueBind(
                queue: this.options.DocumentUploadQueue,
                exchange: this.options.DocumentExchange,
                routingKey: this.options.DocumentUploadRoutingKey);

            jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        public Task PublishAsync<T>(string queueName, T message, CancellationToken ct = default) where T : class
        {
            var json = JsonConvert.SerializeObject(message, jsonSettings);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // 持久化
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: this.options.DocumentExchange,
                routingKey: this.options.DocumentUploadRoutingKey,
                basicProperties: properties,
                body: body);

            return Task.CompletedTask;
        }

        public Task PublishDocumentUploadAsync(Messages.DocumentUploadMessage message, CancellationToken ct = default)
        {
            return PublishAsync(options.DocumentUploadQueue, message, ct);
        }

        public Task PublishTranscribeRequestAsync(Messages.TranscribeRequestMessage message, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(message, jsonSettings);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // 持久化
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: options.DocumentExchange,
                routingKey: options.TranscribeRequestRoutingKey,
                basicProperties: properties,
                body: body);

            return Task.CompletedTask;
        }

        public Task PublishTranscribeCompletedAsync(Messages.TranscribeCompletedMessage message, CancellationToken ct = default)
        {
            var json = JsonConvert.SerializeObject(message, jsonSettings);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = channel.CreateBasicProperties();
            properties.DeliveryMode = 2; // 持久化
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            channel.BasicPublish(
                exchange: options.DocumentExchange,
                routingKey: options.TranscribeCompletedRoutingKey,
                basicProperties: properties,
                body: body);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            channel?.Dispose();
            connection?.Dispose();
        }
    }
}
