using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Api.Extensions;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Folder;
using OmniMind.Entities;
using OmniMind.Persistence.PostgreSql;

namespace App.Controllers
{
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class FolderController : BaseController
    {
        private readonly OmniMindDbContext dbContext;

        public FolderController(OmniMindDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpPost]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ErrorResponse { Message = "文件夹名称不能为空" });
            }

            if (request.Name.Length > 128)
            {
                return BadRequest(new ErrorResponse { Message = "文件夹名称长度不能超过128个字符" });
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

            if (!string.IsNullOrEmpty(request.ParentFolderId))
            {
                var parentFolder = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId && f.KnowledgeBaseId == request.KnowledgeBaseId);
                if (parentFolder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "父文件夹不存在" });
                }
            }

            var exists = await dbContext.Folders
                .AnyAsync(f => f.KnowledgeBaseId == request.KnowledgeBaseId
                    && f.ParentFolderId == request.ParentFolderId
                    && f.Name == request.Name.Trim());
            if (exists)
            {
                return BadRequest(new ErrorResponse { Message = "同级文件夹名称已存在" });
            }

            var folder = new Folder
            {
                KnowledgeBaseId = request.KnowledgeBaseId,
                ParentFolderId = request.ParentFolderId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                SortOrder = request.SortOrder,
                CreatedByUserId = GetUserId(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            folder.Path = await CalculateFolderPath(folder);
            dbContext.Folders.Add(folder);
            await dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, await MapToResponse(folder));
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFolder(string id)
        {
            var folder = await dbContext.Folders
                .Include(f => f.KnowledgeBase)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(folder.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.View);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            return Ok(await MapToResponse(folder));
        }

        [HttpGet("tree/{knowledgeBaseId}")]
        [ProducesResponseType(typeof(List<FolderTreeResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFolderTree(string knowledgeBaseId)
        {
            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBaseId, GetUserId(), KnowledgeBasePermission.View);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            var folders = await dbContext.Folders
                .Where(f => f.KnowledgeBaseId == knowledgeBaseId)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Name)
                .ToListAsync();

            return Ok(BuildFolderTree(folders));
        }

        [HttpGet("list/{knowledgeBaseId}")]
        [ProducesResponseType(typeof(List<FolderResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFolderList(string knowledgeBaseId, [FromQuery] string? parentFolderId = null)
        {
            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBaseId, GetUserId(), KnowledgeBasePermission.View);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            var query = dbContext.Folders
                .Include(f => f.KnowledgeBase)
                .Where(f => f.KnowledgeBaseId == knowledgeBaseId);

            query = parentFolderId is null
                ? query.Where(f => f.ParentFolderId == null)
                : query.Where(f => f.ParentFolderId == parentFolderId);

            var folders = await query.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync();
            var responses = new List<FolderResponse>();
            foreach (var folder in folders)
            {
                responses.Add(await MapToResponse(folder));
            }

            return Ok(responses);
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateFolder(string id, [FromBody] UpdateFolderRequest request)
        {
            var folder = await dbContext.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(folder.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.Name.Length > 128)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹名称长度不能超过128个字符" });
                }

                var exists = await dbContext.Folders
                    .AnyAsync(f => f.KnowledgeBaseId == folder.KnowledgeBaseId
                        && f.ParentFolderId == folder.ParentFolderId
                        && f.Name == request.Name.Trim()
                        && f.Id != id);
                if (exists)
                {
                    return BadRequest(new ErrorResponse { Message = "同级文件夹名称已存在" });
                }

                folder.Name = request.Name.Trim();
                folder.Path = await CalculateFolderPath(folder);
            }

            if (request.Description != null)
            {
                folder.Description = request.Description.Trim();
            }

            if (request.SortOrder.HasValue)
            {
                folder.SortOrder = request.SortOrder.Value;
            }

            folder.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return Ok(await MapToResponse(folder));
        }

        [HttpPatch("{id}/move")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> MoveFolder(string id, [FromBody] MoveFolderRequest request)
        {
            var folder = await dbContext.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(folder.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (!string.IsNullOrEmpty(request.ParentFolderId))
            {
                if (request.ParentFolderId == id || await IsDescendantFolder(id, request.ParentFolderId))
                {
                    return BadRequest(new ErrorResponse { Message = "不能将文件夹移动到自身或其子文件夹下" });
                }

                var parent = await dbContext.Folders.FirstOrDefaultAsync(f => f.Id == request.ParentFolderId);
                if (parent == null)
                {
                    return BadRequest(new ErrorResponse { Message = "目标父文件夹不存在" });
                }

                if (parent.KnowledgeBaseId != folder.KnowledgeBaseId)
                {
                    return BadRequest(new ErrorResponse { Message = "不能跨知识库移动文件夹" });
                }
            }

            folder.ParentFolderId = request.ParentFolderId;
            folder.Path = await CalculateFolderPath(folder);
            if (request.SortOrder.HasValue)
            {
                folder.SortOrder = request.SortOrder.Value;
            }

            folder.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return Ok(await MapToResponse(folder));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteFolder(string id)
        {
            var folder = await dbContext.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(folder.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (await dbContext.Folders.AnyAsync(f => f.ParentFolderId == id))
            {
                return BadRequest(new ErrorResponse { Message = "文件夹下有子文件夹，不能删除" });
            }

            if (await dbContext.Documents.AnyAsync(d => d.FolderId == id))
            {
                return BadRequest(new ErrorResponse { Message = "文件夹下有文档，不能删除" });
            }

            dbContext.Folders.Remove(folder);
            await dbContext.SaveChangesAsync();
            return NoContent();
        }

        private IActionResult Forbid(string? message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = message ?? "无权访问此资源" });
        }

        private async Task<string> CalculateFolderPath(Folder folder)
        {
            if (string.IsNullOrEmpty(folder.ParentFolderId))
            {
                return $"/{folder.Name}/";
            }

            var pathParts = new List<string> { folder.Name };
            var currentParentId = folder.ParentFolderId;
            while (!string.IsNullOrEmpty(currentParentId))
            {
                var parent = await dbContext.Folders.FirstOrDefaultAsync(f => f.Id == currentParentId);
                if (parent == null)
                {
                    break;
                }

                pathParts.Insert(0, parent.Name);
                currentParentId = parent.ParentFolderId;
            }

            return "/" + string.Join("/", pathParts) + "/";
        }

        private async Task<bool> IsDescendantFolder(string folderId, string potentialDescendantId)
        {
            var current = await dbContext.Folders.FindAsync(potentialDescendantId);
            while (current != null && !string.IsNullOrEmpty(current.ParentFolderId))
            {
                if (current.ParentFolderId == folderId)
                {
                    return true;
                }

                current = await dbContext.Folders.FindAsync(current.ParentFolderId);
            }

            return false;
        }

        private static List<FolderTreeResponse> BuildFolderTree(List<Folder> folders)
        {
            var map = folders.ToDictionary(
                f => f.Id,
                f => new FolderTreeResponse
                {
                    Id = f.Id,
                    ParentFolderId = f.ParentFolderId,
                    Name = f.Name,
                    Description = f.Description,
                    SortOrder = f.SortOrder,
                    CreatedAt = f.CreatedAt,
                    DocumentCount = 0
                });

            var roots = new List<FolderTreeResponse>();
            foreach (var folder in folders)
            {
                var node = map[folder.Id];
                if (string.IsNullOrEmpty(folder.ParentFolderId))
                {
                    roots.Add(node);
                }
                else if (map.TryGetValue(folder.ParentFolderId, out var parent))
                {
                    parent.Children.Add(node);
                }
            }

            return roots.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToList();
        }

        private async Task<FolderResponse> MapToResponse(Folder folder)
        {
            var childFolderCount = await dbContext.Folders.CountAsync(f => f.ParentFolderId == folder.Id);
            var documentCount = await dbContext.Documents.CountAsync(d => d.FolderId == folder.Id);

            return new FolderResponse
            {
                Id = folder.Id,
                KnowledgeBaseId = folder.KnowledgeBaseId ?? string.Empty,
                KnowledgeBaseName = folder.KnowledgeBase?.Name,
                ParentFolderId = folder.ParentFolderId,
                Name = folder.Name,
                Path = folder.Path,
                Description = folder.Description,
                SortOrder = folder.SortOrder,
                CreatedByUserId = folder.CreatedByUserId,
                CreatedAt = folder.CreatedAt,
                UpdatedAt = folder.UpdatedAt,
                ChildFolderCount = childFolderCount,
                DocumentCount = documentCount
            };
        }
    }
}
