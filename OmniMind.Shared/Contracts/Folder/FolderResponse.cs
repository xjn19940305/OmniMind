namespace OmniMind.Contracts.Folder
{
    /// <summary>
    /// 文件夹响应
    /// </summary>
    public record FolderResponse
    {
        /// <summary>
        /// 文件夹ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 知识库名称
        /// </summary>
        public string? KnowledgeBaseName { get; init; }

        /// <summary>
        /// 父文件夹ID
        /// </summary>
        public string? ParentFolderId { get; init; }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 文件夹路径
        /// </summary>
        public string? Path { get; init; }

        /// <summary>
        /// 文件夹描述
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int SortOrder { get; init; }

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
        /// 子文件夹数量
        /// </summary>
        public int ChildFolderCount { get; init; }

        /// <summary>
        /// 文档数量
        /// </summary>
        public int DocumentCount { get; init; }
    }

    /// <summary>
    /// 文件夹树形响应（包含子文件夹）
    /// </summary>
    public record FolderTreeResponse
    {
        /// <summary>
        /// 文件夹ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 父文件夹ID
        /// </summary>
        public string? ParentFolderId { get; init; }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 文件夹描述
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int SortOrder { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 子文件夹
        /// </summary>
        public List<FolderTreeResponse> Children { get; init; } = new();

        /// <summary>
        /// 文档数量
        /// </summary>
        public int DocumentCount { get; init; }
    }
}
