using OmniMind.Api.Swaggers;
using OmniMind.Api.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.KnowledgeBase;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;

namespace App.Controllers
{
    /// <summary>
    /// 知识库模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class KnowledgeBaseController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly ILogger<KnowledgeBaseController> logger;

        public KnowledgeBaseController(
            OmniMindDbContext dbContext,
            ILogger<KnowledgeBaseController> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        /// <summary>
        /// 创建知识库
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(KnowledgeBaseResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateKnowledgeBase([FromBody] CreateKnowledgeBaseRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ErrorResponse { Message = "知识库名称不能为空" });
            }

            if (request.Name.Length > 128)
            {
                return BadRequest(new ErrorResponse { Message = "知识库名称长度不能超过128个字符" });
            }

            var currentUserId = GetUserId();

            var knowledgeBase = new KnowledgeBase
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Visibility = request.Visibility,
                OwnerUserId = currentUserId,
                IndexProfileId = request.IndexProfileId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.KnowledgeBases.Add(knowledgeBase);
            await dbContext.SaveChangesAsync();

            // 重新查询以加载导航属性
            var savedKnowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.Owner)
                .Include(kb => kb.Members)
                .FirstAsync(kb => kb.Id == knowledgeBase.Id);

            var response = MapToResponse(savedKnowledgeBase);
            return CreatedAtAction(nameof(GetKnowledgeBase), new { id = knowledgeBase.Id }, response);
        }

        /// <summary>
        /// 获取知识库详情
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(KnowledgeBaseDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetKnowledgeBase(string id)
        {
            var knowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.Owner)
                .Include(kb => kb.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(kb => kb.Id == id);

            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            // 权限检查
            var authResult = await dbContext.CheckKnowledgeBaseAccessAsync(knowledgeBase, GetUserId());
            if (!authResult.HasAccess)
            {
                return StatusCode(403, new ErrorResponse { Message = authResult.Message ?? "无权访问此知识库" });
            }

            var response = MapToDetailResponse(knowledgeBase);
            return Ok(response);
        }

        /// <summary>
        /// 获取知识库列表
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<KnowledgeBaseResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetKnowledgeBases(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? keyword = null,
            [FromQuery] int? visibility = null)
        {
            var currentUserId = GetUserId();

            // 查询用户有权限访问的知识库：1) 自己拥有的  2) 被邀请为成员的  3) 公开的
            var memberKnowledgeBaseIds = await dbContext.KnowledgeBaseMembers
                .Where(m => m.UserId == currentUserId)
                .Select(m => m.KnowledgeBaseId)
                .ToListAsync();

            var query = dbContext.KnowledgeBases
                .Where(kb => kb.OwnerUserId == currentUserId
                    || memberKnowledgeBaseIds.Contains(kb.Id)
                    || kb.Visibility == Visibility.Public);

            // 关键字搜索
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(kb => kb.Name.Contains(keyword) || (kb.Description != null && kb.Description.Contains(keyword)));
            }

            // 可见性筛选
            if (visibility.HasValue)
            {
                query = query.Where(kb => (int)kb.Visibility == visibility.Value);
            }

            var totalCount = await query.CountAsync();

            var knowledgeBases = await query
                .Include(kb => kb.Owner)
                .OrderByDescending(kb => kb.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 获取成员数量
            var kbIds = knowledgeBases.Select(kb => kb.Id).ToList();
            var memberCounts = await dbContext.KnowledgeBaseMembers
                .Where(m => kbIds.Contains(m.KnowledgeBaseId))
                .GroupBy(m => m.KnowledgeBaseId)
                .Select(g => new { KnowledgeBaseId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.KnowledgeBaseId, x => x.Count);

            var responses = knowledgeBases.Select(kb => MapToResponse(kb, memberCounts.GetValueOrDefault(kb.Id, 0))).ToList();

            return Ok(new PagedResponse<KnowledgeBaseResponse>
            {
                Items = responses,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// 更新知识库
        /// </summary>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(KnowledgeBaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateKnowledgeBase(string id, [FromBody] UpdateKnowledgeBaseRequest request)
        {
            var knowledgeBase = await dbContext.KnowledgeBases
                .FirstOrDefaultAsync(kb => kb.Id == id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.Name.Length > 128)
                {
                    return BadRequest(new ErrorResponse { Message = "知识库名称长度不能超过128个字符" });
                }
                knowledgeBase.Name = request.Name.Trim();
            }

            if (request.Description != null)
            {
                knowledgeBase.Description = request.Description.Trim();
            }

            if (request.Visibility.HasValue)
            {
                knowledgeBase.Visibility = request.Visibility.Value;
            }

            if (request.IndexProfileId.HasValue)
            {
                knowledgeBase.IndexProfileId = request.IndexProfileId.Value;
            }

            knowledgeBase.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.KnowledgeBases.Update(knowledgeBase);
            await dbContext.SaveChangesAsync();

            var response = MapToResponse(knowledgeBase);
            return Ok(response);
        }

        /// <summary>
        /// 删除知识库
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteKnowledgeBase(string id)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            dbContext.KnowledgeBases.Remove(knowledgeBase);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 添加知识库成员
        /// </summary>
        [HttpPost("{id}/members")]
        [ProducesResponseType(typeof(KnowledgeBaseMemberResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddMember(string id, [FromBody] AddKnowledgeBaseMemberRequest request)
        {
            var currentUserId = GetUserId();

            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            // 检查用户是否已是成员
            var existingMember = await dbContext.KnowledgeBaseMembers
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == id && m.UserId == request.UserId);

            if (existingMember != null)
            {
                return BadRequest(new ErrorResponse { Message = "该用户已是知识库成员" });
            }

            var member = new KnowledgeBaseMember
            {
                KnowledgeBaseId = id,
                UserId = request.UserId,
                Role = request.Role,
                InvitedByUserId = currentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.KnowledgeBaseMembers.Add(member);
            await dbContext.SaveChangesAsync();

            var response = MapToMemberResponse(member);
            return CreatedAtAction(nameof(GetKnowledgeBase), new { id }, response);
        }

        /// <summary>
        /// 更新知识库成员角色
        /// </summary>
        [HttpPut("{id}/members/{userId}")]
        [ProducesResponseType(typeof(KnowledgeBaseMemberResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMember(string id, string userId, [FromBody] UpdateKnowledgeBaseMemberRequest request)
        {
            var member = await dbContext.KnowledgeBaseMembers
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == id && m.UserId == userId);

            if (member == null)
            {
                return NotFound(new ErrorResponse { Message = "成员不存在" });
            }

            member.Role = request.Role;
            await dbContext.SaveChangesAsync();

            var response = MapToMemberResponse(member);
            return Ok(response);
        }

        /// <summary>
        /// 移除知识库成员
        /// </summary>
        [HttpDelete("{id}/members/{userId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveMember(string id, string userId)
        {
            var member = await dbContext.KnowledgeBaseMembers
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == id && m.UserId == userId);

            if (member == null)
            {
                return NotFound(new ErrorResponse { Message = "成员不存在" });
            }

            dbContext.KnowledgeBaseMembers.Remove(member);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 获取知识库成员列表
        /// </summary>
        [HttpGet("{id}/members")]
        [ProducesResponseType(typeof(List<KnowledgeBaseMemberResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMembers(string id)
        {
            var members = await dbContext.KnowledgeBaseMembers
                .Include(m => m.User)
                .Where(m => m.KnowledgeBaseId == id)
                .OrderBy(m => m.Role)
                .ToListAsync();

            var responses = members.Select(MapToMemberResponse).ToList();
            return Ok(responses);
        }

        /// <summary>
        /// 获取知识库文件列表（文件夹+文档合并）
        /// </summary>
        [HttpGet("{id}/files")]
        [ProducesResponseType(typeof(FileListResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetFileList(
            string id,
            [FromQuery] string? folderId = null,
            [FromQuery] string? keyword = null)
        {
            // 验证知识库是否存在
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            // 构建面包屑路径
            var path = new List<FolderBreadcrumbItem>();
            if (!string.IsNullOrEmpty(folderId))
            {
                var currentFolder = await dbContext.Folders.FindAsync(folderId);
                while (currentFolder != null && currentFolder.KnowledgeBaseId == id)
                {
                    path.Insert(0, new FolderBreadcrumbItem
                    {
                        Id = currentFolder.Id,
                        Name = currentFolder.Name
                    });

                    if (!string.IsNullOrEmpty(currentFolder.ParentFolderId))
                    {
                        currentFolder = await dbContext.Folders.FindAsync(currentFolder.ParentFolderId);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // 获取子文件夹
            var folderQuery = dbContext.Folders
                .Where(f => f.KnowledgeBaseId == id);
            if (string.IsNullOrEmpty(folderId))
            {
                // 根目录：ParentFolderId 为 null
                folderQuery = folderQuery.Where(f => f.ParentFolderId == null);
            }
            else
            {
                // 子目录：ParentFolderId 等于指定的 folderId
                folderQuery = folderQuery.Where(f => f.ParentFolderId == folderId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                folderQuery = folderQuery.Where(f => f.Name.Contains(keyword));
            }

            var folders = await folderQuery
                .OrderBy(f => f.SortOrder)
                .ThenBy(f => f.Name)
                .ToListAsync();

            // 获取文档
            var documentQuery = dbContext.Documents
                .Where(d => d.KnowledgeBaseId == id);

            if (string.IsNullOrEmpty(folderId))
            {
                // 根目录：FolderId 为 null
                documentQuery = documentQuery.Where(d => d.FolderId == null);
            }
            else
            {
                // 子目录：FolderId 等于指定的 folderId
                documentQuery = documentQuery.Where(d => d.FolderId == folderId);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                documentQuery = documentQuery.Where(d => d.Title.Contains(keyword));
            }

            var documents = await documentQuery
                .OrderBy(d => d.Title)
                .ToListAsync();

            // 构建响应
            var items = new List<FileItemResponse>();

            // 添加文件夹项
            foreach (var folder in folders)
            {
                items.Add(new FileItemResponse
                {
                    Id = folder.Id,
                    Type = FileItemType.Folder,
                    Name = folder.Name,
                    Description = folder.Description,
                    CreatedAt = folder.CreatedAt,
                    UpdatedAt = folder.UpdatedAt
                });
            }

            // 添加文档项
            foreach (var doc in documents)
            {
                items.Add(new FileItemResponse
                {
                    Id = doc.Id,
                    Type = FileItemType.Document,
                    Name = doc.Title,
                    ContentType = doc.ContentType,
                    Status = doc.Status,
                    SourceType = doc.SourceType,
                    FileSize = doc.FileSize,
                    Content = doc.Content,
                    CreatedAt = doc.CreatedAt,
                    UpdatedAt = doc.UpdatedAt
                });
            }

            var response = new FileListResponse
            {
                KnowledgeBaseId = id,
                CurrentFolderId = folderId,
                Path = path,
                Items = items,
                FolderCount = folders.Count,
                DocumentCount = documents.Count
            };

            return Ok(response);
        }

        private static KnowledgeBaseResponse MapToResponse(KnowledgeBase kb, int memberCount = 0)
        {
            return new KnowledgeBaseResponse
            {
                Id = kb.Id,
                Name = kb.Name,
                Description = kb.Description,
                Visibility = kb.Visibility,
                IndexProfileId = kb.IndexProfileId,
                CreatedAt = kb.CreatedAt,
                UpdatedAt = kb.UpdatedAt,
                OwnerUserId = kb.OwnerUserId,
                OwnerName = kb.Owner?.NickName ?? kb.Owner?.UserName,
                MemberCount = memberCount,
                WorkspaceCount = 0,
                Workspaces = null
            };
        }

        private static KnowledgeBaseDetailResponse MapToDetailResponse(KnowledgeBase kb)
        {
            return new KnowledgeBaseDetailResponse
            {
                Id = kb.Id,
                Name = kb.Name,
                Description = kb.Description,
                Visibility = kb.Visibility,
                IndexProfileId = kb.IndexProfileId,
                CreatedAt = kb.CreatedAt,
                UpdatedAt = kb.UpdatedAt,
                OwnerUserId = kb.OwnerUserId,
                OwnerName = kb.Owner?.NickName ?? kb.Owner?.UserName,
                MemberCount = kb.Members?.Count ?? 0,
                Members = kb.Members?.Select(m => new MemberRef
                {
                    UserId = m.UserId,
                    UserName = m.User?.NickName ?? m.User?.UserName,
                    Role = m.Role,
                    JoinedAt = m.CreatedAt
                }).ToList() ?? new List<MemberRef>(),
                WorkspaceCount = 0,
                Workspaces = null
            };
        }

        private static KnowledgeBaseMemberResponse MapToMemberResponse(KnowledgeBaseMember m)
        {
            return new KnowledgeBaseMemberResponse
            {
                Id = m.Id,
                KnowledgeBaseId = m.KnowledgeBaseId,
                UserId = m.UserId,
                Role = m.Role,
                CreatedAt = m.CreatedAt
            };
        }
    }
}
