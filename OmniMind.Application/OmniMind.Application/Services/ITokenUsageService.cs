using OmniMind.Entities;

namespace OmniMind.Application.Services
{
    /// <summary>
    /// TOKEN 使用记录服务接口
    /// </summary>
    public interface ITokenUsageService
    {
        /// <summary>
        /// 记录大语言模型 TOKEN 消耗
        /// </summary>
        Task LogLLMUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? sessionId = null,
            string? knowledgeBaseId = null,
            string? extraJson = null);

        /// <summary>
        /// 记录视觉模型 TOKEN 消耗
        /// </summary>
        Task LogVisionUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? sessionId = null,
            string? knowledgeBaseId = null,
            string? extraJson = null);

        /// <summary>
        /// 记录全模态模型 TOKEN 消耗
        /// </summary>
        Task LogMultimodalUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? sessionId = null,
            string? knowledgeBaseId = null,
            string? extraJson = null);

        /// <summary>
        /// 记录语音模型 TOKEN 消耗
        /// </summary>
        Task LogAudioUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? documentId = null,
            string? extraJson = null);

        /// <summary>
        /// 记录向量化 TOKEN 消耗
        /// </summary>
        Task LogEmbeddingUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            string? documentId = null,
            string? knowledgeBaseId = null,
            string? requestId = null,
            string? extraJson = null);

        /// <summary>
        /// 记录重排序模型 TOKEN 消耗
        /// </summary>
        Task LogRerankerUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            string? requestId = null,
            string? extraJson = null);

        /// <summary>
        /// 通用记录方法（支持任意类型）
        /// </summary>
        Task LogUsageAsync(
            string userId,
            string platform,
            TokenUsageType usageType,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? documentId = null,
            string? knowledgeBaseId = null,
            string? sessionId = null,
            string? extraJson = null);

        /// <summary>
        /// 记录 API 调用失败
        /// </summary>
        Task LogErrorAsync(
            string userId,
            string platform,
            TokenUsageType usageType,
            string modelName,
            string? errorCode = null,
            string? errorMessage = null);

        /// <summary>
        /// 获取用户 TOKEN 消耗统计（按类型分类）
        /// </summary>
        Task<(int TotalTokens, int LlmTokens, int VisionTokens, int MultimodalTokens, int AudioTokens, int EmbeddingTokens, int RerankerTokens)> GetUserUsageAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null);

        /// <summary>
        /// 获取用户 TOKEN 消耗统计（按平台分类）
        /// </summary>
        Task<Dictionary<string, int>> GetUserUsageByPlatformAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null);

        /// <summary>
        /// 获取用户 TOKEN 消耗统计（按模型分类）
        /// </summary>
        Task<Dictionary<string, int>> GetUserUsageByModelAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null);

        /// <summary>
        /// 获取用户 TOKEN 消耗汇总统计（平台+模型）
        /// </summary>
        Task<List<TokenUsageSummary>> GetUserUsageSummaryAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null);
    }

    /// <summary>
    /// TOKEN 使用统计摘要
    /// </summary>
    public class TokenUsageSummary
    {
        /// <summary>平台</summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>模型名称</summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>消耗类型</summary>
        public TokenUsageType UsageType { get; set; }

        /// <summary>总 TOKEN 数</summary>
        public int TotalTokens { get; set; }

        /// <summary>调用次数</summary>
        public int CallCount { get; set; }
    }
}
