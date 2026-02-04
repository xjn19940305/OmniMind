using OmniMind.Enums;

namespace OmniMind.Contracts.KnowledgeBase
{
    /// <summary>
    /// 添加知识库成员请求
    /// </summary>
    public record AddKnowledgeBaseMemberRequest
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public KnowledgeBaseMemberRole Role { get; init; } = KnowledgeBaseMemberRole.Viewer;
    }

    /// <summary>
    /// 更新知识库成员请求
    /// </summary>
    public record UpdateKnowledgeBaseMemberRequest
    {
        /// <summary>
        /// 角色
        /// </summary>
        public KnowledgeBaseMemberRole Role { get; init; }
    }

    /// <summary>
    /// 知识库成员响应
    /// </summary>
    public record KnowledgeBaseMemberResponse
    {
        /// <summary>
        /// 成员ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public KnowledgeBaseMemberRole Role { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }
    }

    /// <summary>
    /// 知识库详情响应（继承自基础响应）
    /// </summary>
    public record KnowledgeBaseDetailResponse : KnowledgeBaseResponse
    {
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

        /// <summary>
        /// 成员列表
        /// </summary>
        public List<MemberRef> Members { get; init; } = new();
    }

    /// <summary>
    /// 成员引用
    /// </summary>
    public record MemberRef
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 用户名称
        /// </summary>
        public string? UserName { get; set; }

        /// <summary>
        /// 角色
        /// </summary>
        public KnowledgeBaseMemberRole Role { get; set; }

        /// <summary>
        /// 加入时间
        /// </summary>
        public DateTimeOffset JoinedAt { get; set; }
    }
}
