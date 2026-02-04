using Microsoft.EntityFrameworkCore;
using OmniMind.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 知识库邀请记录
    /// </summary>
    [Table("knowledge_base_invitations")]
    [Index(nameof(Code))]
    [Index(nameof(KnowledgeBaseId))]
    [Index(nameof(Email))]
    public class KnowledgeBaseInvitation
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 知识库ID
        /// </summary>
        [Required]
        [Column("knowledge_base_id")]
        public string KnowledgeBaseId { get; set; } = default!;

        /// <summary>
        /// 知识库导航属性
        /// </summary>
        [ForeignKey(nameof(KnowledgeBaseId))]
        public KnowledgeBase KnowledgeBase { get; set; } = default!;

        /// <summary>
        /// 邀请码（唯一）
        /// </summary>
        [Required]
        [MaxLength(32)]
        [Column("code")]
        public string Code { get; set; } = default!;

        /// <summary>
        /// 被邀请人邮箱（可选，用于精确发送）
        /// </summary>
        [MaxLength(256)]
        [Column("email")]
        public string? Email { get; set; }

        /// <summary>
        /// 默认角色（加入后的角色）
        /// </summary>
        [Required]
        [Column("role")]
        public KnowledgeBaseMemberRole Role { get; set; } = KnowledgeBaseMemberRole.Viewer;

        /// <summary>
        /// 是否需要审核
        /// </summary>
        [Required]
        [Column("require_approval")]
        public bool RequireApproval { get; set; } = false;

        /// <summary>
        /// 邀请状态
        /// </summary>
        [Required]
        [Column("status")]
        public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

        /// <summary>
        /// 邀请者用户ID
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("inviter_user_id")]
        public string InviterUserId { get; set; } = default!;

        /// <summary>
        /// 邀请者导航属性
        /// </summary>
        [ForeignKey(nameof(InviterUserId))]
        public User InviterUser { get; set; } = default!;

        /// <summary>
        /// 被邀请人用户ID（接受邀请后填写）
        /// </summary>
        [MaxLength(64)]
        [Column("invitee_user_id")]
        public string? InviteeUserId { get; set; }

        /// <summary>
        /// 被邀请人导航属性
        /// </summary>
        [ForeignKey(nameof(InviteeUserId))]
        public User? InviteeUser { get; set; }

        /// <summary>
        /// 过期时间（UTC）
        /// </summary>
        [Required]
        [Column("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// 已接受时间
        /// </summary>
        [Column("accepted_at")]
        public DateTimeOffset? AcceptedAt { get; set; }

        /// <summary>
        /// 申请理由（接受邀请时填写的说明）
        /// </summary>
        [MaxLength(1000)]
        [Column("application_reason")]
        public string? ApplicationReason { get; set; }

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 更新时间（UTC）
        /// </summary>
        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }
    }
}
