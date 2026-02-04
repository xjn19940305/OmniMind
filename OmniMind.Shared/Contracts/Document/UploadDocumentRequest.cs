using Microsoft.AspNetCore.Http;

namespace OmniMind.Contracts.Document
{
    /// <summary>
    /// 上传文档请求
    /// </summary>
    public class UploadDocumentRequest
    {
        /// <summary>
        /// 上传的文件
        /// </summary>
        public IFormFile File { get; set; } = null!;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; set; } = string.Empty;

        /// <summary>
        /// 文件夹ID（可选）
        /// </summary>
        public string? FolderId { get; set; }

        /// <summary>
        /// 文档标题（可选，默认使用文件名）
        /// </summary>
        public string? Title { get; set; }
    }
}
