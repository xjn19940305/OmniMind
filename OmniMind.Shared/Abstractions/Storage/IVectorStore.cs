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
        /// <summary>
        /// 插入或更新向量点
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="points"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task UpsertAsync(string collection, IReadOnlyList<VectorPoint> points, CancellationToken ct = default);
        /// <summary>
        /// 向量相似度搜索
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="vector"></param>
        /// <param name="options"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task<IReadOnlyList<VectorSearchHit>> SearchAsync(string collection, float[] vector, VectorSearchOptions options, CancellationToken ct = default);
        /// <summary>
        /// 按过滤条件删除
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="filter"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        Task DeleteByFilterAsync(string collection, VectorFilter filter, CancellationToken ct = default);
        /// <summary>
        /// 确保集合存在（不存在则创建）
        /// </summary>
        /// <param name="collection"></param>
        /// <param name="spec"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
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
