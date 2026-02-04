using OmniMind.Enums;

namespace OmniMind.Contracts.KnowledgeBase
{
    /// <summary>
    /// 文件项类型
    /// </summary>
    public enum FileItemType
    {
        /// <summary>
        /// 文件夹
        /// </summary>
        Folder = 1,

        /// <summary>
        /// 文档
        /// </summary>
        Document = 2
    }

    /// <summary>
    /// 文件项响应（用于文件夹+文档合并列表）
    /// </summary>
    public record FileItemResponse
    {
        /// <summary>
        /// 项ID（文件夹ID或文档ID）
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 项类型（文件夹或文档）
        /// </summary>
        public FileItemType Type { get; init; }

        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 描述（仅文件夹）
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// 内容类型MIME（仅文档，如 application/pdf, image/png 等）
        /// </summary>
        public string? ContentType { get; init; }

        /// <summary>
        /// 状态（仅文档）
        /// </summary>
        public DocumentStatus? Status { get; init; }

        /// <summary>
        /// 来源类型（仅文档）
        /// </summary>
        public SourceType? SourceType { get; init; }

        /// <summary>
        /// 文件大小（字节，仅文档，笔记、网页链接等可为空）
        /// </summary>
        public long? FileSize { get; init; }

        /// <summary>
        /// 文档内容（用于笔记、网页链接等）
        /// </summary>
        public string? Content { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }
    }

    /// <summary>
    /// 文件列表响应
    /// </summary>
    public record FileListResponse
    {
        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 当前文件夹ID（为空表示根目录）
        /// </summary>
        public string? CurrentFolderId { get; init; }

        /// <summary>
        /// 当前文件夹路径（面包屑）
        /// </summary>
        public List<FolderBreadcrumbItem> Path { get; init; } = new();

        /// <summary>
        /// 文件项列表（文件夹在前，文档在后）
        /// </summary>
        public List<FileItemResponse> Items { get; init; } = new();

        /// <summary>
        /// 文件夹数量
        /// </summary>
        public int FolderCount { get; init; }

        /// <summary>
        /// 文档数量
        /// </summary>
        public int DocumentCount { get; init; }
    }

    /// <summary>
    /// 文件夹面包屑项
    /// </summary>
    public record FolderBreadcrumbItem
    {
        /// <summary>
        /// 文件夹ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name { get; init; } = string.Empty;
    }
}
