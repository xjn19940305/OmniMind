using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 文件夹：知识库内的组织单位，支持树形层级结构。
    /// 用于对文档进行分组和管理。
    /// </summary>
    [Table("folders")]
    [Index(nameof(KnowledgeBaseId), nameof(ParentFolderId))]
    [Index(nameof(KnowledgeBaseId), nameof(ParentFolderId), nameof(Name), Name = "idx_folder_lookup")]
    public class Folder
    {
        /// <summary>
        /// 文件夹主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 所属知识库ID
        /// </summary>
        [Required]
        [Column("knowledge_base_id")]
        public string KnowledgeBaseId { get; set; } = default!;

        /// <summary>
        /// 所属知识库
        /// </summary>
        [ForeignKey(nameof(KnowledgeBaseId))]
        public KnowledgeBase KnowledgeBase { get; set; } = default!;

        /// <summary>
        /// 父文件夹ID（null 表示根目录）
        /// </summary>
        [Column("parent_folder_id")]
        public string? ParentFolderId { get; set; }

        /// <summary>
        /// 父文件夹
        /// </summary>
        [ForeignKey(nameof(ParentFolderId))]
        public Folder? ParentFolder { get; set; }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        [Required]
        [Column("name")]
        public string Name { get; set; } = default!;

        /// <summary>
        /// 文件夹路径（用于快速查询，如：/技术文档/前端/）
        /// </summary>
        [Column("path")]
        public string? Path { get; set; }

        /// <summary>
        /// 文件夹描述
        /// </summary>
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>
        /// 排序号（同级文件夹按此排序，越小越靠前）
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// 创建人用户ID
        /// </summary>
        [Required]
        [Column("created_by_user_id")]
        public string CreatedByUserId { get; set; } = default!;

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
        /// 子文件夹集合
        /// </summary>
        public ICollection<Folder> ChildFolders { get; set; } = new List<Folder>();

        /// <summary>
        /// 文件夹下的文档集合
        /// </summary>
        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
