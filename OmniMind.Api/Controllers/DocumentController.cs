using OmniMind.Api.Swaggers;
using OmniMind.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Abstractions.Storage;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Document;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messaging.Abstractions;
using OmniMind.Messaging.RabbitMQ;
using OmniMind.Messages;
using OmniMind.Persistence.PostgreSql;
using OmniMind.Storage.Minio;

namespace App.Controllers
{
    /// <summary>
    /// 文档模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly ILogger<DocumentController> logger;
        private readonly IObjectStorage objectStorage;
        private readonly IMessagePublisher messagePublisher;

        public DocumentController(
            OmniMindDbContext dbContext,
            ILogger<DocumentController> logger,
            IObjectStorage objectStorage,
            IMessagePublisher messagePublisher)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.objectStorage = objectStorage;
            this.messagePublisher = messagePublisher;
        }

        /// <summary>
        /// 上传文档
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [RequestSizeLimit(100_000_000)] // 100MB
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ErrorResponse { Message = "文件不能为空" });
            }

            var maxSize = 100L * 1024 * 1024; // 100MB
            if (request.File.Length > maxSize)
            {
                return BadRequest(new ErrorResponse { Message = "文件大小不能超过 100MB" });
            }

            var currentUserId = GetUserId();

            // 验证知识库是否存在
            var knowledgeBase = await dbContext.KnowledgeBases
                .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return BadRequest(new ErrorResponse { Message = "知识库不存在" });
            }

            // 如果指定了文件夹，验证是否存在且属于该知识库
            if (!string.IsNullOrEmpty(request.FolderId))
            {
                var folder = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == request.FolderId && f.KnowledgeBaseId == request.KnowledgeBaseId);
                if (folder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹不存在或不属于该知识库" });
                }
            }

            // 生成文档ID和对象Key
            var documentId = Guid.CreateVersion7().ToString();
            var fileName = request.File.FileName;
            var objectKey = MinioObjectStorage.GenerateTenantObjectKey(GetUserId(), documentId, fileName);

            // 准备元数据（原始文件名等）
            var metadata = new Dictionary<string, string>
            {
                ["original-filename"] = fileName,
                ["upload-timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
                ["content-type"] = request.File.ContentType,
                ["content-length"] = request.File.Length.ToString()
            };

            // 上传文件到 MinIO（带元数据）
            try
            {
                // 根据文件扩展名确定正确的 MIME 类型
                var mimeType = GetMimeTypeFromExtension(fileName);
                using var stream = request.File.OpenReadStream();
                await objectStorage.PutAsync(objectKey, stream, mimeType, metadata);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "上传文件到 MinIO 失败: {ObjectKey}", objectKey);
                return BadRequest(new ErrorResponse { Message = "文件上传失败，请稍后重试" });
            }

            // 计算文件 Hash (简化版，实际应该用 MD5 或 SHA256)
            var fileHash = $"{request.File.Length}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // 确定内容类型（MIME 类型）
            var contentType = DetermineContentType(fileName);

            // 创建文档记录
            var document = new Document
            {
                Id = documentId,
                KnowledgeBaseId = request.KnowledgeBaseId,
                FolderId = request.FolderId,
                Title = (request.Title ?? Path.GetFileNameWithoutExtension(fileName)).Trim(),
                ContentType = contentType,
                SourceType = SourceType.Upload,
                SourceUri = null,
                ObjectKey = objectKey,
                FileSize = request.File.Length,
                FileHash = fileHash,
                Language = "zh-CN", // 默认中文，后续可以自动检测
                Status = DocumentStatus.Uploaded,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            // 发布文档上传消息到队列
            try
            {
                var uploadMessage = new DocumentUploadMessage
                {
                    DocumentId = document.Id,
                    KnowledgeBaseId = request.KnowledgeBaseId,
                    ObjectKey = objectKey,
                    FileName = fileName,
                    ContentType = contentType
                };

                await messagePublisher.PublishDocumentUploadAsync(uploadMessage);
                logger.LogInformation("已发布文档上传消息: DocumentId={DocumentId}, userId={userId}",
                    document.Id, GetUserId());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "发布文档上传消息失败: DocumentId={DocumentId}", document.Id);
                // 消息发布失败不影响文档创建，可以后续通过补偿机制处理
            }

            var response = await MapToResponse(document);
            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, response);
        }

        /// <summary>
        /// 创建文档（从 URL 或其他来源）
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new ErrorResponse { Message = "文档标题不能为空" });
            }

            if (request.Title.Length > 256)
            {
                return BadRequest(new ErrorResponse { Message = "文档标题长度不能超过256个字符" });
            }
            var currentUserId = GetUserId();

            // 验证知识库是否存在
            var knowledgeBase = await dbContext.KnowledgeBases
                .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return BadRequest(new ErrorResponse { Message = "知识库不存在" });
            }

            // 如果指定了文件夹，验证是否存在且属于该知识库
            if (!string.IsNullOrEmpty(request.FolderId))
            {
                var folder = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == request.FolderId && f.KnowledgeBaseId == request.KnowledgeBaseId);
                if (folder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹不存在或不属于该知识库" });
                }
            }

            var documentId = Guid.CreateVersion7().ToString();

            var document = new Document
            {
                Id = documentId,
                KnowledgeBaseId = request.KnowledgeBaseId,
                FolderId = request.FolderId,
                Title = request.Title.Trim(),
                ContentType = request.ContentType,
                SourceType = request.SourceType,
                SourceUri = request.SourceUri,
                ObjectKey = request.ObjectKey,
                FileHash = request.FileHash,
                Language = request.Language,
                Content = request.Content,
                Status = DocumentStatus.Uploaded,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            // 发布文档创建消息到队列（用于笔记、网页链接等的处理）
            try
            {
                var uploadMessage = new DocumentUploadMessage
                {
                    DocumentId = document.Id,
                    KnowledgeBaseId = request.KnowledgeBaseId,
                    ObjectKey = document.ObjectKey ?? string.Empty,
                    FileName = document.Title,
                    ContentType = document.ContentType
                };

                await messagePublisher.PublishDocumentUploadAsync(uploadMessage);
                logger.LogInformation("已发布文档创建消息: DocumentId={DocumentId}, ContentType={ContentType}",
                    document.Id, document.ContentType);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "发布文档创建消息失败: DocumentId={DocumentId}", document.Id);
            }

            var response = await MapToResponse(document);
            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, response);
        }

        /// <summary>
        /// 获取文档详情
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetDocument(string id)
        {
            var document = await dbContext.Documents
                .Include(d => d.Folder)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            // 权限检查
            var authResult = await dbContext.CheckKnowledgeBaseAccessAsync(document.KnowledgeBaseId, GetUserId());
            if (!authResult.HasAccess)
            {
                return StatusCode(403, new ErrorResponse { Message = authResult.Message ?? "无权访问此文档" });
            }

            var response = await MapToResponse(document);
            return Ok(response);
        }

        /// <summary>
        /// 获取文档列表
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<DocumentResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDocuments(
            [FromQuery] string? knowledgeBaseId = null,
            [FromQuery] string? folderId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? keyword = null,
            [FromQuery] int? status = null)
        {
            // 知识库筛选 - 需要验证权限
            if (!string.IsNullOrEmpty(knowledgeBaseId))
            {
                var authResult = await dbContext.CheckKnowledgeBaseAccessAsync(knowledgeBaseId, GetUserId());
                if (!authResult.HasAccess)
                {
                    return Ok(new PagedResponse<DocumentResponse>
                    {
                        Items = new List<DocumentResponse>(),
                        TotalCount = 0,
                        Page = page,
                        PageSize = pageSize,
                        Message = authResult.Message
                    });
                }
            }

            var query = dbContext.Documents.Include(d => d.Folder).AsQueryable();

            // 知识库筛选
            if (!string.IsNullOrEmpty(knowledgeBaseId))
            {
                query = query.Where(d => d.KnowledgeBaseId == knowledgeBaseId);
            }

            // 文件夹筛选
            if (string.IsNullOrEmpty(folderId))
            {
                // 根目录：只显示 FolderId 为 null 的文档
                query = query.Where(d => d.FolderId == null);
            }
            else
            {
                // 子目录：只显示 FolderId 等于指定值的文档
                query = query.Where(d => d.FolderId == folderId);
            }

            // 关键字搜索
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(d => d.Title.Contains(keyword));
            }

            // 状态筛选
            if (status.HasValue)
            {
                query = query.Where(d => (int)d.Status == status.Value);
            }

            var totalCount = await query.CountAsync();

            var documents = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var responses = new List<DocumentResponse>();
            foreach (var doc in documents)
            {
                responses.Add(await MapToResponse(doc));
            }

            return Ok(new PagedResponse<DocumentResponse>
            {
                Items = responses,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// 删除文档
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteDocument(string id)
        {
            var document = await dbContext.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            // 保存 ObjectKey 用于后续删除文件
            var objectKey = document.ObjectKey;

            // 先删除数据库记录
            dbContext.Documents.Remove(document);
            await dbContext.SaveChangesAsync();

            // 异步删除 MinIO 中的文件（即使失败也不影响用户操作）
            if (!string.IsNullOrEmpty(objectKey))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await objectStorage.DeleteAsync(objectKey);
                        logger.LogInformation("已删除 MinIO 文件: {ObjectKey}", objectKey);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "删除 MinIO 文件失败: {ObjectKey}", objectKey);
                        // 可以考虑记录到失败队列，稍后重试
                    }
                });
            }

            return NoContent();
        }

        /// <summary>
        /// 移动文档到文件夹
        /// </summary>
        [HttpPatch("{id}/move")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MoveDocument(string id, [FromBody] MoveDocumentRequest request)
        {
            var document = await dbContext.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            // 如果指定了文件夹，验证是否存在且属于同一知识库
            if (!string.IsNullOrEmpty(request.FolderId))
            {
                var folder = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == request.FolderId && f.KnowledgeBaseId == document.KnowledgeBaseId);
                if (folder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹不存在或不属于该知识库" });
                }

                document.FolderId = request.FolderId;
            }
            else
            {
                // 移到根目录
                document.FolderId = null;
            }

            document.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.Documents.Update(document);
            await dbContext.SaveChangesAsync();

            var response = await MapToResponse(document);
            return Ok(response);
        }

        #region Private Methods

        /// <summary>
        /// 根据文件扩展名获取 MIME 类型
        /// </summary>
        private string GetMimeTypeFromExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".md" or ".markdown" => "text/markdown",
                ".txt" => "text/plain",
                ".htm" or ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".xml" => "application/xml",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                ".flv" => "video/x-flv",
                ".mkv" => "video/x-matroska",
                ".webm" => "video/webm",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                ".ogg" => "audio/ogg",
                ".wma" => "audio/x-ms-wma",
                ".m4a" => "audio/mp4",
                ".zip" => "application/zip",
                ".rar" => "application/x-rar-compressed",
                ".7z" => "application/x-7z-compressed",
                ".tar" => "application/x-tar",
                ".gz" => "application/gzip",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// 根据文件名确定内容类型（MIME 类型）
        /// </summary>
        private string DetermineContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".md" or ".markdown" => "text/markdown",
                ".txt" => "text/plain",
                ".htm" or ".html" => "text/html",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".mp4" => "video/mp4",
                ".avi" => "video/x-msvideo",
                ".mov" => "video/quicktime",
                ".wmv" => "video/x-ms-wmv",
                ".flv" => "video/x-flv",
                ".mkv" => "video/x-matroska",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                ".m4a" => "audio/mp4", // 或 audio/x-m4a
                ".ogg" => "audio/ogg",
                _ => "text/plain" // 默认作为纯文本处理
            };
        }

        /// <summary>
        /// 映射到响应对象
        /// </summary>
        private async Task<DocumentResponse> MapToResponse(Document document)
        {
            // 统计切片数量
            var chunkCount = await dbContext.Chunks
                .CountAsync(c => c.DocumentId == document.Id);

            return new DocumentResponse
            {
                Id = document.Id,
                KnowledgeBaseId = document.KnowledgeBaseId ?? string.Empty,
                FolderId = document.FolderId,
                FolderName = document.Folder?.Name,
                Title = document.Title,
                ContentType = document.ContentType,
                SourceType = document.SourceType,
                SourceUri = document.SourceUri,
                ObjectKey = document.ObjectKey,
                FileSize = document.FileSize,
                FileHash = document.FileHash,
                Language = document.Language,
                Status = document.Status,
                Error = document.Error,
                Duration = document.Duration,
                Transcription = document.Transcription,
                Content = document.Content,
                SessionId = document.SessionId,
                CreatedByUserId = document.CreatedByUserId,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
                ChunkCount = chunkCount
            };
        }

        #endregion
    }

    /// <summary>
    /// 移动文档请求
    /// </summary>
    public record MoveDocumentRequest
    {
        /// <summary>
        /// 目标文件夹ID（null 表示移到知识库根目录）
        /// </summary>
        public string? FolderId { get; init; }
    }
}
