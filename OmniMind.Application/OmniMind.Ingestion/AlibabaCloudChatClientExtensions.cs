using Microsoft.Extensions.AI;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// 阿里云百练 ChatClient 扩展方法
    /// </summary>
    public static class AlibabaCloudChatClientExtensions
    {
        /// <summary>
        /// 直接创建阿里云百练聊天客户端
        /// </summary>
        /// <param name="apiKey">阿里云 API Key</param>
        /// <param name="model">模型名称（默认: qwen-max）</param>
        /// <param name="endpoint">API 端点（可选）</param>
        /// <returns>IChatClient 实例</returns>
        public static IChatClient CreateAlibabaCloudClient(
            string apiKey,
            string? model = null,
            string? endpoint = null)
        {
            var options = new AlibabaCloudChatOptions
            {
                ApiKey = apiKey,
                Model = model ?? "qwen-max",
                Endpoint = endpoint
            };

            var httpClient = new System.Net.Http.HttpClient();
            var loggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
            var logger = loggerFactory.CreateLogger(typeof(AlibabaCloudChatClient).FullName!);

            return new AlibabaCloudChatClient(httpClient, options, (Microsoft.Extensions.Logging.ILogger<AlibabaCloudChatClient>)logger);
        }
    }
}
