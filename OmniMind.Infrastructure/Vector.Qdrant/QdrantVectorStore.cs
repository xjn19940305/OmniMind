using Qdrant.Client;
using Qdrant.Client.Grpc;
using Microsoft.Extensions.Options;
using OmniMind.Abstractions.Storage;
using static Qdrant.Client.Grpc.Conditions;
using GrpcRange = Qdrant.Client.Grpc.Range;

namespace OmniMind.Vector.Qdrant
{
    public class QdrantVectorStore : IVectorStore
    {
        private readonly QdrantClient client;
        private readonly QdrantOptions options;

        public QdrantVectorStore(
            QdrantClient client,
            IOptions<QdrantOptions> options
            )
        {
            this.client = client;
            this.options = options.Value;
        }

        public async Task UpsertAsync(string collection, IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            var qdrantPoints = new List<PointStruct>();
            foreach (var p in points)
            {
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = p.Id },
                    Vectors = p.Vector.ToArray()
                };
                foreach (var kvp in p.Payload)
                {
                    point.Payload.Add(kvp.Key, ToValue(kvp.Value));
                }
                qdrantPoints.Add(point);
            }

            await client.UpsertAsync(collectionName, qdrantPoints, cancellationToken: ct);
        }

        public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(string collection, float[] vector, VectorSearchOptions options, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            var filter = options.Filter != null ? ToQdrantFilter(options.Filter) : null;

            var searchResult = await client.SearchAsync(
                collectionName,
                vector,
                limit: (ulong)options.Limit,
                filter: filter,
                cancellationToken: ct
            );

            return searchResult.Select(r =>
            {
                var payload = new Dictionary<string, object>();
                foreach (var kvp in r.Payload)
                {
                    payload[kvp.Key] = FromValue(kvp.Value) ?? "";
                }
                return new VectorSearchHit(r.Id.Uuid, r.Score, payload);
            }).ToList();
        }

        public async Task DeleteByFilterAsync(string collection, VectorFilter filter, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);
            var qdrantFilter = ToQdrantFilter(filter);
            await client.DeleteAsync(collectionName, qdrantFilter, cancellationToken: ct);
        }

        public async Task EnsureCollectionAsync(string collection, VectorCollectionSpec spec, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            var exists = await client.CollectionExistsAsync(collectionName, ct);
            if (exists)
            {
                return;
            }

            var distance = spec.Distance.ToLower() switch
            {
                "cosine" => Distance.Cosine,
                "dot" => Distance.Dot,
                "euclid" => Distance.Euclid,
                _ => Distance.Cosine
            };

            await client.CreateCollectionAsync(
                collectionName,
                new VectorParams { Size = (ulong)spec.VectorSize, Distance = distance },
                cancellationToken: ct
            );
        }

        private string GetQualifiedCollectionName(string collection, string? tenantId = "0")
        {
            return $"tenant-{tenantId}_{collection}";
        }

        public static string GenerateTenantCollectionName(string tenantId, string collectionName)
        {
            return $"tenant-{tenantId}_{collectionName}";
        }

        private Filter ToQdrantFilter(VectorFilter filter)
        {
            var conditions = new List<Condition>();
            foreach (var condition in filter.Must)
            {
                var cond = condition.Op.ToLower() switch
                {
                    "eq" => MatchKeyword(condition.Field, condition.Value.ToString() ?? ""),
                    "match" => MatchKeyword(condition.Field, condition.Value.ToString() ?? ""),
                    "gt" => Range(condition.Field, new GrpcRange { Gt = Convert.ToDouble(condition.Value) }),
                    "gte" => Range(condition.Field, new GrpcRange { Gte = Convert.ToDouble(condition.Value) }),
                    "lt" => Range(condition.Field, new GrpcRange { Lt = Convert.ToDouble(condition.Value) }),
                    "lte" => Range(condition.Field, new GrpcRange { Lte = Convert.ToDouble(condition.Value) }),
                    _ => MatchKeyword(condition.Field, condition.Value.ToString() ?? "")
                };
                conditions.Add(cond);
            }

            var qdrantFilter = new Filter();
            foreach (var c in conditions)
            {
                qdrantFilter.Must.Add(c);
            }
            return qdrantFilter;
        }

        private Value ToValue(object obj)
        {
            if (obj is string s)
                return new Value { StringValue = s };
            if (obj is int i)
                return new Value { IntegerValue = (long)i };
            if (obj is long l)
                return new Value { IntegerValue = l };
            if (obj is double d)
                return new Value { DoubleValue = d };
            if (obj is float f)
                return new Value { DoubleValue = (double)f };
            if (obj is bool b)
                return new Value { BoolValue = b };
            return new Value { StringValue = obj.ToString() ?? "" };
        }

        private object? FromValue(Value value)
        {
            switch (value.KindCase)
            {
                case Value.KindOneofCase.StringValue:
                    return value.StringValue;
                case Value.KindOneofCase.IntegerValue:
                    return value.IntegerValue;
                case Value.KindOneofCase.DoubleValue:
                    return value.DoubleValue;
                case Value.KindOneofCase.BoolValue:
                    return value.BoolValue;
                default:
                    return null;
            }
        }

        public async Task DeleteTenantCollectionsAsync(string tenantId, CancellationToken ct = default)
        {
            var collections = await client.ListCollectionsAsync(cancellationToken: ct);
            var prefix = $"tenant-{tenantId}_";

            foreach (var col in collections)
            {
                if (col.StartsWith(prefix))
                {
                    await client.DeleteCollectionAsync(col, cancellationToken: ct);
                }
            }
        }

        public async Task<IReadOnlyList<string>> ListTenantCollectionsAsync(string tenantId, CancellationToken ct = default)
        {
            var collections = await client.ListCollectionsAsync(cancellationToken: ct);
            var prefix = $"tenant-{tenantId}_";

            return collections
                .Where(c => c.StartsWith(prefix))
                .Select(c => c.Substring(prefix.Length))
                .ToList();
        }

        public async Task ClearCollectionAsync(string collection, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);
            var pointsInfo = await client.GetCollectionInfoAsync(collectionName, ct);
            if (pointsInfo?.PointsCount > 0)
            {
                await client.DeleteAsync(collectionName, filter: new Filter(), cancellationToken: ct);
            }
        }
    }
}
