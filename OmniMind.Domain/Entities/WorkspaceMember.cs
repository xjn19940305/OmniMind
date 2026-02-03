using Microsoft.EntityFrameworkCore;
using OmniMind.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Entities
{
    /// <summary>
    /// 工作空间成员：用于 Workspace 级 RBAC（Owner/Admin/Member/Viewer）
    /// </summary>
    [Table("workspace_members")]
    [Index(nameof(WorkspaceId), nameof(UserId), IsUnique = true)]
    public class WorkspaceMember
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 工作空间ID
        /// </summary>
        [Required]
        [Column("workspace_id")]
        public string? WorkspaceId { get; set; }

        /// <summary>
        /// 所属工作空间
        /// </summary>
        [ForeignKey(nameof(WorkspaceId))]
        public Workspace Workspace { get; set; } = default!;

        /// <summary>
        /// 用户ID（Identity 用户ID）
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("user_id")]
        public string UserId { get; set; } = default!;

        /// <summary>
        /// 角色
        /// </summary>
        [Required]
        [Column("role")]
        public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;

        /// <summary>
        /// 加入时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
