using Microsoft.EntityFrameworkCore;
using OmniMind.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 导入批次：用于跟踪一次批量导入任务。
    /// </summary>
    [Table("ingestion_batches")]
    [Index(nameof(KnowledgeBaseId), nameof(CreatedAt))]
    [Index(nameof(CreatedByUserId), nameof(CreatedAt))]
    public class IngestionBatch
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        [Required]
        [Column("knowledge_base_id")]
        public string KnowledgeBaseId { get; set; } = default!;

        [ForeignKey(nameof(KnowledgeBaseId))]
        public KnowledgeBase KnowledgeBase { get; set; } = default!;

        [Required]
        [Column("source_kind")]
        public IngestionSourceKind SourceKind { get; set; }

        [Required]
        [MaxLength(256)]
        [Column("source_identifier")]
        public string SourceIdentifier { get; set; } = default!;

        [MaxLength(128)]
        [Column("external_task_id")]
        public string? ExternalTaskId { get; set; }

        [MaxLength(64)]
        [Column("rule_version")]
        public string? RuleVersion { get; set; }

        [Column("total_count")]
        public int TotalCount { get; set; }

        [Column("success_count")]
        public int SuccessCount { get; set; }

        [Column("failed_count")]
        public int FailedCount { get; set; }

        [Required]
        [Column("status")]
        public IngestionBatchStatus Status { get; set; } = IngestionBatchStatus.Running;

        [MaxLength(512)]
        [Column("error_summary")]
        public string? ErrorSummary { get; set; }

        [Column("metadata_json", TypeName = "text")]
        public string? MetadataJson { get; set; }

        [Required]
        [MaxLength(64)]
        [Column("created_by_user_id")]
        public string CreatedByUserId { get; set; } = default!;

        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Required]
        [Column("started_at")]
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

        [Column("finished_at")]
        public DateTimeOffset? FinishedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
