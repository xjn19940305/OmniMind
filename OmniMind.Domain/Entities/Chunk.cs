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
    /// 切片：统一可检索单元（文本/图片OCR/音频分段/视频转写+关键帧描述等）。
    /// </summary>
    [Table("chunks")]
    [Index(nameof(DocumentId), nameof(Version), nameof(ChunkIndex), IsUnique = true)]
    public class Chunk
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
        /// 对应文档版本号（对应 DocumentVersion.Version）
        /// </summary>
        [Required]
        [Column("version")]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 切片顺序号（从 0 或 1 开始均可，建议从 0）
        /// </summary>
        [Required]
        [Column("chunk_index")]
        public int ChunkIndex { get; set; } = 0;

        /// <summary>
        /// 父切片ID（用于层级切片/TreeRAG）
        /// </summary>
        [Column("parent_chunk_id")]
        public string? ParentChunkId { get; set; }

        /// <summary>
        /// 父切片导航属性（可选）
        /// </summary>
        [ForeignKey(nameof(ParentChunkId))]
        public Chunk? ParentChunk { get; set; }

        /// <summary>
        /// 可检索内容（统一为文本）
        /// </summary>
        [Required]
        [Column("content", TypeName = "longtext")]
        public string Content { get; set; } = default!;

        /// <summary>
        /// 扩展信息（JSON）：speaker/time/ocr bbox/图表结构等
        /// </summary>
        [Column("extra_json", TypeName = "longtext")]
        public string? ExtraJson { get; set; }

        /// <summary>
        /// 该切片 Token 数（可选，用于成本估算与截断）
        /// </summary>
        [Column("token_count")]
        public int? TokenCount { get; set; }

        /// <summary>
        /// 音视频切片开始时间（毫秒）
        /// </summary>
        [Column("start_ms")]
        public int? StartMs { get; set; }

        /// <summary>
        /// 音视频切片结束时间（毫秒）
        /// </summary>
        [Column("end_ms")]
        public int? EndMs { get; set; }

        /// <summary>
        /// 向量索引点ID（可选）：保存 Qdrant point id，便于定向删除/重建
        /// </summary>
        [MaxLength(128)]
        [Column("vector_point_id")]
        public string? VectorPointId { get; set; }

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset DateCreated { get; set; } = DateTimeOffset.UtcNow;
    }

}
