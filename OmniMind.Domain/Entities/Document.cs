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
    /// 文档/资源：多模态统一抽象。
    /// 无论 PDF/图片/音频/视频/网页，统一为 Document，通过 ContentType 区分。
    /// </summary>
    [Table("documents")]
    [Index(nameof(TenantId), nameof(KnowledgeBaseId), nameof(CreatedAt))]
    [Index(nameof(TenantId), nameof(FileHash))]
    [Index(nameof(TenantId), nameof(SessionId))]
    [Index(nameof(TenantId), nameof(ContentType))]
    public class Document : ITenantEntity
    {
        /// <summary>
        /// 文档主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();
        /// <summary>
        /// 租户ID（行级隔离字段）
        /// </summary>
        [Required]
        [Column("tenant_id")]
        public required string TenantId { get; set; }

        /// <summary>
        /// 所属知识库ID（可选，媒体文件/临时附件可为 NULL）
        /// </summary>
        [Column("knowledge_base_id")]
        public string? KnowledgeBaseId { get; set; }

        /// <summary>
        /// 所属知识库
        /// </summary>
        [ForeignKey(nameof(KnowledgeBaseId))]
        public KnowledgeBase KnowledgeBase { get; set; } = default!;

        /// <summary>
        /// 所属文件夹ID（可选，null 表示在知识库根目录）
        /// </summary>
        [Column("folder_id")]
        public string? FolderId { get; set; }

        /// <summary>
        /// 所属文件夹
        /// </summary>
        [ForeignKey(nameof(FolderId))]
        public Folder? Folder { get; set; }

        /// <summary>
        /// 导入归属工作空间ID（追溯"从哪个空间导入/归档"，媒体文件/临时附件可为 NULL）
        /// </summary>
        [Column("workspace_id")]
        public string? WorkspaceId { get; set; }

        /// <summary>
        /// 导入归属工作空间
        /// </summary>
        [ForeignKey(nameof(WorkspaceId))]
        public Workspace Workspace { get; set; } = default!;

        /// <summary>
        /// 标题/显示名称
        /// </summary>
        [Required]
        [Column("title")]
        public string Title { get; set; } = default!;

        /// <summary>
        /// 内容类型（MIME 类型）：application/pdf, audio/mp3, video/mp4, image/png 等
        /// </summary>
        [Required]
        [Column("content_type")]
        [MaxLength(255)]
        public string ContentType { get; set; } = default!;

        /// <summary>
        /// 来源类型（上传/URL/导入）
        /// </summary>
        [Required]
        [Column("source_type")]
        public SourceType SourceType { get; set; }

        /// <summary>
        /// 来源地址（如 URL），上传文件可为空
        /// </summary>
        [MaxLength(512)]
        [Column("source_uri")]
        public string? SourceUri { get; set; }

        /// <summary>
        /// 原文件在对象存储中的 Key（建议 tenant 分区）
        /// </summary>
        [Required]
        [Column("object_key")]
        public string ObjectKey { get; set; } = default!;

        /// <summary>
        /// 文件 Hash（用于去重/版本判断）
        /// </summary>
        [Column("file_hash")]
        public string? FileHash { get; set; }

        /// <summary>
        /// 语言（如 zh-CN / en-US），可选
        /// </summary>
        [MaxLength(16)]
        [Column("language")]
        public string? Language { get; set; }

        /// <summary>
        /// 状态：uploaded/parsing/parsed/indexing/indexed/failed
        /// </summary>
        [Required]
        [Column("status")]
        public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

        /// <summary>
        /// 失败原因（可选）
        /// </summary>
        [MaxLength(512)]
        [Column("error")]
        public string? Error { get; set; }

        /// <summary>
        /// 音频/视频时长（秒）
        /// </summary>
        [Column("duration")]
        public int? Duration { get; set; }

        /// <summary>
        /// 音频/视频转写文本
        /// </summary>
        [Column("transcription", TypeName = "longtext")]
        public string? Transcription { get; set; }

        /// <summary>
        /// 会话ID（用于关联聊天临时附件）
        /// </summary>
        [MaxLength(64)]
        [Column("session_id")]
        public string? SessionId { get; set; }

        /// <summary>
        /// 创建人用户ID（Identity 用户ID）
        /// </summary>
        [Required]
        [MaxLength(64)]
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
        /// 文档版本集合（强烈建议保留）
        /// </summary>
        public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();

        /// <summary>
        /// 切片集合（可检索单元）
        /// </summary>
        public ICollection<Chunk> Chunks { get; set; } = new List<Chunk>();

        /// <summary>
        /// 导入任务集合
        /// </summary>
        public ICollection<IngestionTask> IngestionTasks { get; set; } = new List<IngestionTask>();
    }

}
