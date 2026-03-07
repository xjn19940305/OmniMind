using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Api.Extensions;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Ingestion;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messages;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.PostgreSql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace App.Controllers
{
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class IngestionController : BaseController
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly OmniMindDbContext dbContext;
        private readonly IMessagePublisher messagePublisher;
        private readonly ILogger<IngestionController> logger;

        public IngestionController(
            OmniMindDbContext dbContext,
            IMessagePublisher messagePublisher,
            ILogger<IngestionController> logger)
        {
            this.dbContext = dbContext;
            this.messagePublisher = messagePublisher;
            this.logger = logger;
        }

        [HttpPost("batches")]
        [ProducesResponseType(typeof(IngestionBatchResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateBatch([FromBody] CreateIngestionBatchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.KnowledgeBaseId))
            {
                return BadRequest(new ErrorResponse { Message = "知识库不能为空" });
            }

            if (string.IsNullOrWhiteSpace(request.SourceIdentifier))
            {
                return BadRequest(new ErrorResponse { Message = "来源标识不能为空" });
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

            var now = DateTimeOffset.UtcNow;
            var batch = new IngestionBatch
            {
                KnowledgeBaseId = request.KnowledgeBaseId,
                SourceKind = request.SourceKind,
                SourceIdentifier = request.SourceIdentifier.Trim(),
                ExternalTaskId = request.ExternalTaskId?.Trim(),
                RuleVersion = request.RuleVersion?.Trim(),
                MetadataJson = SerializeMetadata(request.Metadata),
                CreatedByUserId = GetUserId(),
                CreatedAt = now,
                StartedAt = now,
                UpdatedAt = now
            };

            dbContext.IngestionBatches.Add(batch);
            await dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBatch), new { id = batch.Id }, await MapBatchResponseAsync(batch));
        }

        [HttpGet("batches")]
        [ProducesResponseType(typeof(PagedResponse<IngestionBatchResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBatches(
            [FromQuery] string? knowledgeBaseId = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] int? status = null,
            [FromQuery] int? sourceKind = null)
        {
            var currentUserId = GetUserId();
            var query = dbContext.IngestionBatches.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(knowledgeBaseId))
            {
                var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBaseId, currentUserId, KnowledgeBasePermission.Edit);
                if (!auth.HasAccess)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = auth.Message ?? "无权访问此知识库的导入任务" });
                }

                query = query.Where(x => x.KnowledgeBaseId == knowledgeBaseId);
            }
            else
            {
                query = query.Where(x => x.CreatedByUserId == currentUserId);
            }

            if (status.HasValue)
            {
                query = query.Where(x => (int)x.Status == status.Value);
            }

            if (sourceKind.HasValue)
            {
                query = query.Where(x => (int)x.SourceKind == sourceKind.Value);
            }

            var totalCount = await query.CountAsync();
            var batches = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = new List<IngestionBatchResponse>(batches.Count);
            foreach (var batch in batches)
            {
                items.Add(await MapBatchResponseAsync(batch));
            }

            return Ok(new PagedResponse<IngestionBatchResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [HttpGet("batches/{id}")]
        [ProducesResponseType(typeof(IngestionBatchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBatch(string id)
        {
            var batch = await dbContext.IngestionBatches.FirstOrDefaultAsync(x => x.Id == id);
            if (batch == null)
            {
                return NotFound(new ErrorResponse { Message = $"导入批次 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(batch.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            return Ok(await MapBatchResponseAsync(batch));
        }

        [HttpPost("batches/{id}/records")]
        [ProducesResponseType(typeof(SubmitIngestionBatchRecordsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubmitBatchRecords(string id, [FromBody] SubmitIngestionBatchRecordsRequest request)
        {
            if (request.Records.Count == 0)
            {
                return BadRequest(new ErrorResponse { Message = "导入记录不能为空" });
            }

            if (request.Records.Count > 500)
            {
                return BadRequest(new ErrorResponse { Message = "单次最多提交 500 条记录" });
            }

            var batch = await dbContext.IngestionBatches.FirstOrDefaultAsync(x => x.Id == id);
            if (batch == null)
            {
                return NotFound(new ErrorResponse { Message = $"导入批次 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(batch.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (batch.Status == IngestionBatchStatus.Canceled)
            {
                return BadRequest(new ErrorResponse { Message = "已取消的批次不能继续写入记录" });
            }

            var invalidRecord = request.Records.FirstOrDefault(x => string.IsNullOrWhiteSpace(x.Title) || string.IsNullOrWhiteSpace(x.Content));
            if (invalidRecord != null)
            {
                return BadRequest(new ErrorResponse { Message = "每条记录都必须包含标题和正文" });
            }

            var folderIds = request.Records
                .Where(x => !string.IsNullOrWhiteSpace(x.FolderId))
                .Select(x => x.FolderId!)
                .Distinct()
                .ToList();

            if (folderIds.Count > 0)
            {
                var validFolderIds = await dbContext.Folders
                    .Where(x => x.KnowledgeBaseId == batch.KnowledgeBaseId && folderIds.Contains(x.Id))
                    .Select(x => x.Id)
                    .ToListAsync();

                if (validFolderIds.Count != folderIds.Count)
                {
                    return BadRequest(new ErrorResponse { Message = "存在不属于当前知识库的文件夹" });
                }
            }

            var incomingExternalIds = request.Records
                .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
                .Select(x => x.ExternalId!.Trim())
                .Distinct()
                .ToList();

            var existingExternalIds = incomingExternalIds.Count == 0
                ? new HashSet<string>()
                : (await dbContext.Documents
                    .Where(x => x.BatchId == batch.Id && x.ExternalId != null && incomingExternalIds.Contains(x.ExternalId))
                    .Select(x => x.ExternalId!)
                    .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var now = DateTimeOffset.UtcNow;
            var acceptedDocuments = new List<Document>();

            foreach (var record in request.Records)
            {
                var externalId = record.ExternalId?.Trim();
                if (!string.IsNullOrWhiteSpace(externalId) && existingExternalIds.Contains(externalId))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(externalId))
                {
                    existingExternalIds.Add(externalId);
                }

                acceptedDocuments.Add(new Document
                {
                    Id = Guid.CreateVersion7().ToString(),
                    KnowledgeBaseId = batch.KnowledgeBaseId,
                    FolderId = string.IsNullOrWhiteSpace(record.FolderId) ? null : record.FolderId.Trim(),
                    BatchId = batch.Id,
                    Title = record.Title.Trim(),
                    ContentType = string.IsNullOrWhiteSpace(record.ContentType) ? "text/plain" : record.ContentType.Trim(),
                    SourceType = SourceType.Import,
                    SourceUri = record.SourceUri?.Trim(),
                    ExternalId = externalId,
                    SourceSystem = string.IsNullOrWhiteSpace(record.SourceSystem) ? batch.SourceIdentifier : record.SourceSystem.Trim(),
                    FileHash = record.FileHash?.Trim(),
                    Language = string.IsNullOrWhiteSpace(record.Language) ? "zh-CN" : record.Language.Trim(),
                    Status = DocumentStatus.Uploaded,
                    Content = record.Content,
                    MetadataJson = SerializeMetadata(record.Metadata),
                    ContentUpdatedAt = record.ContentUpdatedAt,
                    CreatedByUserId = GetUserId(),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            if (acceptedDocuments.Count > 0)
            {
                dbContext.Documents.AddRange(acceptedDocuments);
                batch.UpdatedAt = now;
                await dbContext.SaveChangesAsync();
            }

            foreach (var document in acceptedDocuments)
            {
                await PublishDocumentUploadAsync(document);
            }

            var batchResponse = await MapBatchResponseAsync(batch);

            return Ok(new SubmitIngestionBatchRecordsResponse
            {
                BatchId = batch.Id,
                AcceptedCount = acceptedDocuments.Count,
                SkippedCount = request.Records.Count - acceptedDocuments.Count,
                DocumentIds = acceptedDocuments.Select(x => x.Id).ToList(),
                Status = batchResponse.Status
            });
        }

        [HttpPost("batches/{id}/retry")]
        [ProducesResponseType(typeof(IngestionBatchResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> RetryBatch(string id)
        {
            var batch = await dbContext.IngestionBatches.FirstOrDefaultAsync(x => x.Id == id);
            if (batch == null)
            {
                return NotFound(new ErrorResponse { Message = $"导入批次 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(batch.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            var failedDocuments = await dbContext.Documents
                .Where(x => x.BatchId == batch.Id && x.Status == DocumentStatus.Failed)
                .ToListAsync();

            var now = DateTimeOffset.UtcNow;
            foreach (var document in failedDocuments)
            {
                document.Status = DocumentStatus.Uploaded;
                document.Error = null;
                document.RetryCount = 0;
                document.LastRetryAt = null;
                document.UpdatedAt = now;
            }

            batch.Status = IngestionBatchStatus.Running;
            batch.ErrorSummary = null;
            batch.FinishedAt = null;
            batch.UpdatedAt = now;

            await dbContext.SaveChangesAsync();

            foreach (var document in failedDocuments)
            {
                await PublishDocumentUploadAsync(document);
            }

            return Ok(await MapBatchResponseAsync(batch));
        }

        [HttpPost("batches/{id}/cancel")]
        [ProducesResponseType(typeof(IngestionBatchResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CancelBatch(string id)
        {
            var batch = await dbContext.IngestionBatches.FirstOrDefaultAsync(x => x.Id == id);
            if (batch == null)
            {
                return NotFound(new ErrorResponse { Message = $"导入批次 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(batch.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (batch.Status != IngestionBatchStatus.Canceled)
            {
                batch.Status = IngestionBatchStatus.Canceled;
                batch.ErrorSummary ??= "批次已由用户取消";
                batch.FinishedAt = DateTimeOffset.UtcNow;
                batch.UpdatedAt = batch.FinishedAt;
                await dbContext.SaveChangesAsync();
            }

            return Ok(await MapBatchResponseAsync(batch));
        }

        private async Task PublishDocumentUploadAsync(Document document)
        {
            try
            {
                await messagePublisher.PublishDocumentUploadAsync(new DocumentUploadMessage
                {
                    DocumentId = document.Id,
                    KnowledgeBaseId = document.KnowledgeBaseId ?? string.Empty,
                    ObjectKey = document.ObjectKey ?? string.Empty,
                    FileName = document.Title,
                    ContentType = document.ContentType
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "发布导入文档处理消息失败: DocumentId={DocumentId}", document.Id);
            }
        }

        private async Task<IngestionBatchResponse> MapBatchResponseAsync(IngestionBatch batch)
        {
            var totalCount = await dbContext.Documents.CountAsync(x => x.BatchId == batch.Id);
            var successCount = await dbContext.Documents.CountAsync(x => x.BatchId == batch.Id && x.Status == DocumentStatus.Indexed);
            var failedCount = await dbContext.Documents.CountAsync(x => x.BatchId == batch.Id && x.Status == DocumentStatus.Failed);
            var pendingCount = Math.Max(0, totalCount - successCount - failedCount);

            var status = batch.Status switch
            {
                IngestionBatchStatus.Canceled => IngestionBatchStatus.Canceled,
                _ when totalCount == 0 => batch.Status,
                _ when successCount == totalCount => IngestionBatchStatus.Success,
                _ when failedCount == totalCount => IngestionBatchStatus.Failed,
                _ when successCount + failedCount == totalCount && failedCount > 0 => IngestionBatchStatus.PartialSuccess,
                _ => IngestionBatchStatus.Running
            };

            return new IngestionBatchResponse
            {
                Id = batch.Id,
                KnowledgeBaseId = batch.KnowledgeBaseId,
                SourceKind = batch.SourceKind,
                SourceIdentifier = batch.SourceIdentifier,
                ExternalTaskId = batch.ExternalTaskId,
                RuleVersion = batch.RuleVersion,
                TotalCount = totalCount,
                SuccessCount = successCount,
                FailedCount = failedCount,
                PendingCount = pendingCount,
                Status = status,
                ErrorSummary = batch.ErrorSummary,
                CreatedByUserId = batch.CreatedByUserId,
                CreatedAt = batch.CreatedAt,
                StartedAt = batch.StartedAt,
                FinishedAt = batch.FinishedAt,
                UpdatedAt = batch.UpdatedAt,
                Metadata = DeserializeMetadata(batch.MetadataJson)
            };
        }

        private static string? SerializeMetadata(Dictionary<string, string?>? metadata)
        {
            return metadata == null || metadata.Count == 0
                ? null
                : JsonSerializer.Serialize(metadata, JsonOptions);
        }

        private static Dictionary<string, string?>? DeserializeMetadata(string? metadataJson)
        {
            if (string.IsNullOrWhiteSpace(metadataJson))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string?>>(metadataJson, JsonOptions);
            }
            catch
            {
                return null;
            }
        }
    }
}
