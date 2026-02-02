namespace OmniMind.Abstractions.Ingestion
{
    /// <summary>
    /// 文件解析器 - 将各种格式的文件转换为纯文本
    /// </summary>
    public interface IFileParser
    {
        /// <summary>
        /// 解析文件为文本
        /// </summary>
        /// <param name="stream">文件流</param>
        /// <param name="contentType">文件 MIME 类型</param>
        /// <param name="documentId">文档 ID（可选，用于日志记录）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>解析后的文本内容</returns>
        Task<string> ParseAsync(
            Stream stream,
            string contentType,
            string? documentId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 检查是否支持该内容类型
        /// </summary>
        /// <param name="contentType">文件 MIME 类型</param>
        /// <returns>是否支持</returns>
        bool IsSupported(string contentType);
    }
}
