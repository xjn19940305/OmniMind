using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniMind.Abstractions.Ingestion;
using OmniMind.Abstractions.SignalR;
using OmniMind.Abstractions.Storage;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Ingestion;
using OmniMind.Persistence.PostgreSql;
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
        /// 需要转写的音视频 MIME 类型
        /// </summary>
        private static readonly HashSet<string> TranscribeContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            // 音频
            "audio/mpeg",        // .mp3
            "audio/mp3",
            "audio/wav",         // .wav
            "audio/wave",
            "audio/x-wav",
            "audio/mp4",         // .m4a
            "audio/x-m4a",
            "audio/aac",
            "audio/ogg",         // .ogg
            "audio/webm",        // .webm audio
            "audio/flac",        // .flac
            "audio/x-flac",
            "audio/amr",         // .amr (录音格式)
            "audio/x-amr",

            // 视频
            "video/mp4",         // .mp4
            "video/x-m4v",
            "video/quicktime",   // .mov
            "video/x-msvideo",   // .avi
            "video/x-matroska",  // .mkv
            "video/webm",        // .webm
            "video/x-flv",       // .flv
            "video/x-ms-wmv",    // .wmv
        };

        /// <summary>
        /// 需要转写的文件扩展名（作为备用检测）
        /// </summary>
        private static readonly HashSet<string> TranscribeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            // 音频
            ".mp3", ".wav", ".wave", ".m4a", ".aac", ".ogg", ".flac", ".amr",
            // 视频
            ".mp4", ".mov", ".avi", ".mkv", ".webm", ".flv", ".wmv", ".m4v"
        };

        /// <summary>
        /// 检测是否是需要转写的音视频文件
        /// </summary>
        public static bool IsAudioOrVideo(Document document)
        {
            // 优先检查 MIME 类型
            if (!string.IsNullOrWhiteSpace(document.ContentType))
            {
                var mimeType = document.ContentType.ToLowerInvariant();
                // 检查是否以 audio/ 或 video/ 开头，或者在我们的预定义列表中
                if (mimeType.StartsWith("audio/") || mimeType.StartsWith("video/"))
                {
                    return true;
                }
            }

            // 备用：检查文件扩展名
            if (!string.IsNullOrWhiteSpace(document.Title) || !string.IsNullOrWhiteSpace(document.ObjectKey))
            {
                var fileName = !string.IsNullOrWhiteSpace(document.Title) ? document.Title : document.ObjectKey;
                var extension = Path.GetExtension(fileName);
                return TranscribeExtensions.Contains(extension);
            }

            return false;
        }

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
                var realtimeNotifier = scope.ServiceProvider.GetService<IRealtimeNotifier>();

                if (fileParser == null)
                {
                    throw new InvalidOperationException("IFileParser 服务未注册");
                }
                if (chunker == null)
                {
                    throw new InvalidOperationException("IChunker 服务未注册");
                }

                // 1. 更新状态为"解析中"并发送通知
                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Parsing)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                // 发送解析中通知
                if (realtimeNotifier != null)
                {
                    await realtimeNotifier.NotifyDocumentProgressAsync(
                        document.CreatedByUserId,
                        document.Id,
                        new DocumentProgress
                        {
                            DocumentId = document.Id,
                            Title = document.Title,
                            Status = "Parsing",
                            Progress = 20,
                            Stage = "正在解析文档内容..."
                        });
                }

                // 2. 检查是否是直接存储内容的文档（笔记、网页链接等）
                string extractedText;

                if (!string.IsNullOrWhiteSpace(document.Content))
                {
                    // 笔记、网页链接等，内容直接存储在 Content 字段
                    logger?.LogInformation("[文档处理] 使用存储的内容: {DocumentId}, ContentType={ContentType}",
                        document.Id, document.ContentType);

                    // 对于网页链接，需要爬取网页内容
                    if (document.ContentType == "text/html" || document.ContentType == "text/url")
                    {
                        // TODO: 网页链接处理 - 待实现
                        // 方案1: 使用爬虫爬取网页内容（如 Puppeteer、Playwright、HtmlAgilityPack）
                        // 方案2: 使用 MCP 协议获取网页内容
                        // 目前暂时使用 URL 作为占位内容
                        logger?.LogInformation("[文档处理] 网页链接文档，URL: {Url}，内容爬取待实现", document.Content);
                    }

                    extractedText = document.Content;
                }
                else
                {
                    // 从 MinIO 下载文件
                    logger?.LogInformation("[文档处理] 正在下载文件: {DocumentId}", document.Id);
                    var objectStorage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
                    var stream = await objectStorage.GetAsync(document.ObjectKey!);

                    // 使用 Document 表中存储的 ContentType（更准确）
                    var contentType = document.ContentType;

                    // 3. 解析文档内容/转写音频视频
                    logger?.LogInformation("[文档处理] 正在处理文件: {DocumentId}, ContentType={ContentType}",
                        document.Id, contentType);

                    extractedText = await fileParser.ParseAsync(stream, contentType, document.Id);
                }

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    throw new InvalidOperationException("文档解析结果为空");
                }

                logger?.LogInformation("[文档处理] 解析完成，文本长度: {TextLength} 字符", extractedText.Length);

                // 更新状态为"已解析"并发送通知
                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Parsed)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                // 发送已解析通知
                if (realtimeNotifier != null)
                {
                    await realtimeNotifier.NotifyDocumentProgressAsync(
                        document.CreatedByUserId,
                        document.Id,
                        new DocumentProgress
                        {
                            DocumentId = document.Id,
                            Title = document.Title,
                            Status = "Parsed",
                            Progress = 50,
                            Stage = "文档已解析，正在建立索引..."
                        });
                }

                // 4. 文本切片
                logger?.LogInformation("[文档处理] 正在切片文档: {DocumentId}", document.Id);

                // 清除旧的切片（如果存在）
                var existingChunks = await dbContext.Chunks
                    .Where(c => c.DocumentId == document.Id)
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

                // 发送索引中通知
                if (realtimeNotifier != null)
                {
                    await realtimeNotifier.NotifyDocumentProgressAsync(
                        document.CreatedByUserId,
                        document.Id,
                        new DocumentProgress
                        {
                            DocumentId = document.Id,
                            Title = document.Title,
                            Status = "Indexing",
                            Progress = 70,
                            Stage = "正在建立向量索引..."
                        });
                }

                var embeddingGenerator = scope.ServiceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
                if (embeddingGenerator == null)
                {
                    throw new InvalidOperationException("IEmbeddingGenerator 服务未注册");
                }

                // 重新加载切片（包含新插入的切片）
                var allChunks = await dbContext.Chunks
                    .Where(c => c.DocumentId == document.Id)
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
                    GeneratedEmbeddings<Embedding<float>> embeddings;

                    using (AiCallContext.BeginScope(document.CreatedByUserId)
                        .WithDocument(document.Id)
                        .WithKnowledgeBase(document?.KnowledgeBaseId))
                    {
                        embeddings = await embeddingGenerator.GenerateAsync(chunkTexts);
                    }

                    if (embeddings.Count != allChunks.Count)
                    {
                        throw new InvalidOperationException($"向量化数量不匹配: 期望 {allChunks.Count}, 实际 {embeddings.Count}");
                    }

                    // 6. 存储向量到 Qdrant
                    logger?.LogInformation("[文档处理] 正在存储向量到 Qdrant: {DocumentId}", document.Id);
                    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();

                    // 获取向量维度（从第一个 embedding 获取）
                    var vectorSize = embeddings.FirstOrDefault()?.Vector.Length ?? 1024;

                    // 确定集合名称：
                    // - 知识库文件：document_kb_{KnowledgeBaseId}
                    // - 临时文件（聊天上传）：document_session_{SessionId}
                    string documentCollectionName;
                    if (!string.IsNullOrEmpty(document.KnowledgeBaseId))
                    {
                        // 知识库文件
                        documentCollectionName = $"document_kb_{document.KnowledgeBaseId}";
                    }
                    else if (!string.IsNullOrEmpty(document.SessionId))
                    {
                        // 临时文件（AI 聊天上传），按 SessionId 隔离
                        documentCollectionName = $"document_session_{document.SessionId}";
                    }
                    else
                    {
                        // 兜底：不应该走到这里，但为了保险
                        logger?.LogWarning("[文档处理] 文档既没有 KnowledgeBaseId 也没有 SessionId: DocumentId={DocumentId}", document.Id);
                        documentCollectionName = $"document_fallback_{document.Id}";
                    }

                    logger?.LogInformation("[文档处理] 使用集合: {CollectionName}, DocumentId={DocumentId}",
                        documentCollectionName, document.Id);

                    // 确保集合存在
                    await vectorStore.EnsureCollectionAsync(
                        documentCollectionName,
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
                            { "document_id", chunk.DocumentId },
                            { "chunk_index", chunk.ChunkIndex },
                            { "content", chunk.Content }
                        };

                        // 从 Embedding<float> 获取向量数组
                        var vectorArray = embedding.Vector.ToArray();

                        vectorPoints.Add(new VectorPoint(pointId, vectorArray, payload));
                    }

                    // 批量插入向量
                    await vectorStore.UpsertAsync(documentCollectionName, vectorPoints);

                    // 更新切片的向量点 ID
                    for (int i = 0; i < allChunks.Count; i++)
                    {
                        allChunks[i].VectorPointId = allChunks[i].Id;
                    }

                    await dbContext.SaveChangesAsync();

                    logger?.LogInformation("[文档处理] 向量化完成: DocumentId={DocumentId}, 向量数量={VectorCount}",
                        document.Id, vectorPoints.Count);
                }

                // 7. 更新状态为"已完成"并发送通知
                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Indexed)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                // 发送完成通知
                if (realtimeNotifier != null)
                {
                    await realtimeNotifier.NotifyDocumentProgressAsync(
                        document.CreatedByUserId,
                        document.Id,
                        new DocumentProgress
                        {
                            DocumentId = document.Id,
                            Title = document.Title,
                            Status = "Indexed",
                            Progress = 100,
                            Stage = "处理完成！"
                        });
                }

                stopwatch.Stop();
                logger?.LogInformation("[文档处理] 处理完成: DocumentId={DocumentId}, 耗时={ElapsedMs}ms",
                    document.Id, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                // 更新状态为"失败"
                await dbContext.Documents
                    .Where(x => x.Id == document.Id)
                    .ExecuteUpdateAsync(d => d
                        .SetProperty(x => x.Status, DocumentStatus.Failed)
                        .SetProperty(x => x.Error, ex.Message.Length > 512
                            ? ex.Message.Substring(0, 512)
                            : ex.Message)
                        .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow));

                // 发送失败通知
                var realtimeNotifier = scope.ServiceProvider.GetService<IRealtimeNotifier>();
                if (realtimeNotifier != null)
                {
                    await realtimeNotifier.NotifyDocumentProgressAsync(
                        document.CreatedByUserId,
                        document.Id,
                        new DocumentProgress
                        {
                            DocumentId = document.Id,
                            Title = document.Title,
                            Status = "Failed",
                            Progress = 0,
                            Stage = "处理失败",
                            Error = ex.Message
                        });
                }

                logger?.LogError(ex, "[文档处理] 处理失败: DocumentId={DocumentId}, 耗时={ElapsedMs}ms",
                    document.Id, stopwatch.ElapsedMilliseconds);

                throw; // 重新抛出异常
            }
        }
    }
}
