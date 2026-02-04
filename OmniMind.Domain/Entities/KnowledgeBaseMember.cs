using Microsoft.EntityFrameworkCore;
using OmniMind.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 知识库成员：用于知识库级别的 RBAC（Admin/Editor/Viewer）。
    /// Owner 不存储在成员表中，通过 KnowledgeBase.OwnerUserId 判断。
    /// </summary>
    [Table("knowledge_base_members")]
    [Index(nameof(KnowledgeBaseId), nameof(UserId), IsUnique = true)]
    [Index(nameof(KnowledgeBaseId), nameof(Role))]
    public class KnowledgeBaseMember
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
        /// 用户ID
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("user_id")]
        public string UserId { get; set; } = default!;

        /// <summary>
        /// 用户导航属性
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = default!;

        /// <summary>
        /// 成员角色：Admin/Editor/Viewer
        /// </summary>
        [Required]
        [Column("role")]
        public KnowledgeBaseMemberRole Role { get; set; } = KnowledgeBaseMemberRole.Viewer;

        /// <summary>
        /// 邀请者用户ID（可选，记录是谁邀请的）
        /// </summary>
        [MaxLength(64)]
        [Column("invited_by_user_id")]
        public string? InvitedByUserId { get; set; }

        /// <summary>
        /// 加入时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
