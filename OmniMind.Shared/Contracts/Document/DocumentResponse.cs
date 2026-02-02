using OmniMind.Enums;

namespace OmniMind.Contracts.Document
{
    /// <summary>
    /// 文档响应
    /// </summary>
    public record DocumentResponse
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 所属知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 所属文件夹ID
        /// </summary>
        public string? FolderId { get; init; }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string? FolderName { get; init; }

        /// <summary>
        /// 文档标题
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// 内容类型（MIME 类型）
        /// </summary>
        public string ContentType { get; init; } = string.Empty;

        /// <summary>
        /// 来源类型
        /// </summary>
        public SourceType SourceType { get; init; }

        /// <summary>
        /// 来源地址
        /// </summary>
        public string? SourceUri { get; init; }

        /// <summary>
        /// 对象存储 Key
        /// </summary>
        public string ObjectKey { get; init; } = string.Empty;

        /// <summary>
        /// 文件 Hash
        /// </summary>
        public string? FileHash { get; init; }

        /// <summary>
        /// 语言
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// 状态
        /// </summary>
        public DocumentStatus Status { get; init; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// 音频/视频时长（秒）
        /// </summary>
        public int? Duration { get; init; }

        /// <summary>
        /// 音频/视频转写文本
        /// </summary>
        public string? Transcription { get; init; }

        /// <summary>
        /// 会话ID
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// 创建人用户ID
        /// </summary>
        public string CreatedByUserId { get; init; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>
        /// 切片数量
        /// </summary>
        public int ChunkCount { get; init; }
    }
}
