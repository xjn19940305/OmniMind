using OmniMind.Api.Swaggers;
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
        [HttpPost(Name = "创建知识库")]
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
        [HttpGet("{id}", Name = "获取知识库详情")]
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

            var response = MapToDetailResponse(knowledgeBase);
            return Ok(response);
        }

        /// <summary>
        /// 获取知识库列表
        /// </summary>
        [HttpGet(Name = "获取知识库列表")]
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
                .Include(kb => kb.Members)
                .OrderByDescending(kb => kb.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var responses = knowledgeBases.Select(MapToResponse).ToList();

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
        [HttpPut("{id}", Name = "更新知识库")]
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
        [HttpDelete("{id}", Name = "删除知识库")]
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
        [HttpPost("{id}/members", Name = "添加知识库成员")]
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
        [HttpPut("{id}/members/{userId}", Name = "更新知识库成员角色")]
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
        [HttpDelete("{id}/members/{userId}", Name = "移除知识库成员")]
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
        [HttpGet("{id}/members", Name = "获取知识库成员列表")]
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

        private static KnowledgeBaseResponse MapToResponse(KnowledgeBase kb)
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

    #region Request/Response Models

    /// <summary>
    /// 添加知识库成员请求
    /// </summary>
    public class AddKnowledgeBaseMemberRequest
    {
        public string UserId { get; set; } = string.Empty;
        public KnowledgeBaseMemberRole Role { get; set; } = KnowledgeBaseMemberRole.Viewer;
    }

    /// <summary>
    /// 更新知识库成员请求
    /// </summary>
    public class UpdateKnowledgeBaseMemberRequest
    {
        public KnowledgeBaseMemberRole Role { get; set; }
    }

    /// <summary>
    /// 知识库成员响应
    /// </summary>
    public class KnowledgeBaseMemberResponse
    {
        public string Id { get; set; } = string.Empty;
        public string KnowledgeBaseId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public KnowledgeBaseMemberRole Role { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }

    /// <summary>
    /// 知识库详情响应
    /// </summary>
    public record KnowledgeBaseDetailResponse : KnowledgeBaseResponse
    {
        public string? OwnerUserId { get; init; }
        public string? OwnerName { get; init; }
        public int MemberCount { get; init; }
        public List<MemberRef> Members { get; init; } = new();
    }

    /// <summary>
    /// 成员引用
    /// </summary>
    public record MemberRef
    {
        public string UserId { get; set; } = string.Empty;
        public string? UserName { get; set; }
        public KnowledgeBaseMemberRole Role { get; set; }
        public DateTimeOffset JoinedAt { get; set; }
    }

    #endregion
}
