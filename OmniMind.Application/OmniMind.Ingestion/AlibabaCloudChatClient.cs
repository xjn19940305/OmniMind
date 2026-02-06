using DocumentFormat.OpenXml.Vml.Wordprocessing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniMind.Application.Services;
using OmniMind.Entities;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

// 使用别名解决 ChatMessage 类型冲突
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 阿里云 DashScope TOKEN 使用情况
    /// </summary>
    public class AlibabaCloudChatUsageInfo
    {
        /// <summary>
        /// 输入 TOKEN 数
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// 输出 TOKEN 数
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
    /// 阿里云百练 DashScope 聊天服务
    /// 文档: https://help.aliyun.com/zh/dashscope/developer-reference/compatibility-of-openai-with-dashscope
    /// 实现 Microsoft.Extensions.AI 的 IChatClient 接口
    /// </summary>
    public sealed class AlibabaCloudChatClient : IChatClient
    {
        private readonly HttpClient httpClient;
        private readonly AlibabaCloudChatOptions options;
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<AlibabaCloudChatClient> logger;

        public AlibabaCloudChatClient(
            HttpClient httpClient,
            AlibabaCloudChatOptions options,
            IServiceProvider serviceProvider,
            ILogger<AlibabaCloudChatClient> logger)
        {
            this.httpClient = httpClient;
            this.options = options;
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            // 使用配置的模型
            var modelId = this.options.Model ?? "qwen-max";
            // 创建元数据
            Metadata = new ChatClientMetadata(modelId);
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        public ChatClientMetadata Metadata { get; }
        public AlibabaCloudChatUsageInfo LastUsageInfo { get; private set; }
        /// <summary>
        /// 获取聊天响应（非流式）
        /// </summary>
        public async Task<ChatCompletion> CompleteAsync(
            IList<AIChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            logger.LogDebug("[AlibabaCloudChat] 正在发送 {Count} 条消息", messages.Count);

            // 构建请求体
            var requestBody = BuildRequestBody(messages, options, stream: false);

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody, GetJsonOptions()),
                Encoding.UTF8,
                "application/json");

            // 发送请求
            var response = await httpClient.PostAsync("/compatible-mode/v1/chat/completions",
                jsonContent,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("[AlibabaCloudChat] API 调用失败: {StatusCode}, {Body}",
                    response.StatusCode, responseBody);
                throw new InvalidOperationException($"阿里云百练 API 调用失败: {response.StatusCode}");
            }

            // 解析响应
            return await ParseChatCompletionAsync(responseBody);
        }

        /// <summary>
        /// 获取聊天响应（流式）
        /// </summary>
        public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
            IList<AIChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            logger.LogDebug("[AlibabaCloudChat] 正在发送流式请求 {Count} 条消息", messages.Count);

            // 构建请求体
            var requestBody = BuildRequestBody(messages, options, stream: true);

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(requestBody, GetJsonOptions()),
                Encoding.UTF8,
                "application/json");

            // 发送请求（使用 ResponseHeadersRead 确保流式读取，避免缓冲整个响应）
            using var request = new HttpRequestMessage(HttpMethod.Post, "/compatible-mode/v1/chat/completions");
            // 建议显式声明 SSE
            request.Headers.Accept.ParseAdd("text/event-stream");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

            request.Content = jsonContent;

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("[AlibabaCloudChat] 流式 API 调用失败: {StatusCode}, {Body}",
                    response.StatusCode, responseBody);
                throw new InvalidOperationException($"阿里云百练流式 API 调用失败: {response.StatusCode}");
            }

            // 读取流式响应
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var sb = new StringBuilder();
            var buffer = new char[1024];
            var list = new List<AlibabaCloudChatUsageInfo>();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0) continue;

                sb.Append(buffer, 0, read);

                while (true)
                {
                    var text = sb.ToString();

                    // SSE 事件分隔：空行
                    var idx = text.IndexOf("\n\n", StringComparison.Ordinal);
                    var idx2 = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);

                    int cut;
                    if (idx >= 0 && idx2 >= 0) cut = Math.Min(idx, idx2);
                    else cut = Math.Max(idx, idx2);

                    if (cut < 0) break;

                    var rawEvent = text.Substring(0, cut);
                    var removeLen = (idx2 >= 0 && idx2 == cut) ? 4 : 2;
                    sb.Remove(0, cut + removeLen);

                    // 合并 event 内所有 data: 行
                    var dataLines = rawEvent
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.TrimStart())
                        .Where(l => l.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        .Select(l => l.Substring(5).TrimStart());

                    var data = string.Join("\n", dataLines);
                    if (string.IsNullOrWhiteSpace(data)) continue;

                    if (data.Contains("chat.completion.chunk"))
                    {
                        // 流结束，usage 已在最后的事件中记录4
                        // 检查此事件是否包含 usage 信息（通常在最后一个 chunk 中）
                        await ExtractUsageFromStreamEvent(data, options?.ModelId!);
                    }
                    var update = ParseStreamUpdate(data);
                    yield return update;
                }
            }

            // 流结束后，设置 LastUsageInfo（取最后一个或累加所有）
            if (list.Count > 0)
            {
                // 通常最后一个 chunk 包含完整的 usage 信息
                LastUsageInfo = list[^1];
            }
        }

        /// <summary>
        /// 从流式事件中提取 usage 信息并记录
        /// </summary>
        private async Task ExtractUsageFromStreamEvent(string data, string model)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(data);
                var root = jsonDoc.RootElement;

                // 检查是否有 usage 字段（不是每个 chunk 都有）
                if (root.TryGetProperty("usage", out var usageProp) && usageProp.ValueKind == JsonValueKind.Object)
                {
                    // 容错检查：确保各个字段存在
                    int inputTokens = 0;
                    int outputTokens = 0;
                    int totalTokens = 0;

                    if (usageProp.TryGetProperty("prompt_tokens", out var promptTokens) && promptTokens.ValueKind == JsonValueKind.Number)
                    {
                        inputTokens = promptTokens.GetInt32();
                    }

                    if (usageProp.TryGetProperty("completion_tokens", out var completionTokens) && completionTokens.ValueKind == JsonValueKind.Number)
                    {
                        outputTokens = completionTokens.GetInt32();
                    }

                    if (usageProp.TryGetProperty("total_tokens", out var totalTokensProp) && totalTokensProp.ValueKind == JsonValueKind.Number)
                    {
                        totalTokens = totalTokensProp.GetInt32();
                    }

                    // 只有当至少有一个有效值时才记录
                    if (inputTokens > 0 || outputTokens > 0 || totalTokens > 0)
                    {
                        string? requestId = null;
                        if (root.TryGetProperty("id", out var requestIdProp) && requestIdProp.ValueKind == JsonValueKind.String)
                        {
                            requestId = requestIdProp.GetString();
                        }

                        // 从上下文获取用户信息
                        var context = AiCallContext.Current;
                        if (context != null)
                        {
                            // 创建作用域获取 Scoped 服务
                            using var scope = serviceProvider.CreateScope();
                            var service = scope.ServiceProvider.GetRequiredService<ITokenUsageService>();
                            await service.LogLLMUsageAsync(
                                userId: context.UserId,
                                platform: AiPlatforms.Aliyun,
                                modelName: model,
                                inputTokens: inputTokens,
                                outputTokens: outputTokens,
                                requestId: requestId,
                                sessionId: context.SessionId,
                                knowledgeBaseId: context.KnowledgeBaseId);
                            logger.LogDebug("[AlibabaCloudChat] 流式 TOKEN 已记录: {InputTokens} 输入, {OutputTokens} 输出, {TotalTokens} 总计",
                                inputTokens, outputTokens, totalTokens > 0 ? totalTokens : inputTokens + outputTokens);
                        }

                        // 同时保存到 LastUsageInfo
                        LastUsageInfo = new AlibabaCloudChatUsageInfo
                        {
                            InputTokens = inputTokens,
                            OutputTokens = outputTokens,
                            TotalTokens = totalTokens > 0 ? totalTokens : inputTokens + outputTokens,
                            RequestId = requestId
                        };

                        logger.LogDebug("[AlibabaCloudChat] 流式 TOKEN 使用: {InputTokens} 输入, {OutputTokens} 输出, {TotalTokens} 总计",
                            LastUsageInfo.InputTokens, LastUsageInfo.OutputTokens, LastUsageInfo.TotalTokens);
                    }
                }
            }
            catch (JsonException ex)
            {
                logger.LogDebug("[AlibabaCloudChat] 解析 usage 信息失败: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[AlibabaCloudChat] 解析 usage 信息时发生错误");
            }
        }

        /// <summary>
        /// 获取服务对象
        /// </summary>
        public object? GetService(Type serviceType, object? serviceKey = null)
        {
            if (serviceKey is null && serviceType == typeof(ChatClientMetadata))
            {
                return Metadata;
            }

            if (serviceKey is null && serviceType?.IsInstanceOfType(this) is true)
            {
                return this;
            }

            return null;
        }

        /// <summary>
        /// 获取服务对象（泛型版本）
        /// </summary>
        public TService? GetService<TService>(object? serviceKey = null) where TService : class
        {
            return GetService(typeof(TService), serviceKey) as TService;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // HttpClient 由 DI 容器管理，这里不需要释放
        }

        #region 私有辅助方法

        /// <summary>
        /// 构建请求体
        /// </summary>
        private object BuildRequestBody(
            IList<AIChatMessage> messages,
            ChatOptions? options,
            bool stream)
        {
            var messageList = new List<object>();

            foreach (var message in messages)
            {
                string role = message.Role.Value.ToString() switch
                {
                    var r when r.Contains("User", StringComparison.OrdinalIgnoreCase) => "user",
                    var r when r.Contains("Assistant", StringComparison.OrdinalIgnoreCase) => "assistant",
                    var r when r.Contains("System", StringComparison.OrdinalIgnoreCase) => "system",
                    _ => "user"
                };

                messageList.Add(new
                {
                    role,
                    content = message.Text ?? string.Empty
                });
            }

            // 构建基础请求
            var requestData = new Dictionary<string, object?>
            {
                ["model"] = options?.ModelId ?? Metadata.ModelId,
                ["messages"] = messageList,
                ["stream"] = stream,
                ["stream_options"] = new
                {
                    include_usage = true
                }
            };
            if (options?.AdditionalProperties != null)
            {
                foreach (var item in options.AdditionalProperties)
                {
                    // 避免 null 写入 JSON
                    if (item.Value == null)
                        continue;

                    // 避免覆盖核心字段
                    if (requestData.ContainsKey(item.Key))
                        continue;

                    requestData[item.Key] = item.Value;
                }
            }

            return requestData;
        }

        /// <summary>
        /// 解析非流式响应
        /// </summary>
        private async Task<ChatCompletion> ParseChatCompletionAsync(string responseBody)
        {
            using var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            // 提取 usage 信息
            if (root.TryGetProperty("usage", out var usageProp))
            {
                LastUsageInfo = new AlibabaCloudChatUsageInfo
                {
                    InputTokens = usageProp.GetProperty("prompt_tokens").GetInt32(),
                    OutputTokens = usageProp.GetProperty("completion_tokens").GetInt32(),
                    TotalTokens = usageProp.GetProperty("total_tokens").GetInt32()
                };

                // 尝试获取 request_id
                if (root.TryGetProperty("request_id", out var requestIdProp))
                {
                    LastUsageInfo.RequestId = requestIdProp.GetString();
                }

                logger.LogDebug("[AlibabaCloudChat] TOKEN 使用: {InputTokens} 输入, {OutputTokens} 输出, {TotalTokens} 总计",
                    LastUsageInfo.InputTokens, LastUsageInfo.OutputTokens, LastUsageInfo.TotalTokens);

                // 记录 TOKEN 使用
                var context = AiCallContext.Current;
                if (context != null)
                {
                    using var scope = serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetService<ITokenUsageService>();
                    if (service != null)
                    {
                        await service.LogLLMUsageAsync(
                            userId: context.UserId,
                            platform: AiPlatforms.Aliyun,
                            modelName: Metadata.ModelId,
                            inputTokens: LastUsageInfo.InputTokens,
                            outputTokens: LastUsageInfo.OutputTokens,
                            requestId: LastUsageInfo.RequestId,
                            sessionId: context.SessionId,
                            knowledgeBaseId: context.KnowledgeBaseId);
                    }
                }
            }

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var message))
                {
                    var role = message.GetProperty("role").GetString();
                    var content = message.GetProperty("content").GetString() ?? string.Empty;

                    // 构建结果列表
                    var chatMessage = new AIChatMessage(
                        role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                        content
                    );

                    logger.LogDebug("[AlibabaCloudChat] 响应成功，内容长度: {Length}", content.Length);
                    return new ChatCompletion(new List<AIChatMessage> { chatMessage });
                }
            }

            logger.LogError("[AlibabaCloudChat] 响应格式错误: {Body}", responseBody);
            throw new InvalidOperationException("阿里云百练 API 响应格式错误");
        }

        /// <summary>
        /// 解析流式响应更新
        /// </summary>
        private StreamingChatCompletionUpdate ParseStreamUpdate(string data)
        {
            using var jsonDoc = JsonDocument.Parse(data);
            var root = jsonDoc.RootElement;

            string content = string.Empty;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];

                // 常见：delta.content
                if (choice.TryGetProperty("delta", out var delta) &&
                    delta.ValueKind == JsonValueKind.Object &&
                    delta.TryGetProperty("content", out var ce) &&
                    ce.ValueKind == JsonValueKind.String)
                {
                    content = ce.GetString() ?? string.Empty;
                }
                // 兜底：message.content（少数实现/最后一段）
                else if (choice.TryGetProperty("message", out var msg) &&
                         msg.ValueKind == JsonValueKind.Object &&
                         msg.TryGetProperty("content", out var me) &&
                         me.ValueKind == JsonValueKind.String)
                {
                    content = me.GetString() ?? string.Empty;
                }
            }

            return CreateStreamingUpdate(content);
        }

        /// <summary>
        /// 创建流式更新对象（辅助方法）
        /// </summary>
        private static StreamingChatCompletionUpdate CreateStreamingUpdate(string content)
        {
            return new StreamingChatCompletionUpdate()
            {
                Text = content
            };
        }
        /// <summary>
        /// 获取 JSON 序列化选项
        /// </summary>
        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        #endregion
    }

    /// <summary>
    /// 阿里云百练配置选项
    /// </summary>
    public class AlibabaCloudChatOptions
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
        /// 模型名称（默认: qwen-max）
        /// 可选模型:
        /// - qwen-max: 通义千问超大规模语言模型
        /// - qwen-plus: 通义千问超大规模语言模型增强版
        /// - qwen-turbo: 通义千问超大规模语言模型加速版
        /// - qwen-long: 通义千问长文本理解模型
        /// - deepseek-v3: DeepSeek V3 模型
        /// - deepseek-v3.2: DeepSeek V3.2 模型
        /// - deepseek-v3-chat: DeepSeek V3 聊天模型
        /// - deepseek-r1: DeepSeek R1 模型
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// 支持的模型列表（用于配置多模型场景）
        /// </summary>
        public string[]? Models { get; set; }

        /// <summary>
        /// 最大生成 Token 数
        /// </summary>
        public int? MaxTokens { get; set; }

        /// <summary>
        /// 温度参数（0-2），越高越随机
        /// </summary>
        public float? Temperature { get; set; }

        /// <summary>
        /// Top P 采样参数（0-1）
        /// </summary>
        public float? TopP { get; set; }

        /// <summary>
        /// 停止序列
        /// </summary>
        public IList<string>? StopSequences { get; set; }
    }
}
