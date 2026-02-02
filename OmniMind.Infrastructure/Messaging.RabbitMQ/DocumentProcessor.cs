using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using OmniMind.Abstractions.Ingestion;
using OmniMind.Abstractions.Storage;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.MySql;
using System.Diagnostics;

namespace OmniMind.Messaging.RabbitMQ
{
    /// <summary>
    /// 文档处理器
    /// 提供文档处理的公共逻辑，可被Quartz Job和RabbitMQ Consumer共用
    /// </summary>
    public static class DocumentProcessor
    {
        /// <summary>
        /// 处理单个文档
        /// </summary>
        public static async Task ProcessDocumentAsync(
            IServiceScope scope,
            Document document,
            OmniMindDbContext dbContext,
            ILogger? logger = null)
        {
            logger?.LogInformation("[文档处理] 开始处理: DocumentId={DocumentId}, Title={Title}",
                document.Id, document.Title);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 获取解析器和切片器
                var fileParser = scope.ServiceProvider.GetService<IFileParser>();
                var chunker = scope.ServiceProvider.GetService<IChunker>();

                if (fileParser == null)
                {
                    throw new InvalidOperationException("IFileParser 服务未注册");
                }
                if (chunker == null)
                {
                    throw new InvalidOperationException("IChunker 服务未注册");
                }

                // 1. 更新状态为"解析中"
                await dbContext.Documents.IgnoreQueryFilters()
                    .Where(x => x.Id == document.Id && x.TenantId == document.TenantId)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Parsing)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                // 2. 从MinIO下载文件
                logger?.LogInformation("[文档处理] 正在下载文件: {DocumentId}", document.Id);
                var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                var stream = await objectStorage.GetAsync(document.ObjectKey!);
                var type = await objectStorage.StatAsync(document.ObjectKey!);
                var contentType = type?.ContentType ?? "application/octet-stream";

                // 3. 解析文档内容
                logger?.LogInformation("[文档处理] 正在解析文档: {DocumentId}, ContentType={ContentType}",
                    document.Id, contentType);

                var extractedText = await fileParser.ParseAsync(stream, contentType, document.Id);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    throw new InvalidOperationException("文档解析结果为空");
                }

                logger?.LogInformation("[文档处理] 解析完成，文本长度: {TextLength} 字符", extractedText.Length);

                // 更新状态为"已解析"
                await dbContext.Documents.IgnoreQueryFilters()
                    .Where(x => x.Id == document.Id && x.TenantId == document.TenantId)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Parsed)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                // 4. 文本切片
                logger?.LogInformation("[文档处理] 正在切片文档: {DocumentId}", document.Id);

                // 清除旧的切片（如果存在）
                var existingChunks = await dbContext.Chunks.IgnoreQueryFilters()
                    .Where(c => c.DocumentId == document.Id && c.TenantId == document.TenantId)
                    .ToListAsync();
                if (existingChunks.Any())
                {
                    dbContext.Chunks.RemoveRange(existingChunks);
                    await dbContext.SaveChangesAsync();
                    logger?.LogInformation("[文档处理] 已清除旧切片: {Count} 个", existingChunks.Count);
                }

                // 执行文本切片
                var textChunks = chunker.Chunk(extractedText, new ChunkingOptions
                {
                    MaxTokens = 500,
                    OverlapTokens = 50
                });

                if (textChunks.Count == 0)
                {
                    logger?.LogWarning("[文档处理] 切片结果为空: DocumentId={DocumentId}", document.Id);
                }
                else
                {
                    // 转换为 Chunk 实体
                    var chunks = textChunks.Select(tc => new Chunk
                    {
                        Id = Guid.CreateVersion7().ToString(),
                        TenantId = document.TenantId,
                        DocumentId = document.Id,
                        Version = 1,
                        ChunkIndex = tc.Index,
                        ParentChunkId = null,
                        Content = tc.Content,
                        ExtraJson = null,
                        TokenCount = tc.TokenCount,
                        StartMs = null,
                        EndMs = null,
                        VectorPointId = null,
                        DateCreated = DateTimeOffset.UtcNow
                    }).ToList();

                    // 批量插入切片
                    dbContext.Chunks.AddRange(chunks);
                    await dbContext.SaveChangesAsync();
                    logger?.LogInformation("[文档处理] 切片完成: DocumentId={DocumentId}, 切片数量={ChunkCount}",
                        document.Id, chunks.Count);
                }

                // 5. 向量化
                logger?.LogInformation("[文档处理] 正在向量化文档: {DocumentId}", document.Id);

                var embeddingGenerator = scope.ServiceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
                if (embeddingGenerator == null)
                {
                    throw new InvalidOperationException("IEmbeddingGenerator 服务未注册");
                }

                // 重新加载切片（包含新插入的切片）
                var allChunks = await dbContext.Chunks.IgnoreQueryFilters()
                    .Where(c => c.DocumentId == document.Id && c.TenantId == document.TenantId)
                    .OrderBy(c => c.ChunkIndex)
                    .ToListAsync();

                if (allChunks.Count == 0)
                {
                    logger?.LogWarning("[文档处理] 没有切片需要向量化: DocumentId={DocumentId}", document.Id);
                }
                else
                {
                    // 批量向量化
                    var chunkTexts = allChunks.Select(c => c.Content).ToList();
                    var embeddings = await embeddingGenerator.GenerateAsync(chunkTexts);

                    if (embeddings.Count != allChunks.Count)
                    {
                        throw new InvalidOperationException($"向量化数量不匹配: 期望 {allChunks.Count}, 实际 {embeddings.Count}");
                    }

                    // 6. 存储向量到 Qdrant
                    logger?.LogInformation("[文档处理] 正在存储向量到 Qdrant: {DocumentId}", document.Id);
                    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

                    // 获取向量维度（从第一个 embedding 获取）
                    var vectorSize = embeddings.FirstOrDefault()?.Vector.Length ?? 1024;

                    // 确保集合存在
                    await vectorStore.EnsureCollectionAsync(
                        "documents",
                        new VectorCollectionSpec(vectorSize, "cosine")
                    );

                    // 准备向量点
                    var vectorPoints = new List<VectorPoint>();
                    for (int i = 0; i < allChunks.Count; i++)
                    {
                        var chunk = allChunks[i];
                        var embedding = embeddings[i];

                        // 使用 Chunk ID 作为向量点 ID
                        var pointId = chunk.Id;

                        // 构建元数据
                        var payload = new Dictionary<string, object>
                        {
                            { "tenant_id", chunk.TenantId },
                            { "document_id", chunk.DocumentId },
                            { "chunk_index", chunk.ChunkIndex },
                            { "content", chunk.Content }
                        };

                        // 从 Embedding<float> 获取向量数组
                        var vectorArray = embedding.Vector.ToArray();

                        vectorPoints.Add(new VectorPoint(pointId, vectorArray, payload));
                    }

                    // 批量插入向量
                    await vectorStore.UpsertAsync("documents", vectorPoints);

                    // 更新切片的向量点 ID
                    for (int i = 0; i < allChunks.Count; i++)
                    {
                        allChunks[i].VectorPointId = allChunks[i].Id;
                    }

                    await dbContext.SaveChangesAsync();

                    logger?.LogInformation("[文档处理] 向量化完成: DocumentId={DocumentId}, 向量数量={VectorCount}",
                        document.Id, vectorPoints.Count);
                }

                // 7. 更新状态为"已完成"
                await dbContext.Documents.IgnoreQueryFilters()
                    .Where(x => x.Id == document.Id && x.TenantId == document.TenantId)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Indexed)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));
                stopwatch.Stop();
                logger?.LogInformation("[文档处理] 处理完成: DocumentId={DocumentId}, 耗时={ElapsedMs}ms",
                    document.Id, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // 更新状态为"失败"
                await dbContext.Documents.IgnoreQueryFilters()
                    .Where(x => x.Id == document.Id && x.TenantId == document.TenantId)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, ex.Message.Length > 512
                            ? ex.Message.Substring(0, 512)
                            : ex.Message)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                logger?.LogError(ex, "[文档处理] 处理失败: DocumentId={DocumentId}, 耗时={ElapsedMs}ms",
                    document.Id, stopwatch.ElapsedMilliseconds);

                throw; // 重新抛出异常
            }
        }
    }
}
