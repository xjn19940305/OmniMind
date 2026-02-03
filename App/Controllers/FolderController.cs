using App.Swaggers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Folder;
using OmniMind.Entities;
using OmniMind.Persistence.MySql;

namespace App.Controllers
{
    /// <summary>
    /// 文件夹模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class FolderController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly ILogger<FolderController> logger;

        public FolderController(
            OmniMindDbContext dbContext,
            ILogger<FolderController> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        /// <summary>
        /// 创建文件夹
        /// </summary>
        [HttpPost(Name = "创建文件夹")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
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
            var currentUserId = GetUserId();

            // 验证知识库是否存在
            var knowledgeBase = await dbContext.KnowledgeBases
                .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return BadRequest(new ErrorResponse { Message = "知识库不存在" });
            }

            // 如果指定了父文件夹，验证是否存在
            if (!string.IsNullOrEmpty(request.ParentFolderId))
            {
                var parentFolder = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId && f.KnowledgeBaseId == request.KnowledgeBaseId);
                if (parentFolder == null)
                {
                    return BadRequest(new ErrorResponse { Message = "父文件夹不存在" });
                }

                // 防止循环引用
                if (request.ParentFolderId == request.KnowledgeBaseId)
                {
                    return BadRequest(new ErrorResponse { Message = "不能将自己设为父文件夹" });
                }
            }

            // 检查同级文件夹名称是否重复
            var exists = await dbContext.Folders
                .AnyAsync(f => f.KnowledgeBaseId == request.KnowledgeBaseId
                    && f.ParentFolderId == request.ParentFolderId
                    && f.Name == request.Name);
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
                CreatedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            // 计算并设置路径
            folder.Path = await CalculateFolderPath(folder);

            dbContext.Folders.Add(folder);
            await dbContext.SaveChangesAsync();

            var response = await MapToResponse(folder);
            return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, response);
        }

        /// <summary>
        /// 获取文件夹详情
        /// </summary>
        [HttpGet("{id}", Name = "获取文件夹详情")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFolder(string id)
        {
            var folder = await dbContext.Folders
                .Include(f => f.KnowledgeBase)
                .FirstOrDefaultAsync(f => f.Id == id);

            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            var response = await MapToResponse(folder);
            return Ok(response);
        }

        /// <summary>
        /// 获取文件夹树（知识库的所有文件夹，树形结构）
        /// </summary>
        [HttpGet("tree/{knowledgeBaseId}", Name = "获取文件夹树")]
        [ProducesResponseType(typeof(List<FolderTreeResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFolderTree(string knowledgeBaseId)
        {
            var folders = await dbContext.Folders
                .Where(f => f.KnowledgeBaseId == knowledgeBaseId)
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Name)
                .ToListAsync();

            var tree = BuildFolderTree(folders);
            return Ok(tree);
        }

        /// <summary>
        /// 获取知识库的文件夹列表（平铺）
        /// </summary>
        [HttpGet("list/{knowledgeBaseId}", Name = "获取文件夹列表")]
        [ProducesResponseType(typeof(List<FolderResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFolderList(
            string knowledgeBaseId,
            [FromQuery] string? parentFolderId = null)
        {
            var query = dbContext.Folders
                .Include(f => f.KnowledgeBase)
                .Where(f => f.KnowledgeBaseId == knowledgeBaseId);

            // 如果指定了 parentFolderId，只获取直接子文件夹
            if (!string.IsNullOrEmpty(parentFolderId))
            {
                query = query.Where(f => f.ParentFolderId == parentFolderId);
            }
            else if (parentFolderId == null)
            {
                // parentFolderId 为 null 或空字符串时，只获取根文件夹
                query = query.Where(f => f.ParentFolderId == null);
            }

            var folders = await query
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Name)
                .ToListAsync();

            var responses = new List<FolderResponse>();
            foreach (var folder in folders)
            {
                responses.Add(await MapToResponse(folder));
            }

            return Ok(responses);
        }

        /// <summary>
        /// 更新文件夹
        /// </summary>
        [HttpPut("{id}", Name = "更新文件夹")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateFolder(string id, [FromBody] UpdateFolderRequest request)
        {
            var folder = await dbContext.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.Name.Length > 128)
                {
                    return BadRequest(new ErrorResponse { Message = "文件夹名称长度不能超过128个字符" });
                }

                // 检查同级文件夹名称是否重复
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

            dbContext.Folders.Update(folder);
            await dbContext.SaveChangesAsync();

            var response = await MapToResponse(folder);
            return Ok(response);
        }

        /// <summary>
        /// 移动文件夹
        /// </summary>
        [HttpPatch("{id}/move", Name = "移动文件夹")]
        [ProducesResponseType(typeof(FolderResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MoveFolder(string id, [FromBody] MoveFolderRequest request)
        {
            var folder = await dbContext.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            // 验证新的父文件夹
            if (!string.IsNullOrEmpty(request.ParentFolderId))
            {
                // 不能将文件夹移动到自己或自己的子文件夹下
                if (request.ParentFolderId == id || await IsDescendantFolder(id, request.ParentFolderId))
                {
                    return BadRequest(new ErrorResponse { Message = "不能将文件夹移动到自己或子文件夹下" });
                }

                var newParent = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == request.ParentFolderId);
                if (newParent == null)
                {
                    return BadRequest(new ErrorResponse { Message = "目标父文件夹不存在" });
                }

                if (newParent.KnowledgeBaseId != folder.KnowledgeBaseId)
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

            dbContext.Folders.Update(folder);
            await dbContext.SaveChangesAsync();

            var response = await MapToResponse(folder);
            return Ok(response);
        }

        /// <summary>
        /// 删除文件夹
        /// </summary>
        [HttpDelete("{id}", Name = "删除文件夹")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteFolder(string id)
        {
            var folder = await dbContext.Folders.FindAsync(id);
            if (folder == null)
            {
                return NotFound(new ErrorResponse { Message = $"文件夹 {id} 不存在" });
            }

            // 检查是否有子文件夹
            var hasChildren = await dbContext.Folders
                .AnyAsync(f => f.ParentFolderId == id);
            if (hasChildren)
            {
                return BadRequest(new ErrorResponse { Message = "文件夹下有子文件夹，不能删除" });
            }

            // 检查是否有文档
            var hasDocuments = await dbContext.Documents
                .AnyAsync(d => d.FolderId == id);
            if (hasDocuments)
            {
                return BadRequest(new ErrorResponse { Message = "文件夹下有文档，不能删除" });
            }

            dbContext.Folders.Remove(folder);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        #region Private Methods

        /// <summary>
        /// 计算文件夹路径
        /// </summary>
        private async Task<string> CalculateFolderPath(Folder folder)
        {
            if (string.IsNullOrEmpty(folder.ParentFolderId))
            {
                return $"/{folder.Name}/";
            }

            var pathParts = new List<string>();
            pathParts.Add(folder.Name);

            var currentParentId = folder.ParentFolderId;
            while (!string.IsNullOrEmpty(currentParentId))
            {
                var parent = await dbContext.Folders
                    .FirstOrDefaultAsync(f => f.Id == currentParentId);

                if (parent == null) break;

                pathParts.Insert(0, parent.Name);
                currentParentId = parent.ParentFolderId;
            }

            return "/" + string.Join("/", pathParts) + "/";
        }

        /// <summary>
        /// 检查是否是后代文件夹
        /// </summary>
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

        /// <summary>
        /// 构建文件夹树
        /// </summary>
        private List<FolderTreeResponse> BuildFolderTree(List<Folder> folders)
        {
            var folderMap = folders.ToDictionary(
                f => f.Id,
                f => new FolderTreeResponse
                {
                    Id = f.Id,
                    ParentFolderId = f.ParentFolderId,
                    Name = f.Name,
                    Description = f.Description,
                    SortOrder = f.SortOrder,
                    CreatedAt = f.CreatedAt,
                    DocumentCount = 0 // 后续可以统计
                });

            var rootFolders = new List<FolderTreeResponse>();

            foreach (var folder in folders)
            {
                if (folderMap.TryGetValue(folder.Id, out var folderNode))
                {
                    if (string.IsNullOrEmpty(folder.ParentFolderId))
                    {
                        rootFolders.Add(folderNode);
                    }
                    else if (folderMap.TryGetValue(folder.ParentFolderId, out var parentNode))
                    {
                        parentNode.Children.Add(folderNode);
                    }
                }
            }

            return rootFolders.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToList();
        }

        /// <summary>
        /// 映射到响应对象
        /// </summary>
        private async Task<FolderResponse> MapToResponse(Folder folder)
        {
            // 统计子文件夹数量
            var childFolderCount = await dbContext.Folders
                .CountAsync(f => f.ParentFolderId == folder.Id);

            // 统计文档数量
            var documentCount = await dbContext.Documents
                .CountAsync(d => d.FolderId == folder.Id);

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

        #endregion
    }
}
