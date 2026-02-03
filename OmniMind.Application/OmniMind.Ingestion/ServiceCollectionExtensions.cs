using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Linq;
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

        /// <summary>
        /// 添加阿里云向量化服务
        /// </summary>
        public static IServiceCollection AddAlibabaCloudEmbedding(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new AlibabaCloudOptions();
            var section = configuration.GetSection("AlibabaCloud");
            options.ApiKey = section["ApiKey"] ?? string.Empty;
            options.Endpoint = section["Endpoint"];
            options.Model = section["Model"];
            options.VectorSize = int.Parse(section["VectorSize"] ?? "1024");

            services.AddSingleton<global::Microsoft.Extensions.AI.IEmbeddingGenerator<string, global::Microsoft.Extensions.AI.Embedding<float>>>(sp =>
            {
                var httpClient = new System.Net.Http.HttpClient();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AlibabaCloudEmbeddingGenerator>>();
                return new AlibabaCloudEmbeddingGenerator(httpClient, options, logger);
            });
            return services;
        }

        /// <summary>
        /// 添加本地模型向量化服务
        /// </summary>
        public static IServiceCollection AddLocalEmbedding(this IServiceCollection services, IConfiguration configuration)
        {
            var options = new LocalEmbeddingOptions();
            var section = configuration.GetSection("LocalEmbedding");
            options.ModelPath = section["ModelPath"] ?? string.Empty;
            options.ModelType = section["ModelType"] ?? "onnx";
            options.VectorSize = int.Parse(section["VectorSize"] ?? "768");
            options.MaxTokens = int.Parse(section["MaxTokens"] ?? "512");
            options.UseGpu = bool.Parse(section["UseGpu"] ?? "false");
            options.Threads = int.Parse(section["Threads"] ?? Environment.ProcessorCount.ToString());

            services.AddSingleton<global::Microsoft.Extensions.AI.IEmbeddingGenerator<string, global::Microsoft.Extensions.AI.Embedding<float>>>(sp =>
            {
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LocalEmbeddingGenerator>>();
                return new LocalEmbeddingGenerator(options, logger);
            });
            return services;
        }

        /// <summary>
        /// 添加阿里云百练聊天服务
        /// </summary>
        public static IServiceCollection AddAlibabaCloudChatClient(this IServiceCollection services, IConfiguration configuration)
        {
            var alibabaCloudSection = configuration.GetSection("AlibabaCloud");
            var chatSection = alibabaCloudSection.GetSection("Chat");

            var options = new AlibabaCloudChatOptions();

            // ApiKey 从外层 AlibabaCloud 读取
            options.ApiKey = alibabaCloudSection["ApiKey"] ?? string.Empty;

            // Endpoint 优先使用 Chat 下的，否则使用外层的
            options.Endpoint = chatSection["Endpoint"] ?? alibabaCloudSection["Endpoint"];

            // Model 是数组，取第一个作为默认模型，同时保存所有模型
            var modelSection = chatSection.GetSection("Model");
            var models = modelSection.GetChildren().Select(x => x.Value).ToArray();
            options.Models = models;
            options.Model = models?.FirstOrDefault() ?? "qwen-max";

            // 读取可选参数
            if (int.TryParse(chatSection["MaxTokens"], out int maxTokens))
            {
                options.MaxTokens = maxTokens;
            }
            if (float.TryParse(chatSection["Temperature"], out float temperature))
            {
                options.Temperature = temperature;
            }
            if (float.TryParse(chatSection["TopP"], out float topP))
            {
                options.TopP = topP;
            }

            services.AddSingleton<global::Microsoft.Extensions.AI.IChatClient>(sp =>
            {
                var httpClient = new System.Net.Http.HttpClient();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AlibabaCloudChatClient>>();
                return new AlibabaCloudChatClient(httpClient, options, logger);
            });
            return services;
        }
    }
}
