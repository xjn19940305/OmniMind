using OmniMind.Enums;

namespace OmniMind.Contracts.KnowledgeBase
{
    /// <summary>
    /// 创建知识库请求
    /// </summary>
    public record CreateKnowledgeBaseRequest
    {
        /// <summary>
        /// 知识库名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 知识库描述
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// 可见性
        /// </summary>
        public Visibility Visibility { get; init; } = Visibility.Internal;

        /// <summary>
        /// 索引配置ID
        /// </summary>
        public long? IndexProfileId { get; init; }
    }
}
