using App.Swaggers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Abstractions.Storage;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Document;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.MySql;
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

        public DocumentController(
            OmniMindDbContext dbContext,
            ILogger<DocumentController> logger,
            IObjectStorage objectStorage)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.objectStorage = objectStorage;
        }

        /// <summary>
        /// 上传文档
        /// </summary>
        [HttpPost("upload", Name = "上传文档")]
        [ProducesResponseType(typeof(DocumentResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [RequestSizeLimit(100_000_000)] // 100MB
        public async Task<IActionResult> UploadDocument(
            [FromForm] IFormFile file,
            [FromForm] string knowledgeBaseId,
            [FromForm] string? folderId = null,
            [FromForm] string? title = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new ErrorResponse { Message = "文件不能为空" });
            }

            var maxSize = 100L * 1024 * 1024; // 100MB
            if (file.Length > maxSize)
            {
                return BadRequest(new ErrorResponse { Message = "文件大小不能超过 100MB" });
            }

            var tenantId = GetTenantId();
            var currentUserId = GetUserId();

            // 验证知识库是否存在
            var knowledgeBase = await dbContext.KnowledgeBases
                .FirstOrDefaultAsync(kb => kb.Id == knowledgeBaseId);
            if (knowledgeBase == null)
            {
                return BadRequest(new ErrorResponse { Message = "知识库不存在" });
            }

            // 如果指定了文件夹，验证是否存在且属于该知识库
            if (!string.IsNullOrEmpty(folderId))
            {
                var folder = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == folderId && f.KnowledgeBaseId == knowledgeBaseId);
                if (folder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹不存在或不属于该知识库" });
                }
            }

            // 生成文档ID和对象Key
            var documentId = Guid.CreateVersion7().ToString();
            var fileName = file.FileName;
            var objectKey = MinioObjectStorage.GenerateTenantObjectKey(tenantId, documentId, fileName);

            // 准备元数据（原始文件名等）
            var metadata = new Dictionary<string, string>
            {
                ["original-filename"] = fileName,
                ["upload-timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
                ["content-type"] = file.ContentType,
                ["content-length"] = file.Length.ToString()
            };

            // 上传文件到 MinIO（带元数据）
            try
            {
                using var stream = file.OpenReadStream();
                await objectStorage.PutAsync(objectKey, stream, file.ContentType, metadata);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "上传文件到 MinIO 失败: {ObjectKey}", objectKey);
                return BadRequest(new ErrorResponse { Message = "文件上传失败，请稍后重试" });
            }

            // 计算文件 Hash (简化版，实际应该用 MD5 或 SHA256)
            var fileHash = $"{file.Length}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // 确定内容类型
            var contentType = DetermineContentType(fileName);

            // 创建文档记录
            var document = new Document
            {
                Id = documentId,
                TenantId = tenantId,
                KnowledgeBaseId = knowledgeBaseId,
                FolderId = folderId,
                WorkspaceId = await GetUserFirstWorkspaceId(),
                Title = (title ?? Path.GetFileNameWithoutExtension(fileName)).Trim(),
                ContentType = contentType,
                SourceType = SourceType.Upload,
                SourceUri = null,
                ObjectKey = objectKey,
                FileHash = fileHash,
                Language = "zh-CN", // 默认中文，后续可以自动检测
                Status = DocumentStatus.Uploaded,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            var response = await MapToResponse(document);
            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, response);
        }

        /// <summary>
        /// 创建文档（从 URL 或其他来源）
        /// </summary>
        [HttpPost(Name = "创建文档")]
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

            var tenantId = GetTenantId();
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

            // 如果是从 URL 导入，需要先下载内容
            string? objectKey = request.ObjectKey;
            if (request.SourceType == SourceType.Url && !string.IsNullOrEmpty(request.SourceUri))
            {
                // TODO: 实现从 URL 下载并上传到 MinIO
                objectKey = MinioObjectStorage.GenerateTenantObjectKey(tenantId, documentId, "imported.txt");
            }

            var document = new Document
            {
                Id = documentId,
                TenantId = tenantId,
                KnowledgeBaseId = request.KnowledgeBaseId,
                FolderId = request.FolderId,
                WorkspaceId = await GetUserFirstWorkspaceId(),
                Title = request.Title.Trim(),
                ContentType = request.ContentType,
                SourceType = request.SourceType,
                SourceUri = request.SourceUri,
                ObjectKey = objectKey,
                FileHash = request.FileHash,
                Language = request.Language,
                Status = DocumentStatus.Uploaded,
                CreatedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Documents.Add(document);
            await dbContext.SaveChangesAsync();

            var response = await MapToResponse(document);
            return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, response);
        }

        /// <summary>
        /// 获取文档详情
        /// </summary>
        [HttpGet("{id}", Name = "获取文档详情")]
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

            var response = await MapToResponse(document);
            return Ok(response);
        }

        /// <summary>
        /// 获取文档列表
        /// </summary>
        [HttpGet(Name = "获取文档列表")]
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

            // 知识库筛选
            if (!string.IsNullOrEmpty(knowledgeBaseId))
            {
                query = query.Where(d => d.KnowledgeBaseId == knowledgeBaseId);
            }

            // 文件夹筛选
            if (!string.IsNullOrEmpty(folderId))
            {
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
        [HttpDelete("{id}", Name = "删除文档")]
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
        [HttpPatch("{id}/move", Name = "移动文档")]
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
        /// 根据文件名确定内容类型
        /// </summary>
        private ContentType DetermineContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => ContentType.Pdf,
                ".doc" or ".docx" => ContentType.Docx,
                ".ppt" or ".pptx" => ContentType.Pptx,
                ".md" or ".markdown" => ContentType.Markdown,
                ".txt" => ContentType.Markdown,
                ".htm" or ".html" => ContentType.Web,
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => ContentType.Image,
                ".mp4" or ".avi" or ".mov" or ".wmv" or ".flv" or ".mkv" => ContentType.Video,
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => ContentType.Audio,
                _ => ContentType.Markdown // 默认作为 Markdown 处理
            };
        }

        /// <summary>
        /// 获取用户的第一个工作空间ID
        /// </summary>
        private async Task<string> GetUserFirstWorkspaceId()
        {
            var currentUserId = GetUserId();
            var firstWorkspace = await dbContext.WorkspaceMembers
                .Where(m => m.UserId == currentUserId)
                .Join(dbContext.Workspaces, m => m.WorkspaceId, w => w.Id, (m, w) => w)
                .FirstOrDefaultAsync();

            return firstWorkspace?.Id ?? string.Empty;
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
                FileHash = document.FileHash,
                Language = document.Language,
                Status = document.Status,
                Error = document.Error,
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
