using Microsoft.Extensions.DependencyInjection;
using OmniMind.Abstractions.Ingestion;
using OmniMind.Ingestion;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// Ingestion 服务注册扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加 Ingestion 服务
        /// </summary>
        public static IServiceCollection AddIngestion(this IServiceCollection services)
        {
            // 注册文件解析器
            services.AddSingleton<IFileParser, FileParser>();

            // 注册文本切片器
            services.AddSingleton<IChunker, TextChunker>();

            return services;
        }
    }
}
