using Microsoft.EntityFrameworkCore;
using OmniMind.Entities;
using OmniMind.Persistence.PostgreSql;

namespace OmniMind.Application.Services
{
    /// <summary>
    /// TOKEN 使用记录服务实现
    /// </summary>
    public class TokenUsageService : ITokenUsageService
    {
        private readonly OmniMindDbContext _dbContext;

        public TokenUsageService(OmniMindDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 记录大语言模型 TOKEN 消耗
        /// </summary>
        public async Task LogLLMUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? sessionId = null,
            string? knowledgeBaseId = null,
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = TokenUsageType.LLM,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                SessionId = sessionId,
                KnowledgeBaseId = knowledgeBaseId,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 记录视觉模型 TOKEN 消耗
        /// </summary>
        public async Task LogVisionUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? sessionId = null,
            string? knowledgeBaseId = null,
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = TokenUsageType.Vision,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                SessionId = sessionId,
                KnowledgeBaseId = knowledgeBaseId,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 记录全模态模型 TOKEN 消耗
        /// </summary>
        public async Task LogMultimodalUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? sessionId = null,
            string? knowledgeBaseId = null,
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = TokenUsageType.Multimodal,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                SessionId = sessionId,
                KnowledgeBaseId = knowledgeBaseId,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 记录语音模型 TOKEN 消耗
        /// </summary>
        public async Task LogAudioUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            int outputTokens,
            string? requestId = null,
            string? documentId = null,
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = TokenUsageType.Audio,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                DocumentId = documentId,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 记录向量化 TOKEN 消耗
        /// </summary>
        public async Task LogEmbeddingUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            string? documentId = null,
            string? knowledgeBaseId = null,
            string? requestId = null,
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = TokenUsageType.Embedding,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = 0, // 向量化没有输出 TOKEN
                TotalTokens = inputTokens,
                DocumentId = documentId,
                KnowledgeBaseId = knowledgeBaseId,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 记录重排序模型 TOKEN 消耗
        /// </summary>
        public async Task LogRerankerUsageAsync(
            string userId,
            string platform,
            string modelName,
            int inputTokens,
            string? requestId = null,
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = TokenUsageType.Reranker,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = 0, // 重排序通常没有输出 TOKEN
                TotalTokens = inputTokens,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 通用记录方法（支持任意类型）
        /// </summary>
        public async Task LogUsageAsync(
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
            string? extraJson = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = usageType,
                ModelName = modelName,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                DocumentId = documentId,
                KnowledgeBaseId = knowledgeBaseId,
                SessionId = sessionId,
                RequestId = requestId,
                ExtraJson = extraJson,
                IsSuccess = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 记录 API 调用失败
        /// </summary>
        public async Task LogErrorAsync(
            string userId,
            string platform,
            TokenUsageType usageType,
            string modelName,
            string? errorCode = null,
            string? errorMessage = null)
        {
            var log = new TokenUsageLog
            {
                Id = Guid.CreateVersion7().ToString(),
                UserId = userId,
                Platform = platform,
                UsageType = usageType,
                ModelName = modelName,
                InputTokens = 0,
                OutputTokens = 0,
                TotalTokens = 0,
                IsSuccess = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage != null && errorMessage.Length > 512
                    ? errorMessage.Substring(0, 512)
                    : errorMessage,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.TokenUsageLogs.Add(log);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// 获取用户 TOKEN 消耗统计（按类型分类）
        /// </summary>
        public async Task<(int TotalTokens, int LlmTokens, int VisionTokens, int MultimodalTokens, int AudioTokens, int EmbeddingTokens, int RerankerTokens)> GetUserUsageAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null)
        {
            var query = _dbContext.TokenUsageLogs
                .Where(l => l.UserId == userId && l.IsSuccess);

            if (startDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= endDate.Value);
            }

            var result = await query
                .GroupBy(l => 1)
                .Select(g => new
                {
                    TotalTokens = g.Sum(l => l.TotalTokens),
                    LlmTokens = g.Where(l => l.UsageType == TokenUsageType.LLM).Sum(l => l.TotalTokens),
                    VisionTokens = g.Where(l => l.UsageType == TokenUsageType.Vision).Sum(l => l.TotalTokens),
                    MultimodalTokens = g.Where(l => l.UsageType == TokenUsageType.Multimodal).Sum(l => l.TotalTokens),
                    AudioTokens = g.Where(l => l.UsageType == TokenUsageType.Audio).Sum(l => l.TotalTokens),
                    EmbeddingTokens = g.Where(l => l.UsageType == TokenUsageType.Embedding).Sum(l => l.TotalTokens),
                    RerankerTokens = g.Where(l => l.UsageType == TokenUsageType.Reranker).Sum(l => l.TotalTokens)
                })
                .FirstOrDefaultAsync();

            if (result == null)
            {
                return (0, 0, 0, 0, 0, 0, 0);
            }

            return (result.TotalTokens, result.LlmTokens, result.VisionTokens, result.MultimodalTokens, result.AudioTokens, result.EmbeddingTokens, result.RerankerTokens);
        }

        /// <summary>
        /// 获取用户 TOKEN 消耗统计（按平台分类）
        /// </summary>
        public async Task<Dictionary<string, int>> GetUserUsageByPlatformAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null)
        {
            var query = _dbContext.TokenUsageLogs
                .Where(l => l.UserId == userId && l.IsSuccess);

            if (startDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= endDate.Value);
            }

            return await query
                .GroupBy(l => l.Platform)
                .Select(g => new { Platform = g.Key, TotalTokens = g.Sum(l => l.TotalTokens) })
                .ToDictionaryAsync(g => g.Platform, g => g.TotalTokens);
        }

        /// <summary>
        /// 获取用户 TOKEN 消耗统计（按模型分类）
        /// </summary>
        public async Task<Dictionary<string, int>> GetUserUsageByModelAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null)
        {
            var query = _dbContext.TokenUsageLogs
                .Where(l => l.UserId == userId && l.IsSuccess);

            if (startDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= endDate.Value);
            }

            return await query
                .GroupBy(l => l.ModelName)
                .Select(g => new { ModelName = g.Key, TotalTokens = g.Sum(l => l.TotalTokens) })
                .ToDictionaryAsync(g => g.ModelName, g => g.TotalTokens);
        }

        /// <summary>
        /// 获取用户 TOKEN 消耗汇总统计（平台+模型）
        /// </summary>
        public async Task<List<TokenUsageSummary>> GetUserUsageSummaryAsync(
            string userId,
            DateTimeOffset? startDate = null,
            DateTimeOffset? endDate = null)
        {
            var query = _dbContext.TokenUsageLogs
                .Where(l => l.UserId == userId && l.IsSuccess);

            if (startDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(l => l.CreatedAt <= endDate.Value);
            }

            return await query
                .GroupBy(l => new { l.Platform, l.ModelName, l.UsageType })
                .Select(g => new TokenUsageSummary
                {
                    Platform = g.Key.Platform,
                    ModelName = g.Key.ModelName,
                    UsageType = g.Key.UsageType,
                    TotalTokens = g.Sum(l => l.TotalTokens),
                    CallCount = g.Count()
                })
                .OrderByDescending(s => s.TotalTokens)
                .ToListAsync();
        }
    }
}
