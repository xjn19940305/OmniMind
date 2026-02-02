using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 阿里云 DashScope 文本向量化服务
    /// 文档: https://help.aliyun.com/zh/dashscope/developer-reference/text-embedding-api-details
    /// 实现 Microsoft.Extensions.AI 的 IEmbeddingGenerator 接口
    /// </summary>
    public sealed class AlibabaCloudEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly HttpClient httpClient;
        private readonly AlibabaCloudOptions options;
        private readonly ILogger<AlibabaCloudEmbeddingGenerator> logger;

        public AlibabaCloudEmbeddingGenerator(
            HttpClient httpClient,
            AlibabaCloudOptions options,
            ILogger<AlibabaCloudEmbeddingGenerator> logger)
        {
            this.httpClient = httpClient;
            this.options = options;
            this.logger = logger;

            // 设置 DashScope API 地址
            this.httpClient.BaseAddress = new Uri(this.options.Endpoint ?? "https://dashscope.aliyuncs.com");

            // 使用配置的模型
            var modelId = this.options.Model ?? "text-embedding-v3";

            // 创建元数据属性
            Metadata = new EmbeddingGeneratorMetadata(
                modelId: modelId,
                dimensions: VectorSize);

            // 设置默认请求头
            this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.options.ApiKey}");
            this.httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        public EmbeddingGeneratorMetadata Metadata { get; }

        /// <summary>
        /// 获取向量维度
        /// </summary>
        public int VectorSize => options.VectorSize > 0 ? options.VectorSize : 1024;

        /// <summary>
        /// 生成向量
        /// </summary>
        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var texts = values.ToList();
            if (texts.Count == 0)
            {
                return new GeneratedEmbeddings<Embedding<float>>();
            }

            logger.LogDebug("[AlibabaCloudEmbedding] 正在向量化 {Count} 个文本", texts.Count);

            // DashScope API 单次最多支持 25 个文本
            const int batchSize = 25;
            var allEmbeddings = new List<Embedding<float>>();

            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();
                var embeddings = await GenerateBatchInternalAsync(batch, cancellationToken);
                allEmbeddings.AddRange(embeddings);
            }

            return new GeneratedEmbeddings<Embedding<float>>(allEmbeddings);
        }

        /// <summary>
        /// 内部批量实现
        /// </summary>
        private async Task<List<Embedding<float>>> GenerateBatchInternalAsync(
            List<string> texts,
            CancellationToken cancellationToken)
        {
            try
            {
                // 构建请求体
                var requestBody = new
                {
                    model = Metadata.ModelId,
                    input = new
                    {
                        texts = texts
                    },
                    parameters = new
                    {
                        text_type = "document" // document 或 query
                    }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                // 发送请求
                var response = await httpClient.PostAsync(
                    "/api/v1/services/embeddings/text-embedding/text-embedding",
                    content,
                    cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("[AlibabaCloudEmbedding] API 调用失败: {StatusCode}, {Body}",
                        response.StatusCode, responseBody);
                    throw new InvalidOperationException($"阿里云向量化 API 调用失败: {response.StatusCode}");
                }

                // 解析响应
                using var jsonDoc = JsonDocument.Parse(responseBody);
                var root = jsonDoc.RootElement;

                // DashScope 响应格式: { "output": { "embeddings": [...] } }
                if (root.TryGetProperty("output", out var output) &&
                    output.TryGetProperty("embeddings", out var embeddings))
                {
                    var result = new List<Embedding<float>>();

                    foreach (var embedding in embeddings.EnumerateArray())
                    {
                        if (embedding.TryGetProperty("embedding", out var vector))
                        {
                            var floatArray = new float[vector.GetArrayLength()];
                            int index = 0;
                            foreach (var element in vector.EnumerateArray())
                            {
                                floatArray[index++] = element.GetSingle();
                            }

                            // 创建官方的 Embedding<float> 对象
                            result.Add(new Embedding<float>(floatArray));
                        }
                    }

                    logger.LogDebug("[AlibabaCloudEmbedding] 向量化完成，生成 {Count} 个向量", result.Count);
                    return result;
                }

                logger.LogError("[AlibabaCloudEmbedding] 响应格式错误: {Body}", responseBody);
                throw new InvalidOperationException("阿里云向量化 API 响应格式错误");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[AlibabaCloudEmbedding] 向量化失败");
                throw;
            }
        }

        /// <summary>
        /// 获取服务对象（元数据等）
        /// </summary>
        public object? GetService(Type serviceType, object? key = null)
        {
            // 返回元数据
            if (key is null && serviceType == typeof(EmbeddingGeneratorMetadata))
            {
                return Metadata;
            }

            // 返回自身
            if (key is null && serviceType?.IsInstanceOfType(this) is true)
            {
                return this;
            }

            return null;
        }

        /// <summary>
        /// 获取服务对象（泛型版本）
        /// </summary>
        public TService? GetService<TService>(object? key = null) where TService : class
        {
            return GetService(typeof(TService), key) as TService;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // HttpClient 由 DI 容器管理，这里不需要释放
        }
    }

    /// <summary>
    /// 阿里云 DashScope 配置选项
    /// </summary>
    public class AlibabaCloudOptions
    {
        /// <summary>
        /// API Key（从阿里云控制台获取）
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// API 端点（默认为官方端点）
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// 向量化模型名称（默认: text-embedding-v3）
        /// 可选模型:
        /// - text-embedding-v3: 1024维，最新版本
        /// - text-embedding-v2: 1536维
        /// - text-embedding-v1: 1536维
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// 向量维度（根据模型自动设置，也可手动指定）
        /// text-embedding-v3: 1024
        /// text-embedding-v2: 1536
        /// text-embedding-v1: 1536
        /// </summary>
        public int VectorSize { get; set; } = 1024;
    }
}
