namespace OmniMind.Contracts.Folder
{
    /// <summary>
    /// 创建文件夹请求
    /// </summary>
    public record CreateFolderRequest
    {
        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 父文件夹ID（null 表示根目录）
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
        public int SortOrder { get; init; } = 0;
    }
}
