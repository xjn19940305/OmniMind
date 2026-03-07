using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Api.Extensions;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.KnowledgeBase;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;

namespace App.Controllers
{
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class KnowledgeBaseController : BaseController
    {
        private readonly OmniMindDbContext dbContext;

        public KnowledgeBaseController(OmniMindDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

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

            var saved = await dbContext.KnowledgeBases
                .Include(kb => kb.Owner)
                .Include(kb => kb.Members)
                .FirstAsync(kb => kb.Id == knowledgeBase.Id);

            return CreatedAtAction(nameof(GetKnowledgeBase), new { id = saved.Id }, MapToResponse(saved, saved.Members.Count));
        }

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

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.View);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            return Ok(MapToDetailResponse(knowledgeBase));
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResponse<KnowledgeBaseResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetKnowledgeBases(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? keyword = null,
            [FromQuery] int? visibility = null)
        {
            var currentUserId = GetUserId();
            var memberKnowledgeBaseIds = await dbContext.KnowledgeBaseMembers
                .Where(m => m.UserId == currentUserId)
                .Select(m => m.KnowledgeBaseId)
                .ToListAsync();

            var query = dbContext.KnowledgeBases
                .Where(kb => kb.OwnerUserId == currentUserId
                    || memberKnowledgeBaseIds.Contains(kb.Id)
                    || kb.Visibility == Visibility.Public);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(kb => kb.Name.Contains(keyword) || (kb.Description != null && kb.Description.Contains(keyword)));
            }

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

            var kbIds = knowledgeBases.Select(kb => kb.Id).ToList();
            var memberCounts = await dbContext.KnowledgeBaseMembers
                .Where(m => kbIds.Contains(m.KnowledgeBaseId))
                .GroupBy(m => m.KnowledgeBaseId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);

            return Ok(new PagedResponse<KnowledgeBaseResponse>
            {
                Items = knowledgeBases.Select(kb => MapToResponse(kb, memberCounts.GetValueOrDefault(kb.Id, 0))).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [HttpPut("{id}")]
        [ProducesResponseType(typeof(KnowledgeBaseResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateKnowledgeBase(string id, [FromBody] UpdateKnowledgeBaseRequest request)
        {
            var knowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.Owner)
                .FirstOrDefaultAsync(kb => kb.Id == id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.Edit);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
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
            await dbContext.SaveChangesAsync();

            var memberCount = await dbContext.KnowledgeBaseMembers.CountAsync(m => m.KnowledgeBaseId == id);
            return Ok(MapToResponse(knowledgeBase, memberCount));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteKnowledgeBase(string id)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.Delete);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            dbContext.KnowledgeBases.Remove(knowledgeBase);
            await dbContext.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/members")]
        [ProducesResponseType(typeof(KnowledgeBaseMemberResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> AddMember(string id, [FromBody] AddKnowledgeBaseMemberRequest request)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.ManageMembers);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (request.UserId == knowledgeBase.OwnerUserId)
            {
                return BadRequest(new ErrorResponse { Message = "拥有者无需重复添加为成员" });
            }

            var userExists = await dbContext.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
            {
                return BadRequest(new ErrorResponse { Message = "目标用户不存在" });
            }

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
                InvitedByUserId = GetUserId(),
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.KnowledgeBaseMembers.Add(member);
            await dbContext.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMembers), new { id }, MapToMemberResponse(member));
        }

        [HttpPut("{id}/members/{userId}")]
        [ProducesResponseType(typeof(KnowledgeBaseMemberResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> UpdateMember(string id, string userId, [FromBody] UpdateKnowledgeBaseMemberRequest request)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.ManageMembers);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (userId == knowledgeBase.OwnerUserId)
            {
                return BadRequest(new ErrorResponse { Message = "不能修改拥有者角色" });
            }

            var member = await dbContext.KnowledgeBaseMembers
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == id && m.UserId == userId);
            if (member == null)
            {
                return NotFound(new ErrorResponse { Message = "成员不存在" });
            }

            member.Role = request.Role;
            await dbContext.SaveChangesAsync();
            return Ok(MapToMemberResponse(member));
        }

        [HttpDelete("{id}/members/{userId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> RemoveMember(string id, string userId)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.ManageMembers);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (userId == knowledgeBase.OwnerUserId)
            {
                return BadRequest(new ErrorResponse { Message = "不能移除拥有者" });
            }

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

        [HttpGet("{id}/members")]
        [ProducesResponseType(typeof(List<KnowledgeBaseMemberResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMembers(string id)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.ManageMembers);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            var members = await dbContext.KnowledgeBaseMembers
                .Include(m => m.User)
                .Where(m => m.KnowledgeBaseId == id)
                .OrderBy(m => m.Role)
                .ToListAsync();

            return Ok(members.Select(MapToMemberResponse).ToList());
        }

        [HttpGet("{id}/files")]
        [ProducesResponseType(typeof(FileListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFileList(string id, [FromQuery] string? folderId = null, [FromQuery] string? keyword = null)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.View);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            var path = new List<FolderBreadcrumbItem>();
            if (!string.IsNullOrEmpty(folderId))
            {
                var currentFolder = await dbContext.Folders.FindAsync(folderId);
                while (currentFolder != null && currentFolder.KnowledgeBaseId == id)
                {
                    path.Insert(0, new FolderBreadcrumbItem { Id = currentFolder.Id, Name = currentFolder.Name });
                    currentFolder = string.IsNullOrEmpty(currentFolder.ParentFolderId)
                        ? null
                        : await dbContext.Folders.FindAsync(currentFolder.ParentFolderId);
                }
            }

            var folderQuery = dbContext.Folders.Where(f => f.KnowledgeBaseId == id);
            folderQuery = string.IsNullOrEmpty(folderId)
                ? folderQuery.Where(f => f.ParentFolderId == null)
                : folderQuery.Where(f => f.ParentFolderId == folderId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                folderQuery = folderQuery.Where(f => f.Name.Contains(keyword));
            }

            var documentQuery = dbContext.Documents.Where(d => d.KnowledgeBaseId == id);
            documentQuery = string.IsNullOrEmpty(folderId)
                ? documentQuery.Where(d => d.FolderId == null)
                : documentQuery.Where(d => d.FolderId == folderId);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                documentQuery = documentQuery.Where(d => d.Title.Contains(keyword));
            }

            var folders = await folderQuery.OrderBy(f => f.SortOrder).ThenBy(f => f.Name).ToListAsync();
            var documents = await documentQuery.OrderBy(d => d.Title).ToListAsync();

            var items = new List<FileItemResponse>();
            items.AddRange(folders.Select(folder => new FileItemResponse
            {
                Id = folder.Id,
                Type = FileItemType.Folder,
                Name = folder.Name,
                Description = folder.Description,
                CreatedAt = folder.CreatedAt,
                UpdatedAt = folder.UpdatedAt
            }));
            items.AddRange(documents.Select(doc => new FileItemResponse
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
            }));

            return Ok(new FileListResponse
            {
                KnowledgeBaseId = id,
                CurrentFolderId = folderId,
                Path = path,
                Items = items,
                FolderCount = folders.Count,
                DocumentCount = documents.Count
            });
        }

        private IActionResult Forbid(string? message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = message ?? "无权访问此资源" });
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
                MemberCount = memberCount
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
                }).ToList() ?? new List<MemberRef>()
            };
        }

        private static KnowledgeBaseMemberResponse MapToMemberResponse(KnowledgeBaseMember member)
        {
            return new KnowledgeBaseMemberResponse
            {
                Id = member.Id,
                KnowledgeBaseId = member.KnowledgeBaseId,
                UserId = member.UserId,
                UserName = member.User?.NickName ?? member.User?.UserName,
                Role = member.Role,
                CreatedAt = member.CreatedAt
            };
        }
    }
}
