using OmniMind.Enums;

namespace OmniMind.Contracts.Workspace
{
    /// <summary>
    /// 工作空间响应
    /// </summary>
    public record WorkspaceResponse
    {
        /// <summary>
        /// 工作空间ID
        /// </summary>
        public string? Id { get; init; }

        /// <summary>
        /// 工作空间名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 工作空间类型
        /// </summary>
        public WorkspaceType Type { get; init; }

        /// <summary>
        /// 所有者用户ID
        /// </summary>
        public string OwnerUserId { get; init; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }
    }

    /// <summary>
    /// 工作空间详情响应
    /// </summary>
    public record WorkspaceDetailResponse
    {
        /// <summary>
        /// 工作空间ID
        /// </summary>
        public string? Id { get; init; }

        /// <summary>
        /// 工作空间名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 工作空间类型
        /// </summary>
        public WorkspaceType Type { get; init; }

        /// <summary>
        /// 所有者用户ID
        /// </summary>
        public string OwnerUserId { get; init; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTimeOffset? UpdatedAt { get; init; }

        /// <summary>
        /// 知识库数量
        /// </summary>
        public int KnowledgeBaseCount { get; init; }

        /// <summary>
        /// 成员数量
        /// </summary>
        public int MemberCount { get; init; }

        /// <summary>
        /// 知识库列表
        /// </summary>
        public List<KnowledgeBaseRef>? KnowledgeBases { get; init; }

        /// <summary>
        /// 成员列表
        /// </summary>
        public List<MemberRef>? Members { get; init; }
    }

    /// <summary>
    /// 知识库引用
    /// </summary>
    public record KnowledgeBaseRef
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
        /// 别名
        /// </summary>
        public string? AliasName { get; init; }

        /// <summary>
        /// 排序
        /// </summary>
        public int SortOrder { get; init; }
    }

    /// <summary>
    /// 成员引用
    /// </summary>
    public record MemberRef
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public WorkspaceRole Role { get; init; }

        /// <summary>
        /// 加入时间
        /// </summary>
        public DateTimeOffset JoinedAt { get; init; }
    }

    /// <summary>
    /// 工作空间成员响应
    /// </summary>
    public record WorkspaceMemberResponse
    {
        /// <summary>
        /// 成员ID
        /// </summary>
        public string? Id { get; init; }

        /// <summary>
        /// 工作空间ID
        /// </summary>
        public string? WorkspaceId { get; init; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public WorkspaceRole Role { get; init; }

        /// <summary>
        /// 加入时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }
    }
}
