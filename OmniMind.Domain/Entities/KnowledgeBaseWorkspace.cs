using Microsoft.EntityFrameworkCore;
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
    /// 知识库-工作空间关联表（多对多）。
    /// 用独立主键便于后续扩展：如挂载别名、排序、权限、标签等。
    /// </summary>
    [Table("kb_workspaces")]
    [Index(nameof(KnowledgeBaseId), nameof(WorkspaceId), IsUnique = true)]
    public class KnowledgeBaseWorkspace
    {
        /// <summary>
        /// 关联主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 知识库ID
        /// </summary>
        [Required]
        [Column("knowledge_base_id")]
        public string? KnowledgeBaseId { get; set; }

        /// <summary>
        /// 关联的知识库
        /// </summary>
        [ForeignKey(nameof(KnowledgeBaseId))]
        public KnowledgeBase KnowledgeBase { get; set; } = default!;

        /// <summary>
        /// 工作空间ID
        /// </summary>
        [Required]
        [Column("workspace_id")]
        public string? WorkspaceId { get; set; }

        /// <summary>
        /// 关联的工作空间
        /// </summary>
        [ForeignKey(nameof(WorkspaceId))]
        public Workspace Workspace { get; set; } = default!;

        /// <summary>
        /// 可选：该知识库在该工作空间下展示的别名
        /// </summary>
        [MaxLength(128)]
        [Column("alias_name")]
        public string? AliasName { get; set; }

        /// <summary>
        /// 可选：该知识库在该工作空间下的排序（越小越靠前）
        /// </summary>
        [Required]
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
