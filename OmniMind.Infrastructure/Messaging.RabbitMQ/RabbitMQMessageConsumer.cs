using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// RabbitMQ 消息消费者基类（适配 RabbitMQ.Client 6.8.1）
    /// - AsyncEventingBasicConsumer + DispatchConsumersAsync=true
    /// - prefetch=1（避免并发导致 channel 非线程安全问题）
    /// - Ack/Nack 安全容错（channel disposed/closed 不崩溃）
    /// - 支持 CancellationToken 优雅停止（Stop() + Dispose）
    /// </summary>
    public abstract class RabbitMQMessageConsumer : IDisposable
    {
        private readonly RabbitMQOptions _options;
        private readonly JsonSerializerSettings _jsonSettings;
        private readonly string _queueName;

        private IConnection? _connection;
        private IModel? _channel;
        private string? _consumerTag;

        protected RabbitMQMessageConsumer(IOptions<RabbitMQOptions> options, string queueName)
        {
            _options = options.Value;
            _queueName = queueName;

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        /// <summary>
        /// 建立连接与通道（建议：不要依赖 AutomaticRecoveryEnabled，交由外层 Worker 重连）
        /// </summary>
        protected virtual void EnsureConnected()
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                return;

            DisposeInternal(); // 清理旧的

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,

                DispatchConsumersAsync = true,    // ✅ async consumer 必开
                AutomaticRecoveryEnabled = false, // ✅ 推荐由外层重连循环控制
                TopologyRecoveryEnabled = false
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // ✅ 公平分发：每个消费者最多持有 1 条未 Ack 消息（你可按吞吐调大）
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }

        /// <summary>
        /// 开始消费消息（异步回调）
        /// </summary>
        public void StartConsuming<T>(
            Func<T, CancellationToken, Task> handleMessageAsync,
            Func<int> getInFlight,
            Action<int> setInFlight,
            CancellationToken stoppingToken) where T : class
        {
            EnsureConnected();

            var channel = _channel!;
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.Received += async (_, ea) =>
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    // 正在停止：不处理新消息，让其回到队列（或由别的实例接手）
                    SafeNack(channel, ea.DeliveryTag, requeue: true);
                    return;
                }

                setInFlight(getInFlight() + 1);
                try
                {
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var message = JsonConvert.DeserializeObject<T>(json, _jsonSettings);

                    if (message == null)
                    {
                        SafeNack(channel, ea.DeliveryTag, requeue: false);
                        return;
                    }
                    // 业务处理
                    await handleMessageAsync(message, stoppingToken);
                    // 手动确认
                    SafeAck(channel, ea.DeliveryTag);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 停机中：不 ack，回到队列
                    SafeNack(channel, ea.DeliveryTag, requeue: true);
                }
                catch (Exception)
                {
                    // ✅ 处理失败：先不 requeue（避免死循环）；建议你做 DLX+重试队列
                    SafeNack(channel, ea.DeliveryTag, requeue: false);
                    // 抛不抛都行：如果抛，外层重连循环会重建连接；
                    // 这里不抛，保持消费继续。
                }
                finally
                {
                    setInFlight(Math.Max(0, getInFlight() - 1));
                }
            };

            _consumerTag = channel.BasicConsume(
                queue: _queueName,
                autoAck: false,
                consumer: consumer);
        }

        /// <summary>
        /// 停止消费（不立即 Dispose，先取消订阅）
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_channel != null && _channel.IsOpen && !string.IsNullOrEmpty(_consumerTag))
                {
                    _channel.BasicCancel(_consumerTag);
                }
            }
            catch { /* ignore */ }
        }

        private static void SafeAck(IModel channel, ulong tag)
        {
            try
            {
                if (channel.IsOpen)
                    channel.BasicAck(tag, multiple: false);
            }
            catch (ObjectDisposedException) { }
            catch (AlreadyClosedException) { }
        }

        private static void SafeNack(IModel channel, ulong tag, bool requeue)
        {
            try
            {
                if (channel.IsOpen)
                    channel.BasicNack(tag, multiple: false, requeue: requeue);
            }
            catch (ObjectDisposedException) { }
            catch (AlreadyClosedException) { }
        }

        private void DisposeInternal()
        {
            try { Stop(); } catch { }
            try { _channel?.Close(); } catch { }
            try { _connection?.Close(); } catch { }

            _channel?.Dispose();
            _connection?.Dispose();
            _channel = null;
            _connection = null;
            _consumerTag = null;
        }

        public void Dispose()
        {
            DisposeInternal();
            GC.SuppressFinalize(this);
        }
    }
}
