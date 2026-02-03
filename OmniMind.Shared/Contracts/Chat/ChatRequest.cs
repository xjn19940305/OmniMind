using Microsoft.AspNetCore.Http;

namespace OmniMind.Contracts.Chat
{
    /// <summary>
    /// 聊天请求
    /// </summary>
    public record ChatRequest
    {
        /// <summary>
        /// 会话ID（用于关联对话）
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// 用户消息
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID（可选，如果提供则使用RAG检索增强回答）
        /// </summary>
        public string? KnowledgeBaseId { get; init; }
        /// <summary>
        /// 文件ID
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// 检索的文档数量（默认 5，仅在使用知识库时有效）
        /// </summary>
        public int TopK { get; init; } = 5;

        /// <summary>
        /// 对话历史（可选）
        /// </summary>
        public List<ChatMessage>? History { get; init; }

        /// <summary>
        /// 使用的模型（可选，默认使用配置的第一个模型）
        /// </summary>
        public string? Model { get; init; }

        /// <summary>
        /// 温度参数（0-2）
        /// </summary>
        public float? Temperature { get; init; }

        /// <summary>
        /// 最大 Token 数
        /// </summary>
        public int? MaxTokens { get; init; }

        /// <summary>
        /// 流式响应（暂未实现）
        /// </summary>
        public bool Stream { get; init; } = false;
    }

    /// <summary>
    /// 流式聊天响应
    /// </summary>
    public record ChatStreamResponse
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string MessageId { get; init; } = string.Empty;

        /// <summary>
        /// 会话ID
        /// </summary>
        public string ConversationId { get; init; } = string.Empty;
    }

    /// <summary>
    /// 聊天消息
    /// </summary>
    public record ChatMessage
    {
        /// <summary>
        /// 角色：user/assistant/system
        /// </summary>
        public string Role { get; init; } = "user";

        /// <summary>
        /// 消息内容
        /// </summary>
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// 带文档的聊天请求
    /// </summary>
    public record ChatWithDocumentRequest
    {
        /// <summary>
        /// 会话ID（用于关联对话）
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// 用户消息
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 检索的文档数量（默认 5）
        /// </summary>
        public int TopK { get; init; } = 5;

        /// <summary>
        /// 对话历史（可选）
        /// </summary>
        public List<ChatMessage>? History { get; init; }

        /// <summary>
        /// 使用的模型（可选）
        /// </summary>
        public string? Model { get; init; }

        /// <summary>
        /// 温度参数（0-2）
        /// </summary>
        public float? Temperature { get; init; }

        /// <summary>
        /// 最大 Token 数
        /// </summary>
        public int? MaxTokens { get; init; }
    }

    /// <summary>
    /// 上传文件请求
    /// </summary>
    public record UploadFileRequest
    {
        /// <summary>
        /// 文件
        /// </summary>
        public IFormFile File { get; init; } = default!;

        /// <summary>
        /// 会话ID（可选，用于关联聊天）
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// 文件哈希值（可选，用于文件去重）
        /// </summary>
        public string? FileHash { get; init; }
    }

    /// <summary>
    /// 检查文件哈希请求
    /// </summary>
    public record CheckFileHashRequest
    {
        /// <summary>
        /// 文件哈希值
        /// </summary>
        public string FileHash { get; init; } = string.Empty;
    }

    /// <summary>
    /// 检查文件哈希响应（返回已存在的文件信息或null）
    /// </summary>
    public record CheckFileHashResponse
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 文件名
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 附件类型
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// 文件URL
        /// </summary>
        public string Url { get; init; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long Size { get; init; }

        /// <summary>
        /// 状态（5=已索引/已就绪）
        /// </summary>
        public int Status { get; init; }
    }

    /// <summary>
    /// 上传文件响应
    /// </summary>
    public record UploadResponse
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 文件名
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 文件类型：pdf/word/image/video/audio/markdown
        /// </summary>
        public string Type { get; init; } = string.Empty;

        /// <summary>
        /// 文件URL
        /// </summary>
        public string Url { get; init; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public int Size { get; init; }

        /// <summary>
        /// 所属会话ID
        /// </summary>
        public string SessionId { get; init; } = string.Empty;
    }
}
