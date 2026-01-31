using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Abstractions.Storage
{
    /// <summary>
    /// Qdrant
    /// </summary>
    public interface IVectorStore
    {
        Task UpsertAsync(string collection, IReadOnlyList<VectorPoint> points, CancellationToken ct = default);
        Task<IReadOnlyList<VectorSearchHit>> SearchAsync(string collection, float[] vector, VectorSearchOptions options, CancellationToken ct = default);
        Task DeleteByFilterAsync(string collection, VectorFilter filter, CancellationToken ct = default);
        Task EnsureCollectionAsync(string collection, VectorCollectionSpec spec, CancellationToken ct = default);
    }
    public sealed record VectorPoint(string Id, float[] Vector, IReadOnlyDictionary<string, object> Payload);
    public sealed record VectorSearchHit(string Id, float Score, IReadOnlyDictionary<string, object> Payload);

    public sealed record VectorSearchOptions(
        int Limit,
        VectorFilter? Filter = null,
        bool WithPayload = true
    );

    public sealed record VectorFilter(IReadOnlyList<VectorCondition> Must);
    public sealed record VectorCondition(string Field, string Op, object Value);

    public sealed record VectorCollectionSpec(int VectorSize, string Distance /* cosine/dot/euclid */);

}
