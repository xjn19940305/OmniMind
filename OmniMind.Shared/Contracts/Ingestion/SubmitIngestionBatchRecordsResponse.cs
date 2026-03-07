using OmniMind.Enums;
using System.Collections.Generic;

namespace OmniMind.Contracts.Ingestion
{
    public record SubmitIngestionBatchRecordsResponse
    {
        public string BatchId { get; init; } = string.Empty;

        public int AcceptedCount { get; init; }

        public int SkippedCount { get; init; }

        public List<string> DocumentIds { get; init; } = new();

        public IngestionBatchStatus Status { get; init; }
    }
}
