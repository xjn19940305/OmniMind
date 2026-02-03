using Microsoft.EntityFrameworkCore;
using OmniMind.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Entities
{
    /// <summary>
    /// 工作空间：协作与权限边界（个人/团队/共享）。
    /// 一个租户下可拥有多个工作空间。
    /// </summary>
    [Table("workspaces")]
    [Index(nameof(Name))]
    public class Workspace
    {
        /// <summary>
        /// 工作空间主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 工作空间名称
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Column("name")]
        public string Name { get; set; } = default!;

        /// <summary>
        /// 工作空间类型：个人/团队/共享
        /// </summary>
        [Required]
        [Column("type")]
        public WorkspaceType Type { get; set; } = WorkspaceType.Team;

        /// <summary>
        /// 所有者用户ID（Identity 用户ID）
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("owner_user_id")]
        public string OwnerUserId { get; set; } = default!;

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

        /// <summary>
        /// 工作空间与知识库的关联（一个工作空间可挂载多个知识库）
        /// </summary>
        public ICollection<KnowledgeBaseWorkspace> KnowledgeBaseLinks { get; set; } = new List<KnowledgeBaseWorkspace>();

        /// <summary>
        /// 工作空间成员
        /// </summary>
        public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();

        /// <summary>
        /// 该空间下“导入归属”的文档集合（用于追溯资源从哪个空间导入）
        /// </summary>
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
