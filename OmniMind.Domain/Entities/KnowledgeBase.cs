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
    /// 知识库：知识域/主题域的容器（指南库、会议纪要库、FAQ库等）。
    /// 一个知识库可挂载到多个工作空间（多对多）。
    /// </summary>
    [Table("knowledge_bases")]
    [Index(nameof(Name))]
    public class KnowledgeBase
    {
        /// <summary>
        /// 知识库主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 知识库名称
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Column("name")]
        public string Name { get; set; } = default!;

        /// <summary>
        /// 知识库描述
        /// </summary>
        [MaxLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>
        /// 可见性：private/internal/public
        /// </summary>
        [Required]
        [Column("visibility")]
        public Visibility Visibility { get; set; } = Visibility.Internal;

        /// <summary>
        /// 索引配置ID（预留）：记录 embedding 模型、向量维度、切片策略等
        /// </summary>
        [Column("index_profile_id")]
        public long? IndexProfileId { get; set; }

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
        /// 知识库挂载到的工作空间关系集合
        /// </summary>
        public ICollection<KnowledgeBaseWorkspace> WorkspaceLinks { get; set; } = new List<KnowledgeBaseWorkspace>();

        /// <summary>
        /// 知识库下的文档集合
        /// </summary>
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
