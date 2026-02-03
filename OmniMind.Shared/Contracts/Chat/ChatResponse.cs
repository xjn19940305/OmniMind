namespace OmniMind.Contracts.Chat
{
    /// <summary>
    /// 聊天响应
    /// </summary>
    public record ChatResponse
    {
        /// <summary>
        /// 助手的回复内容
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// 使用的模型
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// 使用的 Token 数量（可选）
        /// </summary>
        public TokenUsage? Usage { get; init; }

        /// <summary>
        /// 检索到的相关文档（仅带文档聊天时返回）
        /// </summary>
        public List<RetrievedDocument>? RetrievedDocuments { get; init; }
    }

    /// <summary>
    /// Token 使用情况
    /// </summary>
    public record TokenUsage
    {
        /// <summary>
        /// 输入 Token 数
        /// </summary>
        public int PromptTokens { get; init; }

        /// <summary>
        /// 输出 Token 数
        /// </summary>
        public int CompletionTokens { get; init; }

        /// <summary>
        /// 总 Token 数
        /// </summary>
        public int TotalTokens { get; init; }
    }

    /// <summary>
    /// 检索到的文档
    /// </summary>
    public record RetrievedDocument
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 文档标题
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// 文档内容片段
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// 相似度分数
        /// </summary>
        public float Score { get; init; }

        /// <summary>
        /// 所属文档ID
        /// </summary>
        public string? DocumentId { get; init; }

        /// <summary>
        /// 所属知识库ID
        /// </summary>
        public string? KnowledgeBaseId { get; init; }
    }
}
