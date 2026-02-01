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
    /// 导入任务：用于异步解析/切片/向量化/索引流水线的状态跟踪（可配合 SignalR 推送进度）。
    /// </summary>
    [Table("ingestion_tasks")]
    [Index(nameof(TenantId), nameof(DocumentId), nameof(Status))]
    [Index(nameof(TenantId), nameof(WorkspaceId), nameof(KnowledgeBaseId), nameof(CreatedAt))]
    public class IngestionTask : ITenantEntity
    {
        /// <summary>
        /// 主键
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
        /// 工作空间ID（任务归属空间）
        /// </summary>
        [Required]
        [Column("workspace_id")]
        public required string WorkspaceId { get; set; }

        /// <summary>
        /// 知识库ID（任务归属知识库）
        /// </summary>
        [Required]
        [Column("knowledge_base_id")]
        public required string KnowledgeBaseId { get; set; }

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
        /// 当前阶段（上传/解析/切片/向量化/索引）
        /// </summary>
        [Required]
        [Column("stage")]
        public IngestionStage Stage { get; set; } = IngestionStage.Upload;

        /// <summary>
        /// 进度（0-100）
        /// </summary>
        [Required]
        [Column("progress")]
        public int Progress { get; set; } = 0;

        /// <summary>
        /// 任务状态（Running/Success/Failed）
        /// </summary>
        [Required]
        [Column("status")]
        public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Running;

        /// <summary>
        /// 错误信息（失败时）
        /// </summary>
        [MaxLength(512)]
        [Column("error")]
        public string? Error { get; set; }

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
    }
}
