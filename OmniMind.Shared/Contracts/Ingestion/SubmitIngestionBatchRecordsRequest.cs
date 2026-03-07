using System;
using System.Collections.Generic;

namespace OmniMind.Contracts.Ingestion
{
    public record SubmitIngestionBatchRecordsRequest
    {
        public List<IngestionBatchRecordRequest> Records { get; init; } = new();
    }

    public record IngestionBatchRecordRequest
    {
        public string? ExternalId { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ContentType { get; init; } = "text/plain";

        public string? SourceUri { get; init; }

        public string? SourceSystem { get; init; }

        public string? FileHash { get; init; }

        public string? Language { get; init; }

        public string? FolderId { get; init; }

        public DateTimeOffset? ContentUpdatedAt { get; init; }

        public Dictionary<string, string?>? Metadata { get; init; }
    }
}
