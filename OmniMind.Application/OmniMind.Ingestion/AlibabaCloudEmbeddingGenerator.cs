using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using OmniMind.Application.Services;
using OmniMind.Entities;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 阿里云 DashScope TOKEN 使用情况
    /// </summary>
    public class AlibabaCloudUsageInfo
    {
        /// <summary>
        /// 输入 TOKEN 数
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// 输出 TOKEN 数（向量化通常为 0）
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// 总 TOKEN 数
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// 请求ID（用于追溯）
        /// </summary>
        public string? RequestId { get; set; }
    }

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
        private readonly IServiceProvider serviceProvider;

        public AlibabaCloudEmbeddingGenerator(
            HttpClient httpClient,
            AlibabaCloudOptions options,
            ILogger<AlibabaCloudEmbeddingGenerator> logger,
            IServiceProvider serviceProvider)
        {
            this.httpClient = httpClient;
            this.options = options;
            this.logger = logger;
            this.serviceProvider = serviceProvider;

            // 设置 DashScope API 地址
            this.httpClient.BaseAddress = new Uri(this.options.Endpoint ?? "https://dashscope.aliyuncs.com");

            // 使用配置的模型
            var modelId = this.options.Model ?? "text-embedding-v4";

            // 创建元数据属性
            Metadata = new EmbeddingGeneratorMetadata(
                modelId: modelId,
                dimensions: VectorSize);

            // 设置默认请求头
            // 移除已存在的 Authorization 头（避免重复添加）
            this.httpClient.DefaultRequestHeaders.Remove("Authorization");
            this.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {this.options.ApiKey}");
            // 注意：Content-Type 应该在发送请求时通过 HttpContent 设置，而不是在 DefaultRequestHeaders 中
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
        /// 获取最后请求的 TOKEN 使用情况
        /// </summary>
        public AlibabaCloudUsageInfo? LastUsageInfo { get; private set; }

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
            const int batchSize = 5;
            var allEmbeddings = new List<Embedding<float>>();
            AlibabaCloudUsageInfo? totalUsage = null;

            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();
                var (embeddings, usage) = await GenerateBatchInternalAsync(batch, cancellationToken);
                allEmbeddings.AddRange(embeddings);

                // 累加 TOKEN 使用量
                if (usage != null)
                {
                    if (totalUsage == null)
                    {
                        totalUsage = usage;
                    }
                    else
                    {
                        totalUsage.InputTokens += usage.InputTokens;
                        totalUsage.OutputTokens += usage.OutputTokens;
                        totalUsage.TotalTokens += usage.TotalTokens;
                    }
                }
            }

            LastUsageInfo = totalUsage;

            // 自动记录 TOKEN 使用（从服务容器中获取 ITokenUsageService）
            if (totalUsage != null)
            {
                // 创建作用域以获取 Scoped 服务
                using var scope = serviceProvider.CreateScope();
                var tokenUsageService = scope.ServiceProvider.GetService<OmniMind.Application.Services.ITokenUsageService>();

                if (tokenUsageService != null)
                {
                    var context = AiCallContext.Current;
                    if (context != null)
                    {
                        try
                        {
                            await tokenUsageService.LogEmbeddingUsageAsync(
                                userId: context.UserId,
                                platform: AiPlatforms.Aliyun,
                                modelName: Metadata.ModelId,
                                inputTokens: totalUsage.InputTokens,
                                documentId: context.DocumentId,
                                knowledgeBaseId: context.KnowledgeBaseId,
                                requestId: totalUsage.RequestId);

                            logger.LogDebug("[AlibabaCloudEmbedding] 已记录 TOKEN 使用: {Tokens}", totalUsage.TotalTokens);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[AlibabaCloudEmbedding] 记录 TOKEN 使用失败");
                        }
                    }
                    else
                    {
                        logger.LogWarning("[AlibabaCloudEmbedding] 未设置 EmbeddingContext，跳过 TOKEN 使用记录");
                    }
                }
            }

            return new GeneratedEmbeddings<Embedding<float>>(allEmbeddings);
        }

        /// <summary>
        /// 内部批量实现
        /// </summary>
        private async Task<(List<Embedding<float>> Embeddings, AlibabaCloudUsageInfo? Usage)> GenerateBatchInternalAsync(
            List<string> texts,
            CancellationToken cancellationToken)
        {
            try
            {
                // 构建请求体
                var requestBody = new
                {
                    model = Metadata.ModelId,
                    input = texts
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                // 发送请求
                var response = await httpClient.PostAsync("/compatible-mode/v1/embeddings",
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

                // 提取 usage 信息
                AlibabaCloudUsageInfo? usage = null;
                if (root.TryGetProperty("usage", out var usageProp))
                {
                    usage = new AlibabaCloudUsageInfo
                    {
                        InputTokens = usageProp.GetProperty("prompt_tokens").GetInt32(),
                        OutputTokens = 0,
                        TotalTokens = usageProp.GetProperty("total_tokens").GetInt32()
                    };

                    // 尝试获取 request_id
                    if (root.TryGetProperty("id", out var requestIdProp))
                    {
                        usage.RequestId = requestIdProp.GetString();
                    }

                    logger.LogDebug("[AlibabaCloudEmbedding] TOKEN 使用: {InputTokens} 输入, {OutputTokens} 输出, {TotalTokens} 总计",
                        usage.InputTokens, usage.OutputTokens, usage.TotalTokens);
                }

                // DashScope OpenAI 兼容格式响应: { "data": [{ "embedding": [...], "index": 0, "object": "embedding" }] }
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    var result = new List<Embedding<float>>();

                    foreach (var item in data.EnumerateArray())
                    {
                        if (item.TryGetProperty("embedding", out var vector))
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
                    return (result, usage);
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
