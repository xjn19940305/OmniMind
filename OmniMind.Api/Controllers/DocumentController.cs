using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Abstractions.Storage;
using OmniMind.Api.Extensions;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Document;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messages;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.PostgreSql;
using OmniMind.Storage.Minio;

namespace App.Controllers
{
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly ILogger<DocumentController> logger;
        private readonly IObjectStorage objectStorage;
        private readonly IVectorStore vectorStore;
        private readonly IMessagePublisher messagePublisher;

        public DocumentController(
            OmniMindDbContext dbContext,
            ILogger<DocumentController> logger,
            IObjectStorage objectStorage,
            IVectorStore vectorStore,
            IMessagePublisher messagePublisher)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.objectStorage = objectStorage;
            this.vectorStore = vectorStore;
            this.messagePublisher = messagePublisher;
        }

        [HttpPost("upload")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
        [RequestSizeLimit(100_000_000)]
        public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ErrorResponse { Message = "文件不能为空" });
            }

            if (request.File.Length > 100L * 1024 * 1024)
            {
                return BadRequest(new ErrorResponse { Message = "文件大小不能超过100MB" });
            }

            var knowledgeBase = await dbContext.KnowledgeBases.FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return BadRequest(new ErrorResponse { Message = "知识库不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (!string.IsNullOrEmpty(request.FolderId))
            {
                var folderExists = await dbContext.Folders.AnyAsync(f => f.Id == request.FolderId && f.KnowledgeBaseId == request.KnowledgeBaseId);
                if (!folderExists)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹不存在或不属于该知识库" });
                }
            }

            var documentId = Guid.CreateVersion7().ToString();
            var fileName = request.File.FileName;
            var objectKey = MinioObjectStorage.GenerateObjectKey(GetUserId(), documentId, fileName);
            var contentType = DetermineContentType(fileName);
            if (!IsSupportedUploadContentType(contentType))
            {
                return BadRequest(new ErrorResponse { Message = "当前暂仅支持 pdf、docx、pptx、xlsx、md、txt，以及现有图片音视频类型" });
            }

            try
            {
                using var stream = request.File.OpenReadStream();
                await objectStorage.PutAsync(objectKey, stream, contentType, new Dictionary<string, string>
                {
                    ["original-filename"] = fileName,
                    ["upload-timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
                    ["content-type"] = contentType,
                    ["content-length"] = request.File.Length.ToString()
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "上传文件到对象存储失败: {ObjectKey}", objectKey);
                return BadRequest(new ErrorResponse { Message = "文件上传失败，请稍后重试" });
            }

            var document = new Document
            {
                Id = documentId,
                KnowledgeBaseId = request.KnowledgeBaseId,
                FolderId = request.FolderId,
                Title = (request.Title ?? Path.GetFileNameWithoutExtension(fileName)).Trim(),
                ContentType = contentType,
                SourceType = SourceType.Upload,
                ObjectKey = objectKey,
                FileSize = request.File.Length,
                FileHash = $"{request.File.Length}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                Language = "zh-CN",
                Status = DocumentStatus.Uploaded,
                CreatedByUserId = GetUserId(),
                CreatedAt = DateTimeOffset.UtcNow,
                Content = request.Content
            };

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();
            await PublishDocumentUploadAsync(document, fileName);

            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, await MapToResponse(document));
        }

        [HttpPost]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
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

            var knowledgeBase = await dbContext.KnowledgeBases.FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return BadRequest(new ErrorResponse { Message = "知识库不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (!string.IsNullOrEmpty(request.FolderId))
            {
                var folderExists = await dbContext.Folders.AnyAsync(f => f.Id == request.FolderId && f.KnowledgeBaseId == request.KnowledgeBaseId);
                if (!folderExists)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹不存在或不属于该知识库" });
                }
            }

            var document = new Document
            {
                Id = Guid.CreateVersion7().ToString(),
                KnowledgeBaseId = request.KnowledgeBaseId,
                FolderId = request.FolderId,
                Title = request.Title.Trim(),
                ContentType = string.IsNullOrWhiteSpace(request.ContentType) ? "text/plain" : request.ContentType.Trim(),
                SourceType = request.SourceType,
                SourceUri = request.SourceUri,
                ObjectKey = request.ObjectKey,
                FileHash = request.FileHash,
                Language = request.Language,
                Content = request.Content,
                Status = DocumentStatus.Uploaded,
                CreatedByUserId = GetUserId(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();
            await PublishDocumentUploadAsync(document, document.Title);

            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, await MapToResponse(document));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDocument(string id)
        {
            var document = await dbContext.Documents
                .Include(d => d.Folder)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            if (!await EnsureDocumentReadableAsync(document))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "无权访问此文档" });
            }

            return Ok(await MapToResponse(document));
        }

        [HttpGet("{id}/preview")]
        public async Task<IActionResult> PreviewDocument(string id)
        {
            var document = await dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == id);
            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            if (!await EnsureDocumentReadableAsync(document))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "无权访问此文档" });
            }

            if (!string.IsNullOrWhiteSpace(document.ObjectKey))
            {
                try
                {
                    var stream = await objectStorage.GetAsync(document.ObjectKey);
                    Response.Headers["Content-Disposition"] =
                        $"inline; filename*=UTF-8''{Uri.EscapeDataString(document.Title)}";
                    return File(stream, document.ContentType);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "预览文档失败: DocumentId={DocumentId}", document.Id);
                    return NotFound(new ErrorResponse { Message = "文件内容不存在或无法预览" });
                }
            }

            if (!string.IsNullOrWhiteSpace(document.Content))
            {
                Response.Headers["Content-Disposition"] =
                    $"inline; filename*=UTF-8''{Uri.EscapeDataString(document.Title)}";
                return Content(document.Content, document.ContentType);
            }

            if (!string.IsNullOrWhiteSpace(document.SourceUri))
            {
                return Redirect(document.SourceUri);
            }

            return NotFound(new ErrorResponse { Message = "文件内容不存在或无法预览" });
        }

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
            var query = dbContext.Documents.Include(d => d.Folder).AsQueryable();

            if (!string.IsNullOrEmpty(knowledgeBaseId))
            {
                var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBaseId, GetUserId(), KnowledgeBasePermission.View);
                if (!auth.HasAccess)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = auth.Message ?? "无权访问此知识库" });
                }

                query = query.Where(d => d.KnowledgeBaseId == knowledgeBaseId);
            }
            else
            {
                query = query.Where(d => d.SessionId != null && d.CreatedByUserId == GetUserId());
            }

            query = string.IsNullOrEmpty(folderId)
                ? query.Where(d => d.FolderId == null)
                : query.Where(d => d.FolderId == folderId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(d => d.Title.Contains(keyword));
            }

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
            foreach (var document in documents)
            {
                responses.Add(await MapToResponse(document));
            }

            return Ok(new PagedResponse<DocumentResponse>
            {
                Items = responses,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteDocument(string id)
        {
            var document = await dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id);
            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            if (!await EnsureDocumentWritableAsync(document))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "无权删除此文档" });
            }

            await DeleteDocumentArtifactsAsync(document);

            var chunks = await dbContext.Chunks.Where(c => c.DocumentId == document.Id).ToListAsync();
            if (chunks.Count > 0)
            {
                dbContext.Chunks.RemoveRange(chunks);
            }

            dbContext.Documents.Remove(document);
            await dbContext.SaveChangesAsync();
            return NoContent();
        }

        [HttpPatch("{id}/move")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> MoveDocument(string id, [FromBody] MoveDocumentRequest request)
        {
            var document = await dbContext.Documents.FindAsync(id);
            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = $"文档 {id} 不存在" });
            }

            if (!await EnsureDocumentWritableAsync(document))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "无权移动此文档" });
            }

            if (!string.IsNullOrEmpty(request.FolderId))
            {
                var folder = await dbContext.Folders.FirstOrDefaultAsync(f => f.Id == request.FolderId && f.KnowledgeBaseId == document.KnowledgeBaseId);
                if (folder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "目标文件夹不存在或不属于当前知识库" });
                }

                document.FolderId = request.FolderId;
            }
            else
            {
                document.FolderId = null;
            }

            document.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return Ok(await MapToResponse(document));
        }

        private async Task PublishDocumentUploadAsync(Document document, string fileName)
        {
            try
            {
                await messagePublisher.PublishDocumentUploadAsync(new DocumentUploadMessage
                {
                    DocumentId = document.Id,
                    KnowledgeBaseId = document.KnowledgeBaseId ?? string.Empty,
                    ObjectKey = document.ObjectKey ?? string.Empty,
                    FileName = fileName,
                    ContentType = document.ContentType
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "发布文档处理消息失败: DocumentId={DocumentId}", document.Id);
            }
        }

        private async Task<bool> EnsureDocumentReadableAsync(Document document)
        {
            if (!string.IsNullOrEmpty(document.KnowledgeBaseId))
            {
                var auth = await dbContext.AuthorizeKnowledgeBaseAsync(document.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.View);
                return auth.HasAccess;
            }

            return document.CreatedByUserId == GetUserId();
        }

        private async Task<bool> EnsureDocumentWritableAsync(Document document)
        {
            if (!string.IsNullOrEmpty(document.KnowledgeBaseId))
            {
                var auth = await dbContext.AuthorizeKnowledgeBaseAsync(document.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
                return auth.HasAccess;
            }

            return document.CreatedByUserId == GetUserId();
        }

        private async Task DeleteDocumentArtifactsAsync(Document document)
        {
            if (!string.IsNullOrEmpty(document.ObjectKey))
            {
                try
                {
                    await objectStorage.DeleteAsync(document.ObjectKey);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "删除对象存储文件失败: {ObjectKey}", document.ObjectKey);
                }
            }

            var collectionName = GetVectorCollectionName(document);
            if (collectionName != null)
            {
                try
                {
                    await vectorStore.DeleteByFilterAsync(
                        collectionName,
                        new VectorFilter(new[]
                        {
                            new VectorCondition("document_id", "match", document.Id)
                        }));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "删除向量数据失败: DocumentId={DocumentId}, Collection={Collection}", document.Id, collectionName);
                }
            }
        }

        private static string? GetVectorCollectionName(Document document)
        {
            if (!string.IsNullOrEmpty(document.KnowledgeBaseId))
            {
                return VectorCollectionName.BuildKnowledgeBaseCollectionName(document.KnowledgeBaseId);
            }

            if (!string.IsNullOrEmpty(document.SessionId))
            {
                return VectorCollectionName.BuildSessionCollectionName(document.SessionId);
            }

            return null;
        }

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
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
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
                ".webm" => "video/webm",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".flac" => "audio/flac",
                ".aac" => "audio/aac",
                ".ogg" => "audio/ogg",
                ".m4a" => "audio/mp4",
                _ => "application/octet-stream"
            };
        }

        private static bool IsSupportedUploadContentType(string contentType)
        {
            return contentType switch
            {
                "application/pdf" => true,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => true,
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => true,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true,
                "text/markdown" => true,
                "text/plain" => true,
                "image/jpeg" or "image/png" or "image/gif" or "image/bmp" or "image/webp" => true,
                "video/mp4" or "video/quicktime" => true,
                "audio/mpeg" or "audio/wav" or "audio/mp4" => true,
                _ => false
            };
        }

        private async Task<DocumentResponse> MapToResponse(Document document)
        {
            var chunkCount = await dbContext.Chunks.CountAsync(c => c.DocumentId == document.Id);
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

        private IActionResult Forbid(string? message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = message ?? "无权访问此资源" });
        }
    }

    public record MoveDocumentRequest
    {
        public string? FolderId { get; init; }
    }
}
