using OmniMind.Enums;

namespace OmniMind.Contracts.Document
{
    /// <summary>
    /// 创建文档请求
    /// </summary>
    public record CreateDocumentRequest
    {
        /// <summary>
        /// 所属知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 所属文件夹ID（可选，null 表示根目录）
        /// </summary>
        public string? FolderId { get; init; }

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
        /// 来源地址（如 URL）
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
    }
}
