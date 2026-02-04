using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using OmniMind.Abstractions.Ingestion;
using OmniMind.Ingestion;
using Microsoft.Extensions.AI;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// Ingestion 服务注册扩展
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 创建阿里云百练聊天客户端（工厂方法，无需 DI 容器）
        /// </summary>
        public static IChatClient CreateAlibabaCloudClient(
            string apiKey,
            string? model = null,
            string? endpoint = null,
            bool useProxy = true)
        {
            var options = new AlibabaCloudChatOptions
            {
                ApiKey = apiKey,
                Model = model ?? "qwen-max",
                Endpoint = endpoint
            };

            var handler = new System.Net.Http.SocketsHttpHandler
            {
                // 禁用自动解压，避免缓冲
                AutomaticDecompression = System.Net.DecompressionMethods.None,
                // 代理设置
                UseProxy = useProxy,
                // 连接保持/复用
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                // keep-alive 配置
                KeepAlivePingDelay = TimeSpan.FromSeconds(30),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(10),
                EnableMultipleHttp2Connections = false,
            };

            var httpClient = new System.Net.Http.HttpClient(handler, disposeHandler: true)
            {
                BaseAddress = new Uri(options.Endpoint ?? "https://dashscope.aliyuncs.com"),
                Timeout = Timeout.InfiniteTimeSpan,
                // SSE 使用 HTTP/1.1 更稳定
                DefaultRequestVersion = System.Net.HttpVersion.Version11,
                DefaultVersionPolicy = System.Net.Http.HttpVersionPolicy.RequestVersionOrLower,
            };

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

            // 设置 SSE 相关请求头
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
            httpClient.DefaultRequestHeaders.CacheControl =
                new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

            var loggerFactory = NullLoggerFactory.Instance;
            var logger = loggerFactory.CreateLogger<AlibabaCloudChatClient>();

            return new AlibabaCloudChatClient(httpClient, options, logger);
        }

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
                var loggerFactory = NullLoggerFactory.Instance;
                var logger = loggerFactory.CreateLogger<AlibabaCloudChatClient>();
                return CreateAlibabaCloudClient(options.ApiKey, options.Model, options.Endpoint);
            });
            return services;
        }
    }
}
