using App.Swaggers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.Workspace;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.MySql;

namespace App.Controllers
{
    /// <summary>
    /// 工作空间模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class WorkspaceController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly ILogger<WorkspaceController> logger;

        public WorkspaceController(
            OmniMindDbContext dbContext,
            ILogger<WorkspaceController> logger)
        {
            this.dbContext = dbContext;
            this.logger = logger;
        }

        /// <summary>
        /// 创建工作空间
        /// </summary>
        [HttpPost(Name = "创建工作空间")]
        [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new ErrorResponse { Message = "工作空间名称不能为空" });
            }

            if (request.Name.Length > 128)
            {
                return BadRequest(new ErrorResponse { Message = "工作空间名称长度不能超过128个字符" });
            }

            var userId = GetUserId();

            var workspace = new Workspace
            {
                Name = request.Name.Trim(),
                Type = request.Type,
                OwnerUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Workspaces.Add(workspace);

            // 创建者自动成为 Owner
            var ownerMember = new WorkspaceMember
            {
                WorkspaceId = workspace.Id,
                UserId = userId,
                Role = WorkspaceRole.Owner,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.WorkspaceMembers.Add(ownerMember);

            await dbContext.SaveChangesAsync();

            var response = MapToResponse(workspace);
            return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, response);
        }

        /// <summary>
        /// 获取工作空间详情
        /// </summary>
        [HttpGet("{id}", Name = "获取工作空间详情")]
        [ProducesResponseType(typeof(WorkspaceDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetWorkspace(string id)
        {
            var workspace = await dbContext.Workspaces
                .Include(w => w.KnowledgeBaseLinks)
                    .ThenInclude(link => link.KnowledgeBase)
                .Include(w => w.Members)
                .FirstOrDefaultAsync(w => w.Id == id);

            if (workspace == null)
            {
                return NotFound(new ErrorResponse { Message = $"工作空间 {id} 不存在" });
            }

            var response = MapToDetailResponse(workspace);
            return Ok(response);
        }

        /// <summary>
        /// 获取工作空间列表
        /// </summary>
        [HttpGet(Name = "获取工作空间列表")]
        [ProducesResponseType(typeof(PagedResponse<WorkspaceResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWorkspaces(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? keyword = null,
            [FromQuery] int? type = null)
        {
            var currentUserId = GetUserId();

            // 用户只能看到：1) 自己创建的工作空间  2) 被邀请加入的工作空间
            var myWorkspaceIds = await dbContext.WorkspaceMembers
                .Where(m => m.UserId == currentUserId)
                .Select(m => m.WorkspaceId)
                .ToListAsync();

            var query = dbContext.Workspaces
                .Where(w => w.OwnerUserId == currentUserId || myWorkspaceIds.Contains(w.Id));

            // 关键字搜索
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(w => w.Name.Contains(keyword));
            }

            // 类型筛选
            if (type.HasValue)
            {
                query = query.Where(w => (int)w.Type == type.Value);
            }

            var totalCount = await query.CountAsync();

            var workspaces = await query
                .OrderByDescending(w => w.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var responses = workspaces.Select(MapToResponse).ToList();

            return Ok(new PagedResponse<WorkspaceResponse>
            {
                Items = responses,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// 更新工作空间
        /// </summary>
        [HttpPut("{id}", Name = "更新工作空间")]
        [ProducesResponseType(typeof(WorkspaceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateWorkspace(string id, [FromBody] UpdateWorkspaceRequest request)
        {
            var workspace = await dbContext.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id);
            if (workspace == null)
            {
                return NotFound(new ErrorResponse { Message = $"工作空间 {id} 不存在" });
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.Name.Length > 128)
                {
                    return BadRequest(new ErrorResponse { Message = "工作空间名称长度不能超过128个字符" });
                }
                workspace.Name = request.Name.Trim();
            }

            if (request.Type.HasValue)
            {
                workspace.Type = request.Type.Value;
            }

            workspace.UpdatedAt = DateTimeOffset.UtcNow;

            dbContext.Workspaces.Update(workspace);
            await dbContext.SaveChangesAsync();

            var response = MapToResponse(workspace);
            return Ok(response);
        }

        /// <summary>
        /// 删除工作空间
        /// </summary>
        [HttpDelete("{id}", Name = "删除工作空间")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteWorkspace(string id)
        {
            var workspace = await dbContext.Workspaces
                .FirstOrDefaultAsync(w => w.Id == id);
            if (workspace == null)
            {
                return NotFound(new ErrorResponse { Message = $"工作空间 {id} 不存在" });
            }

            // 检查是否有关联的知识库
            var kbCount = await dbContext.KnowledgeBaseWorkspaces
                .CountAsync(link => link.WorkspaceId == id);

            if (kbCount > 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Message = $"该工作空间关联了 {kbCount} 个知识库，请先卸载所有知识库后再删除工作空间"
                });
            }

            dbContext.Workspaces.Remove(workspace);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 添加工作空间成员
        /// </summary>
        [HttpPost("{id}/members", Name = "添加工作空间成员")]
        [ProducesResponseType(typeof(WorkspaceMemberResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddMember(string id, [FromBody] AddMemberRequest request)
        {

            var workspace = await dbContext.Workspaces.FindAsync(id);
            if (workspace == null)
            {
                return NotFound(new ErrorResponse { Message = $"工作空间 {id} 不存在" });
            }

            // 不允许添加Owner角色，Owner身份只能通过创建工作空间获得
            if (request.Role == WorkspaceRole.Owner)
            {
                return BadRequest(new ErrorResponse { Message = "不能添加所有者角色，所有者身份只能通过创建工作空间获得" });
            }

            // 检查用户是否已是成员
            var existingMember = await dbContext.WorkspaceMembers
                .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == request.UserId);

            if (existingMember != null)
            {
                return BadRequest(new ErrorResponse { Message = "该用户已是工作空间成员" });
            }

            var member = new WorkspaceMember
            {
                WorkspaceId = id,
                UserId = request.UserId,
                Role = request.Role,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.WorkspaceMembers.Add(member);
            await dbContext.SaveChangesAsync();

            var response = MapToMemberResponse(member);
            return CreatedAtAction(nameof(GetWorkspace), new { id }, response);
        }

        /// <summary>
        /// 更新工作空间成员角色
        /// </summary>
        [HttpPut("{id}/members/{userId}", Name = "更新工作空间成员角色")]
        [ProducesResponseType(typeof(WorkspaceMemberResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateMember(string id, string userId, [FromBody] UpdateMemberRequest request)
        {
            var currentUserId = GetUserId();

            var member = await dbContext.WorkspaceMembers
                .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == userId);

            if (member == null)
            {
                return NotFound(new ErrorResponse { Message = "成员不存在" });
            }

            // 不允许将任何成员改为Owner角色
            if (request.Role == WorkspaceRole.Owner)
            {
                return BadRequest(new ErrorResponse { Message = "不能将成员设置为所有者，所有者身份只能通过创建工作空间获得" });
            }

            // 不允许修改Owner的角色
            if (member.Role == WorkspaceRole.Owner)
            {
                return BadRequest(new ErrorResponse { Message = "不能修改所有者的角色" });
            }

            // 不允许修改自己的角色（防止所有者将自己降级）
            if (userId == currentUserId)
            {
                return BadRequest(new ErrorResponse { Message = "不能修改自己的角色" });
            }

            member.Role = request.Role;
            await dbContext.SaveChangesAsync();

            var response = MapToMemberResponse(member);
            return Ok(response);
        }

        /// <summary>
        /// 移除工作空间成员
        /// </summary>
        [HttpDelete("{id}/members/{userId}", Name = "移除工作空间成员")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RemoveMember(string id, string userId)
        {
            var member = await dbContext.WorkspaceMembers
                .FirstOrDefaultAsync(m => m.WorkspaceId == id && m.UserId == userId);

            if (member == null)
            {
                return NotFound(new ErrorResponse { Message = "成员不存在" });
            }

            // 不允许移除 Owner
            if (member.Role == WorkspaceRole.Owner)
            {
                return BadRequest(new ErrorResponse { Message = "不能移除工作空间所有者" });
            }

            dbContext.WorkspaceMembers.Remove(member);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 获取工作空间成员列表
        /// </summary>
        [HttpGet("{id}/members", Name = "获取工作空间成员列表")]
        [ProducesResponseType(typeof(List<WorkspaceMemberResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMembers(string id)
        {
            var members = await dbContext.WorkspaceMembers
                .Where(m => m.WorkspaceId == id)
                .OrderBy(m => m.Role)
                .ToListAsync();

            var responses = members.Select(MapToMemberResponse).ToList();
            return Ok(responses);
        }

        private static WorkspaceResponse MapToResponse(Workspace w)
        {
            return new WorkspaceResponse
            {
                Id = w.Id,
                Name = w.Name,
                Type = w.Type,
                OwnerUserId = w.OwnerUserId,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt
            };
        }

        private static WorkspaceDetailResponse MapToDetailResponse(Workspace w)
        {
            return new WorkspaceDetailResponse
            {
                Id = w.Id,
                Name = w.Name,
                Type = w.Type,
                OwnerUserId = w.OwnerUserId,
                CreatedAt = w.CreatedAt,
                UpdatedAt = w.UpdatedAt,
                KnowledgeBaseCount = w.KnowledgeBaseLinks?.Count ?? 0,
                MemberCount = w.Members?.Count ?? 0,
                KnowledgeBases = w.KnowledgeBaseLinks?.Select(link => new KnowledgeBaseRef
                {
                    Id = link.KnowledgeBaseId,
                    Name = link.KnowledgeBase.Name,
                    AliasName = link.AliasName,
                    SortOrder = link.SortOrder
                }).ToList(),
                Members = w.Members?.Select(m => new MemberRef
                {
                    UserId = m.UserId,
                    Role = m.Role,
                    JoinedAt = m.CreatedAt
                }).ToList()
            };
        }

        private static WorkspaceMemberResponse MapToMemberResponse(WorkspaceMember m)
        {
            return new WorkspaceMemberResponse
            {
                Id = m.Id,
                WorkspaceId = m.WorkspaceId,
                UserId = m.UserId,
                Role = m.Role,
                CreatedAt = m.CreatedAt
            };
        }
    }
}
