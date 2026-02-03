using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using OmniMind.Abstractions.SignalR;
using OmniMind.Realtime.SignalR;

namespace OmniMind.Realtime.SignalR
{
    /// <summary>
    /// SignalR 服务注册扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加 SignalR 服务
        /// </summary>
        public static ISignalRServerBuilder AddSignalRServices(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // 添加 SignalR
            var signalRBuilder = services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.KeepAliveInterval = TimeSpan.FromSeconds(10);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
                options.HandshakeTimeout = TimeSpan.FromSeconds(15);
            });

            // 配置 Redis 背板（用于多服务器扩展）
            var redisConnectionString = configuration["StackExchangeRedis:Connection"];
            if (!string.IsNullOrEmpty(redisConnectionString))
            {
                signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
                {
                    options.Configuration.ChannelPrefix = "OmniMind.SignalR";
                });
            }

            // 注册实时通知服务
            services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

            return signalRBuilder;
        }
    }
}
