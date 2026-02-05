using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// TOKEN 消耗记录表
    /// 用于记录用户调用各类 AI 服务时消耗的 TOKEN 数量
    /// 支持多平台、多模型类型的统一记录
    /// </summary>
    [Table("token_usage_logs")]
    [Index(nameof(UserId))]
    [Index(nameof(Platform))]
    [Index(nameof(UsageType))]
    [Index(nameof(CreatedAt))]
    [Index(nameof(UserId), nameof(CreatedAt))]
    [Index(nameof(UserId), nameof(Platform), nameof(CreatedAt))]
    public class TokenUsageLog
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 用户ID（谁消耗的）
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("user_id")]
        public required string UserId { get; set; }

        /// <summary>
        /// 租户ID（预留字段，当前不使用）
        /// </summary>
        [MaxLength(64)]
        [Column("tenant_id")]
        public string? TenantId { get; set; }

        /// <summary>
        /// 平台/服务商（如 aliyun、openai、azure、anthropic、baidu、tencent 等）
        /// </summary>
        [Required]
        [MaxLength(32)]
        [Column("platform")]
        public required string Platform { get; set; }

        /// <summary>
        /// 消耗类型（大语言模型、视觉模型、全模态模型、语音模型、向量模型等）
        /// </summary>
        [Required]
        [Column("usage_type")]
        public TokenUsageType UsageType { get; set; }

        /// <summary>
        /// 模型名称/版本（如 qwen-max、gpt-4o、claude-3-5-sonnet、text-embedding-v3 等）
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Column("model_name")]
        public required string ModelName { get; set; }

        /// <summary>
        /// 输入 TOKEN 数
        /// </summary>
        [Required]
        [Column("input_tokens")]
        public int InputTokens { get; set; }

        /// <summary>
        /// 输出 TOKEN 数（部分模型只有输入，此时为 0）
        /// </summary>
        [Required]
        [Column("output_tokens")]
        public int OutputTokens { get; set; }

        /// <summary>
        /// 总 TOKEN 数（input_tokens + output_tokens）
        /// </summary>
        [Required]
        [Column("total_tokens")]
        public int TotalTokens { get; set; }

        /// <summary>
        /// 关联的文档ID（向量化、语音转写等场景记录）
        /// </summary>
        [MaxLength(64)]
        [Column("document_id")]
        public string? DocumentId { get; set; }

        /// <summary>
        /// 关联的知识库ID（可选，用于统计分析）
        /// </summary>
        [MaxLength(64)]
        [Column("knowledge_base_id")]
        public string? KnowledgeBaseId { get; set; }

        /// <summary>
        /// 会话ID（聊天时记录，用于关联对话）
        /// </summary>
        [MaxLength(64)]
        [Column("session_id")]
        public string? SessionId { get; set; }

        /// <summary>
        /// 请求ID（各平台返回的请求ID，用于追溯和排查问题）
        /// </summary>
        [MaxLength(128)]
        [Column("request_id")]
        public string? RequestId { get; set; }

        /// <summary>
        /// 是否成功（true: 成功, false: 失败不扣费但记录日志）
        /// </summary>
        [Required]
        [Column("is_success")]
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 错误代码（失败时记录平台返回的错误码）
        /// </summary>
        [MaxLength(64)]
        [Column("error_code")]
        public string? ErrorCode { get; set; }

        /// <summary>
        /// 错误信息（失败时记录）
        /// </summary>
        [MaxLength(512)]
        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 备注信息（可选，用于记录额外信息）
        /// </summary>
        [MaxLength(512)]
        [Column("remarks")]
        public string? Remarks { get; set; }

        /// <summary>
        /// 扩展信息（JSON格式，用于存储平台特定的额外数据）
        /// 如：计费信息、请求耗时、模型参数等
        /// </summary>
        [Column("extra_json", TypeName = "text")]
        public string? ExtraJson { get; set; }

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// TOKEN 消耗类型枚举
    /// </summary>
    public enum TokenUsageType
    {
        /// <summary>
        /// 大语言模型（文本生成、对话等）
        /// 如：qwen-max、gpt-4、claude-3 等
        /// </summary>
        LLM = 1,

        /// <summary>
        /// 视觉模型（图像理解、OCR、图像描述等）
        /// 如：gpt-4o-vision、qwen-vl-max 等
        /// </summary>
        Vision = 2,

        /// <summary>
        /// 全模态模型（同时支持文本、图像、音频等多模态输入）
        /// 如：gpt-4o、gemini-1.5-pro 等
        /// </summary>
        Multimodal = 3,

        /// <summary>
        /// 语音模型（语音识别、语音合成等）
        /// 如：whisper、paraformer 等
        /// </summary>
        Audio = 4,

        /// <summary>
        /// 向量模型（文本向量化、嵌入生成）
        /// 如：text-embedding-v3、text-embedding-ada-002 等
        /// </summary>
        Embedding = 5,

        /// <summary>
        /// 重排序模型（RAG 检索结果重排序）
        /// 如：bge-reranker、cohere-rerank 等
        /// </summary>
        Reranker = 6
    }

    /// <summary>
    /// 常用 AI 平台常量
    /// </summary>
    public static class AiPlatforms
    {
        /// <summary>阿里云百练 DashScope</summary>
        public const string Aliyun = "aliyun";

        /// <summary>OpenAI</summary>
        public const string OpenAI = "openai";

        /// <summary>微软 Azure OpenAI</summary>
        public const string Azure = "azure";

        /// <summary>Anthropic Claude</summary>
        public const string Anthropic = "anthropic";

        /// <summary>百度文心一言</summary>
        public const string Baidu = "baidu";

        /// <summary>腾讯混元</summary>
        public const string Tencent = "tencent";

        /// <summary>智谱 AI</summary>
        public const string Zhipu = "zhipu";

        /// <summary>月之暗面 Kimi</summary>
        public const string Moonshot = "moonshot";

        /// <summary>深度求索 DeepSeek</summary>
        public const string DeepSeek = "deepseek";

        /// <summary>零一万物 01.AI</summary>
        public const string Yi = "yi";

        /// <summary>其他/自定义</summary>
        public const string Other = "other";
    }
}
