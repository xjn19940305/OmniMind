namespace OmniMind.Contracts.Folder
{
    /// <summary>
    /// 移动文件夹请求
    /// </summary>
    public record MoveFolderRequest
    {
        /// <summary>
        /// 新的父文件夹ID（null 表示移到根目录）
        /// </summary>
        public string? ParentFolderId { get; init; }

        /// <summary>
        /// 新的排序号
        /// </summary>
        public int? SortOrder { get; init; }
    }
}
