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
    /// 文档版本：用于索引重建、回溯、对比（企业级强烈建议）。
    /// </summary>
    [Table("document_versions")]
    [Index(nameof(DocumentId), nameof(Version), IsUnique = true)]
    public class DocumentVersion
    {
        /// <summary>
        /// 主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 文档ID
        /// </summary>
        [Required]
        [Column("document_id")]
        public required string DocumentId { get; set; }

        /// <summary>
        /// 文档
        /// </summary>
        [ForeignKey(nameof(DocumentId))]
        public Document Document { get; set; } = default!;

        /// <summary>
        /// 版本号（从 1 开始递增）
        /// </summary>
        [Required]
        [Column("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 该版本原文件在对象存储中的 Key
        /// </summary>
        [Required]
        [Column("object_key")]
        public string ObjectKey { get; set; } = default!;

        /// <summary>
        /// 该版本的文件 Hash
        /// </summary>
        [MaxLength(128)]
        [Column("file_hash")]
        public string? FileHash { get; set; }

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }

}
