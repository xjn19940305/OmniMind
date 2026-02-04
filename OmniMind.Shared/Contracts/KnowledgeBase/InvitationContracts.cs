using OmniMind.Enums;

namespace OmniMind.Contracts.KnowledgeBase
{
    /// <summary>
    /// 创建邀请请求
    /// </summary>
    public record CreateInvitationRequest
    {
        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 被邀请人邮箱（可选）
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// 默认角色
        /// </summary>
        public KnowledgeBaseMemberRole Role { get; init; } = KnowledgeBaseMemberRole.Viewer;

        /// <summary>
        /// 是否需要审核
        /// </summary>
        public bool RequireApproval { get; init; }

        /// <summary>
        /// 有效期（天数）
        /// </summary>
        public int ExpireDays { get; init; } = 7;
    }

    /// <summary>
    /// 邀请响应
    /// </summary>
    public record InvitationResponse
    {
        /// <summary>
        /// 邀请ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 知识库ID
        /// </summary>
        public string KnowledgeBaseId { get; init; } = string.Empty;

        /// <summary>
        /// 知识库名称
        /// </summary>
        public string? KnowledgeBaseName { get; init; }

        /// <summary>
        /// 邀请码
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// 邀请链接
        /// </summary>
        public string InviteLink { get; init; } = string.Empty;

        /// <summary>
        /// 被邀请人邮箱
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// 默认角色
        /// </summary>
        public KnowledgeBaseMemberRole Role { get; init; }

        /// <summary>
        /// 是否需要审核
        /// </summary>
        public bool RequireApproval { get; init; }

        /// <summary>
        /// 邀请状态
        /// </summary>
        public InvitationStatus Status { get; init; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTimeOffset ExpiresAt { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>
        /// 申请理由
        /// </summary>
        public string? ApplicationReason { get; init; }

        /// <summary>
        /// 被邀请人用户ID
        /// </summary>
        public string? InviteeUserId { get; init; }

        /// <summary>
        /// 被邀请人用户信息
        /// </summary>
        public InviteeUserInfo? InviteeUser { get; init; }
    }

    /// <summary>
    /// 被邀请人用户信息
    /// </summary>
    public record InviteeUserInfo
    {
        public string Id { get; init; } = string.Empty;
        public string? UserName { get; init; }
        public string? NickName { get; init; }
        public string? Email { get; init; }
    }

    /// <summary>
    /// 邀请列表响应
    /// </summary>
    public record InvitationListResponse
    {
        /// <summary>
        /// 邀请列表
        /// </summary>
        public List<InvitationResponse> Invitations { get; init; } = new();

        /// <summary>
        /// 总数
        /// </summary>
        public int TotalCount { get; init; }
    }

    /// <summary>
    /// 接受/拒绝邀请请求
    /// </summary>
    public record RespondInvitationRequest
    {
        /// <summary>
        /// 邀请码
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// 是否接受（true=接受，false=拒绝）
        /// </summary>
        public bool Accept { get; init; } = true;

        /// <summary>
        /// 申请理由（当需要审核时填写）
        /// </summary>
        public string? ApplicationReason { get; init; }
    }

    /// <summary>
    /// 审核邀请请求
    /// </summary>
    public record ApprovalInvitationRequest
    {
        /// <summary>
        /// 邀请ID
        /// </summary>
        public string InvitationId { get; init; } = string.Empty;

        /// <summary>
        /// 是否通过（true=通过，false=拒绝）
        /// </summary>
        public bool Approved { get; init; } = true;
    }
}
