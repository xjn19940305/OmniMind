using OmniMind.Enums;
using System.Collections.Generic;

namespace OmniMind.Contracts.Ingestion
{
    public record CreateIngestionBatchRequest
    {
        public string KnowledgeBaseId { get; init; } = string.Empty;

        public IngestionSourceKind SourceKind { get; init; }

        public string SourceIdentifier { get; init; } = string.Empty;

        public string? ExternalTaskId { get; init; }

        public string? RuleVersion { get; init; }

        public Dictionary<string, string?>? Metadata { get; init; }
    }
}
