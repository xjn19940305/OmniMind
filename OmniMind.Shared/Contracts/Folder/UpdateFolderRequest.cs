namespace OmniMind.Contracts.Folder
{
    /// <summary>
    /// 更新文件夹请求
    /// </summary>
    public record UpdateFolderRequest
    {
        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// 文件夹描述
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// 排序号
        /// </summary>
        public int? SortOrder { get; init; }
    }
}
