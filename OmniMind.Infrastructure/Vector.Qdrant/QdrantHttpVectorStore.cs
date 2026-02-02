using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OmniMind.Abstractions.Storage;

namespace OmniMind.Vector.Qdrant
{
    public class QdrantHttpVectorStore : IVectorStore
    {
        private readonly HttpClient httpClient;
        private readonly QdrantOptions options;
        private readonly JsonSerializerOptions jsonOptions;

        public QdrantHttpVectorStore(
            HttpClient httpClient,
            IOptions<QdrantOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;

            // 设置 BaseAddress（包含端口 6333）
            var scheme = this.options.Https ? "https" : "http";
            var host = this.options.Host ?? "localhost";
            var port = this.options.Port > 0 ? this.options.Port : 6333;
            this.httpClient.BaseAddress = new Uri($"{scheme}://{host}:{port}/");

            this.jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task UpsertAsync(string collection, IReadOnlyList<VectorPoint> points, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            var payload = new
            {
                points = points.Select(p => new
                {
                    id = p.Id,
                    vector = p.Vector.ToArray(),
                    payload = p.Payload
                })
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PutAsync(
                $"collections/{collectionName}/points",
                content,
                ct);

            response.EnsureSuccessStatusCode();
        }

        public async Task<IReadOnlyList<VectorSearchHit>> SearchAsync(string collection, float[] vector, VectorSearchOptions options, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            // 构建动态 payload
            var payloadDict = new Dictionary<string, object>
            {
                ["vector"] = vector,
                ["limit"] = options.Limit,
                ["with_payload"] = true
            };

            // 如果有过滤器，添加 filter
            if (options.Filter != null && options.Filter.Must.Any())
            {
                payloadDict["filter"] = new
                {
                    must = options.Filter.Must.Select(m => new
                    {
                        key = m.Field,
                        match = new { value = m.Value }
                    })
                };
            }

            var content = new StringContent(
                JsonSerializer.Serialize(payloadDict, jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(
                $"collections/{collectionName}/points/search",
                content,
                ct);

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var searchResult = JsonSerializer.Deserialize<QdrantSearchResponse>(responseBody, jsonOptions);

            return searchResult?.result?.Select(r => new VectorSearchHit(
                r.id,
                r.score,
                r.payload ?? new Dictionary<string, object>()))
                .ToList() ?? new List<VectorSearchHit>();
        }

        public async Task DeleteByFilterAsync(string collection, VectorFilter filter, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            var payload = new
            {
                filter = new
                {
                    must = filter.Must.Select(m => new
                    {
                        key = m.Field,
                        match = new { value = m.Value }
                    })
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync(
                $"collections/{collectionName}/points/delete",
                content,
                ct);

            response.EnsureSuccessStatusCode();
        }

        public async Task EnsureCollectionAsync(string collection, VectorCollectionSpec spec, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            // 检查集合是否存在
            var checkResponse = await httpClient.GetAsync($"collections/{collectionName}", ct);
            if (checkResponse.IsSuccessStatusCode)
            {
                return; // 集合已存在
            }

            // 创建集合
            var distance = spec.Distance.ToLower() switch
            {
                "cosine" => "Cosine",
                "dot" => "Dot",
                "euclid" => "Euclid",
                _ => "Cosine"
            };

            var payload = new
            {
                vectors = new
                {
                    size = spec.VectorSize,
                    distance = distance
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(payload, jsonOptions),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PutAsync(
                $"collections/{collectionName}",
                content,
                ct);

            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteTenantCollectionsAsync(string tenantId, CancellationToken ct = default)
        {
            var collections = await ListCollectionsAsync(ct);
            var prefix = $"tenant-{tenantId}_";

            foreach (var col in collections)
            {
                if (col.StartsWith(prefix))
                {
                    await DeleteCollectionAsync(col, ct);
                }
            }
        }

        public async Task<IReadOnlyList<string>> ListTenantCollectionsAsync(string tenantId, CancellationToken ct = default)
        {
            var collections = await ListCollectionsAsync(ct);
            var prefix = $"tenant-{tenantId}_";

            return collections
                .Where(c => c.StartsWith(prefix))
                .Select(c => c.Substring(prefix.Length))
                .ToList();
        }

        public async Task ClearCollectionAsync(string collection, CancellationToken ct = default)
        {
            var collectionName = GetQualifiedCollectionName(collection);

            // 先获取集合信息，检查是否有数据
            var infoResponse = await httpClient.GetAsync($"collections/{collectionName}", ct);
            if (!infoResponse.IsSuccessStatusCode)
            {
                return;
            }

            var infoBody = await infoResponse.Content.ReadAsStringAsync(ct);
            var info = JsonSerializer.Deserialize<QdrantCollectionInfo>(infoBody, jsonOptions);

            if (info?.result?.points_count > 0)
            {
                // 删除所有点（使用空过滤器）
                var payload = new
                {
                    filter = new { }
                };

                var content = new StringContent(
                    JsonSerializer.Serialize(payload, jsonOptions),
                    System.Text.Encoding.UTF8,
                    "application/json");

                var response = await httpClient.PostAsync(
                    $"collections/{collectionName}/points/delete",
                    content,
                    ct);

                response.EnsureSuccessStatusCode();
            }
        }

        private string GetQualifiedCollectionName(string collection, string? tenantId = "0")
        {
            return $"tenant-{tenantId}_{collection}";
        }

        public static string GenerateTenantCollectionName(string tenantId, string collectionName)
        {
            return $"tenant-{tenantId}_{collectionName}";
        }

        private async Task<List<string>> ListCollectionsAsync(CancellationToken ct)
        {
            var response = await httpClient.GetAsync("collections", ct);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<QdrantCollectionsResponse>(responseBody, jsonOptions);

            return result?.result?.collections?.Select(c => c.name).ToList() ?? new List<string>();
        }

        private async Task DeleteCollectionAsync(string collectionName, CancellationToken ct)
        {
            var response = await httpClient.DeleteAsync($"collections/{collectionName}", ct);
            response.EnsureSuccessStatusCode();
        }

        // HTTP API 响应类型
        private class QdrantSearchResponse
        {
            public List<QdrantSearchResult>? result { get; set; }
        }

        private class QdrantSearchResult
        {
            public string id { get; set; } = string.Empty;
            public float score { get; set; }
            public Dictionary<string, object>? payload { get; set; }
        }

        private class QdrantCollectionsResponse
        {
            public QdrantCollectionsResult? result { get; set; }
        }

        private class QdrantCollectionsResult
        {
            public List<QdrantCollection>? collections { get; set; }
        }

        private class QdrantCollection
        {
            public string name { get; set; } = string.Empty;
        }

        private class QdrantCollectionInfo
        {
            public QdrantCollectionInfoResult? result { get; set; }
        }

        private class QdrantCollectionInfoResult
        {
            public ulong? points_count { get; set; }
        }
    }
}
