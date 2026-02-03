using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 阿里云百练 DashScope 聊天服务
    /// 文档: https://help.aliyun.com/zh/dashscope/developer-reference/compatibility-of-openai-with-dashscope
    /// 实现 Microsoft.Extensions.AI 的 IChatClient 接口
    /// </summary>
    public sealed class AlibabaCloudChatClient : IChatClient
    {
        private readonly HttpClient httpClient;
        private readonly AlibabaCloudChatOptions options;
        private readonly ILogger<AlibabaCloudChatClient> logger;

        public AlibabaCloudChatClient(
            HttpClient httpClient,
            AlibabaCloudChatOptions options,
            ILogger<AlibabaCloudChatClient> logger)
        {
            this.httpClient = httpClient;
            this.options = options;
            this.logger = logger;

            // 设置 DashScope API 地址
            this.httpClient.BaseAddress = new Uri(this.options.Endpoint ?? "https://dashscope.aliyuncs.com");

            // 使用配置的模型
            var modelId = this.options.Model ?? "qwen-max";

            // 创建元数据
            Metadata = new ChatClientMetadata(modelId);

            // 设置默认请求头
            this.httpClient.DefaultRequestHeaders.Remove("Authorization");
            this.httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {this.options.ApiKey}");
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        public ChatClientMetadata Metadata { get; }

        /// <summary>
        /// 获取聊天响应（非流式）
        /// </summary>
        public async Task<ChatCompletion> CompleteAsync(
            IList<ChatMessage> messages,
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
            return ParseChatCompletion(responseBody);
        }

        /// <summary>
        /// 获取聊天响应（流式）
        /// </summary>
        public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
            IList<ChatMessage> messages,
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

            // 发送请求
            using var response = await httpClient.PostAsync(
                "/compatible-mode/v1/chat/completions",
                jsonContent,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("[AlibabaCloudChat] 流式 API 调用失败: {StatusCode}, {Body}",
                    response.StatusCode, responseBody);
                throw new InvalidOperationException($"阿里云百练流式 API 调用失败: {response.StatusCode}");
            }

            // 读取流式响应
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // SSE 格式: data:{...} 或 data: {...}
                // 兼容带空格和不带空格的格式
                var data = line.TrimStart();
                if (data.StartsWith("data:"))
                {
                    // 移除 "data:" 或 "data: " 前缀
                    data = data.Substring(5).TrimStart();

                    if (data == "[DONE]")
                        break;

                    yield return ParseStreamUpdate(data);
                }
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
            IList<ChatMessage> messages,
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
                ["stream"] = stream
            };

            // 添加可选参数
            if (options is not null)
            {
                // 尝试获取 MaxTokens
                var maxTokensProp = options.GetType().GetProperty("MaxTokens");
                if (maxTokensProp is not null)
                {
                    var maxTokens = maxTokensProp.GetValue(options);
                    if (maxTokens is int maxTokensVal && maxTokensVal > 0)
                    {
                        requestData["max_tokens"] = maxTokensVal;
                    }
                }

                // 尝试获取 Temperature
                var tempProp = options.GetType().GetProperty("Temperature");
                if (tempProp is not null)
                {
                    var temp = tempProp.GetValue(options);
                    if (temp is float tempVal && tempVal > 0)
                    {
                        requestData["temperature"] = tempVal;
                    }
                }

                // 尝试获取 TopP
                var topPProp = options.GetType().GetProperty("TopP");
                if (topPProp is not null)
                {
                    var topP = topPProp.GetValue(options);
                    if (topP is float topPVal && topPVal > 0)
                    {
                        requestData["top_p"] = topPVal;
                    }
                }

                // 尝试获取 StopSequences
                var stopProp = options.GetType().GetProperty("StopSequences");
                if (stopProp is not null)
                {
                    var stop = stopProp.GetValue(options);
                    if (stop is IList<string> stopSequences && stopSequences.Count > 0)
                    {
                        requestData["stop"] = stopSequences;
                    }
                }
            }

            return requestData;
        }

        /// <summary>
        /// 解析非流式响应
        /// </summary>
        private ChatCompletion ParseChatCompletion(string responseBody)
        {
            using var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

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
                    var chatMessage = new ChatMessage(
                        role == "assistant" ? ChatRole.Assistant : ChatRole.User,
                        content
                    );

                    logger.LogDebug("[AlibabaCloudChat] 响应成功，内容长度: {Length}", content.Length);
                    return new ChatCompletion(new List<ChatMessage> { chatMessage });
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

            string? content = null;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.ValueKind == JsonValueKind.Array &&
                choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("delta", out var delta))
                {
                    if (delta.TryGetProperty("content", out var contentElement))
                    {
                        content = contentElement.GetString();
                    }
                }
            }

            // 创建 StreamingChatCompletionUpdate
            var update = CreateStreamingUpdate(content ?? string.Empty);

            // 记录日志（只在有内容时）
            if (!string.IsNullOrEmpty(content))
            {
                logger.LogDebug("[AlibabaCloudChat] 流式更新: {Content}", content);
            }

            return update;
        }

        /// <summary>
        /// 创建流式更新对象（辅助方法）
        /// </summary>
        private static StreamingChatCompletionUpdate CreateStreamingUpdate(string content)
        {
            // 使用反射创建并设置属性
            var update = new StreamingChatCompletionUpdate();
            var textProperty = typeof(StreamingChatCompletionUpdate).GetProperty("Text");

            if (textProperty != null)
            {
                // 总是设置 Text 属性，即使是空字符串
                textProperty.SetValue(update, content ?? string.Empty);
            }

            return update;
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
