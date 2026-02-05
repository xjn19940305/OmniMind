using OmniMind.Enums;

namespace OmniMind.Contracts.KnowledgeBase
{
    /// <summary>
    /// 知识库响应
    /// </summary>
    public record KnowledgeBaseResponse
    {
        /// <summary>
        /// 知识库ID
        /// </summary>
        public string? Id { get; init; }

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
        public Visibility Visibility { get; init; }

        /// <summary>
        /// 索引配置ID
        /// </summary>
        public long? IndexProfileId { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>
        /// 拥有者用户ID
        /// </summary>
        public string? OwnerUserId { get; init; }

        /// <summary>
        /// 拥有者名称
        /// </summary>
        public string? OwnerName { get; init; }

        /// <summary>
        /// 成员数量
        /// </summary>
        public int MemberCount { get; init; }
    }
}
