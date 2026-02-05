namespace OmniMind.Contracts.Config
{
    /// <summary>
    /// 模型配置响应
    /// </summary>
    public record ModelConfigResponse
    {
        /// <summary>
        /// 聊天模型列表
        /// </summary>
        public List<string> ChatModels { get; init; } = new();

        /// <summary>
        /// 向量模型
        /// </summary>
        public string EmbeddingModel { get; init; } = string.Empty;

        /// <summary>
        /// 向量维度
        /// </summary>
        public int VectorSize { get; init; }

        /// <summary>
        /// 最大 Token 数
        /// </summary>
        public int MaxTokens { get; init; }

        /// <summary>
        /// 温度参数
        /// </summary>
        public double Temperature { get; init; }

        /// <summary>
        /// TopP 参数
        /// </summary>
        public double TopP { get; init; }
    }

    /// <summary>
    /// 单个模型详情响应（兼容旧接口）
    /// </summary>
    public record ModelDetailResponse
    {
        /// <summary>
        /// 模型ID
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// 模型描述
        /// </summary>
        public string Description { get; init; } = string.Empty;
    }
}
