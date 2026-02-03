namespace OmniMind.Messages
{
    /// <summary>
    /// 文档上传消息
    /// </summary>
    public record DocumentUploadMessage
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 对象存储Key
        /// </summary>
        public string ObjectKey { get; init; } = string.Empty;

        /// <summary>
        /// 原始文件名
        /// </summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>
        /// 内容类型（MIME 类型）
        /// </summary>
        public string ContentType { get; init; } = string.Empty;

        /// <summary>
        /// 消息创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    }
}
