namespace OmniMind.Contracts.KnowledgeBase
{
    /// <summary>
    /// 挂载知识库请求
    /// </summary>
    public record MountKnowledgeBaseRequest
    {
        /// <summary>
        /// 工作空间ID
        /// </summary>
        public string? WorkspaceId { get; init; }

        /// <summary>
        /// 别名
        /// </summary>
        public string? AliasName { get; init; }

        /// <summary>
        /// 排序
        /// </summary>
        public int? SortOrder { get; init; }
    }
}
