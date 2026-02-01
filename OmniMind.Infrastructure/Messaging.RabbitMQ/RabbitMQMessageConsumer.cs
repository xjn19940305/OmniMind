using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 消息消费者基类
    /// </summary>
    public abstract class RabbitMQMessageConsumer : IDisposable
    {
        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly RabbitMQOptions options;
        private readonly JsonSerializerSettings jsonSettings;
        private readonly string queueName;

        protected RabbitMQMessageConsumer(
            IOptions<RabbitMQOptions> options,
            string queueName)
        {
            this.options = options.Value;
            this.queueName = queueName;

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

            // 设置预取数量，实现公平分发
            channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        /// <summary>
        /// 开始消费消息
        /// </summary>
        public void StartConsuming<T>(Func<T, Task> handleMessage) where T : class
        {
            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += async (sender, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);

                try
                {
                    var message = JsonConvert.DeserializeObject<T>(json, jsonSettings);

                    if (message != null)
                    {
                        await handleMessage(message);

                        // 手动确认消息
                        channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                    }
                    else
                    {
                        // 消息格式错误，拒绝并重新入队
                        channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                    }
                }
                catch (Exception ex)
                {
                    // 处理失败，记录日志并拒绝消息（不重新入队列，避免死循环）
                    Console.WriteLine($"Error processing message: {ex.Message}");

                    // 可以将失败的消息发送到死信队列
                    channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            channel.BasicConsume(
                queue: queueName,
                autoAck: false, // 手动确认
                consumer: consumer);
        }

        public void Dispose()
        {
            channel?.Dispose();
            connection?.Dispose();
        }
    }
}
