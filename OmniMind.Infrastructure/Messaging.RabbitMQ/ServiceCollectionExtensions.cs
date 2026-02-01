using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OmniMind.Messaging.Abstractions;
using OmniMind.Messaging.RabbitMQ;
using Microsoft.Extensions.Configuration;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// 服务集合扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加 RabbitMQ 消息服务
        /// </summary>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">配置</param>
        /// <param name="includeConsumer">是否包含消费者（多节点API服务应设为false，消费者服务设为true）</param>
        public static IServiceCollection AddRabbitMQ(
            this IServiceCollection services,
            IConfiguration configuration,
            bool includeConsumer = false)
        {
            // 配置RabbitMQ选项
            services.Configure<RabbitMQOptions>(configuration.GetSection("rabbitMQ"));

            // 注册消息发布者（单例）
            services.AddSingleton<IMessagePublisher, RabbitMQMessagePublisher>();

            return services;
        }
    }
}
