namespace OmniMind.Abstractions.Ingestion
{
    /// <summary>
    /// 文本切片器 - 将长文本分割成可检索的语义块
    /// </summary>
    public interface IChunker
    {
        /// <summary>
        /// 将文本切分成多个切片
        /// </summary>
        /// <param name="text">待切片的文本</param>
        /// <param name="options">切片选项（可选）</param>
        /// <returns>切片列表</returns>
        List<TextChunk> Chunk(string text, ChunkingOptions? options = null);
    }

    /// <summary>
    /// 文本切片
    /// </summary>
    public class TextChunk
    {
        /// <summary>
        /// 切片索引
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// 切片内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Token 数量（估算）
        /// </summary>
        public int TokenCount { get; set; }
    }

    /// <summary>
    /// 文本切片选项
    /// </summary>
    public class ChunkingOptions
    {
        /// <summary>
        /// 每个切片最大 token 数（默认 500）
        /// </summary>
        public int MaxTokens { get; set; } = 500;

        /// <summary>
        /// 切片间重叠 token 数（默认 50）
        /// </summary>
        public int OverlapTokens { get; set; } = 50;

        /// <summary>
        /// 每个切片最小 token 数（默认 100）
        /// </summary>
        public int MinTokens { get; set; } = 100;
    }
}
