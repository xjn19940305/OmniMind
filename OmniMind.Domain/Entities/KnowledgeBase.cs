using Microsoft.EntityFrameworkCore;
using OmniMind.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 知识库：知识域/主题域的容器（指南库、会议纪要库、FAQ库等）。
    /// 每个知识库归属于一个拥有者，支持邀请其他用户协作为成员。
    /// </summary>
    [Table("knowledge_bases")]
    [Index(nameof(Name))]
    [Index(nameof(OwnerUserId))]
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
        /// 可见性：Private(仅自己)/Internal(成员可见)/Public(公开)
        /// </summary>
        [Required]
        [Column("visibility")]
        public Visibility Visibility { get; set; } = Visibility.Private;

        /// <summary>
        /// 拥有者用户ID
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("owner_user_id")]
        public string OwnerUserId { get; set; } = default!;

        /// <summary>
        /// 拥有者导航属性
        /// </summary>
        [ForeignKey(nameof(OwnerUserId))]
        public User Owner { get; set; } = default!;

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
        /// 知识库成员集合（不包含 Owner）
        /// </summary>
        public ICollection<KnowledgeBaseMember> Members { get; set; } = new List<KnowledgeBaseMember>();

        /// <summary>
        /// 知识库下的文档集合
        /// </summary>
        public ICollection<Document> Documents { get; set; } = new List<Document>();

        /// <summary>
        /// 知识库下的文件夹集合
        /// </summary>
        public ICollection<Folder> Folders { get; set; } = new List<Folder>();
    }
}
