namespace OmniMind.Contracts.Common
{
    /// <summary>
    /// 分页响应
    /// </summary>
    public record PagedResponse<T>
    {
        /// <summary>
        /// 数据列表
        /// </summary>
        public List<T> Items { get; init; } = new();

        /// <summary>
        /// 总数量
        /// </summary>
        public int TotalCount { get; init; }

        /// <summary>
        /// 当前页
        /// </summary>
        public int Page { get; init; }

        /// <summary>
        /// 每页大小
        /// </summary>
        public int PageSize { get; init; }

        /// <summary>
        /// 提示消息（用于权限提示等）
        /// </summary>
        public string? Message { get; init; }
    }

    /// <summary>
    /// 错误响应
    /// </summary>
    public record ErrorResponse
    {
        /// <summary>
        /// 错误消息
        /// </summary>
        public string Message { get; init; } = string.Empty;
    }
}
