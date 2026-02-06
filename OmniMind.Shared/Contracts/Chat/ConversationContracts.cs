namespace OmniMind.Contracts.Chat
{
    /// <summary>
    /// 会话列表响应
    /// </summary>
    public record ConversationListResponse
    {
        /// <summary>
        /// 会话列表
        /// </summary>
        public List<ConversationResponse> Conversations { get; init; } = new();

        /// <summary>
        /// 总数
        /// </summary>
        public int Total { get; init; }
    }

    /// <summary>
    /// 会话响应
    /// </summary>
    public record ConversationResponse
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 会话标题
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// 会话类型：simple/knowledge_base/document
        /// </summary>
        public string ConversationType { get; init; } = string.Empty;

        /// <summary>
        /// 关联的知识库ID（可选）
        /// </summary>
        public string? KnowledgeBaseId { get; init; }

        /// <summary>
        /// 关联的文档ID（可选）
        /// </summary>
        public string? DocumentId { get; init; }

        /// <summary>
        /// 使用的模型ID（可选）
        /// </summary>
        public string? ModelId { get; init; }

        /// <summary>
        /// 是否置顶
        /// </summary>
        public bool IsPinned { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>
        /// 消息数量
        /// </summary>
        public int MessageCount { get; init; }

        /// <summary>
        /// 最后一条消息预览
        /// </summary>
        public string? LastMessage { get; init; }

        /// <summary>
        /// 最后一条消息时间
        /// </summary>
        public DateTimeOffset? LastMessageAt { get; init; }
    }

    /// <summary>
    /// 会话详情响应（包含消息列表）
    /// </summary>
    public record ConversationDetailResponse
    {
        /// <summary>
        /// 会话ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 会话标题
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// 会话类型
        /// </summary>
        public string ConversationType { get; init; } = string.Empty;

        /// <summary>
        /// 关联的知识库ID（可选）
        /// </summary>
        public string? KnowledgeBaseId { get; init; }

        /// <summary>
        /// 关联的文档ID（可选）
        /// </summary>
        public string? DocumentId { get; init; }

        /// <summary>
        /// 使用的模型ID（可选）
        /// </summary>
        public string? ModelId { get; init; }

        /// <summary>
        /// 是否置顶
        /// </summary>
        public bool IsPinned { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>
        /// 消息列表
        /// </summary>
        public List<ChatMessageDto> Messages { get; init; } = new();
    }

    /// <summary>
    /// 聊天消息DTO
    /// </summary>
    public record ChatMessageDto
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 角色：user/assistant/system
        /// </summary>
        public string Role { get; init; } = string.Empty;

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// 状态：pending/streaming/completed/failed
        /// </summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>
        /// 错误信息（状态为failed时）
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// 关联的知识库ID（可选）
        /// </summary>
        public string? KnowledgeBaseId { get; init; }

        /// <summary>
        /// 关联的文档ID（可选）
        /// </summary>
        public string? DocumentId { get; init; }

        /// <summary>
        /// 参考文档（JSON格式，可选）
        /// </summary>
        public string? References { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 完成时间
        /// </summary>
        public DateTimeOffset? CompletedAt { get; init; }
    }

    /// <summary>
    /// 更新会话标题请求
    /// </summary>
    public record UpdateConversationTitleRequest
    {
        /// <summary>
        /// 新标题
        /// </summary>
        public string Title { get; init; } = string.Empty;
    }

    /// <summary>
    /// 置顶/取消置顶会话
    /// </summary>
    public record TogglePinRequest
    {
        /// <summary>
        /// 是否置顶
        /// </summary>
        public bool IsPinned { get; init; }
    }
}
