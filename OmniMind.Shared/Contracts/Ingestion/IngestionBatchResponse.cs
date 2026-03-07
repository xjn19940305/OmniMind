using OmniMind.Enums;
using System;
using System.Collections.Generic;

namespace OmniMind.Contracts.Ingestion
{
    public record IngestionBatchResponse
    {
        public string Id { get; init; } = string.Empty;

        public string KnowledgeBaseId { get; init; } = string.Empty;

        public IngestionSourceKind SourceKind { get; init; }

        public string SourceIdentifier { get; init; } = string.Empty;

        public string? ExternalTaskId { get; init; }

        public string? RuleVersion { get; init; }

        public int TotalCount { get; init; }

        public int SuccessCount { get; init; }

        public int FailedCount { get; init; }

        public int PendingCount { get; init; }

        public IngestionBatchStatus Status { get; init; }

        public string? ErrorSummary { get; init; }

        public string CreatedByUserId { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset StartedAt { get; init; }

        public DateTimeOffset? FinishedAt { get; init; }

        public DateTimeOffset? UpdatedAt { get; init; }

        public Dictionary<string, string?>? Metadata { get; init; }
    }
}
