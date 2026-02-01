using App.Swaggers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.KnowledgeBase;
using OmniMind.Entities;
using OmniMind.Persistence.MySql;

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

            var tenantId = GetTenantId();
            var currentUserId = GetUserId();

            // 获取用户的第一个工作空间
            var firstWorkspace = await dbContext.WorkspaceMembers
                .Where(m => m.UserId == currentUserId)
                .Join(dbContext.Workspaces, m => m.WorkspaceId, w => w.Id, (m, w) => w)
                .FirstOrDefaultAsync();

            if (firstWorkspace == null)
            {
                return BadRequest(new ErrorResponse { Message = "您还没有工作空间，请先创建工作空间" });
            }

            var knowledgeBase = new KnowledgeBase
            {
                TenantId = tenantId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Visibility = request.Visibility,
                IndexProfileId = request.IndexProfileId,
                CreatedAt = DateTimeOffset.UtcNow
            };



            // 自动挂载到用户的第一个工作空间
            var link = new KnowledgeBaseWorkspace
            {
                TenantId = tenantId,
                KnowledgeBaseId = knowledgeBase.Id,
                WorkspaceId = firstWorkspace.Id,
                SortOrder = 0,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            dbContext.KnowledgeBases.Add(knowledgeBase);
            dbContext.KnowledgeBaseWorkspaces.Add(link);
            await dbContext.SaveChangesAsync();

            // 重新查询以加载导航属性
            var savedKnowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.WorkspaceLinks)
                    .ThenInclude(l => l.Workspace)
                .FirstAsync(kb => kb.Id == knowledgeBase.Id);

            var response = MapToResponse(savedKnowledgeBase);
            return CreatedAtAction(nameof(GetKnowledgeBase), new { id = knowledgeBase.Id }, response);
        }

        /// <summary>
        /// 获取知识库详情
        /// </summary>
        [HttpGet("{id}", Name = "获取知识库详情")]
        [ProducesResponseType(typeof(KnowledgeBaseResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetKnowledgeBase(string id)
        {
            var knowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.WorkspaceLinks)
                    .ThenInclude(link => link.Workspace)
                .FirstOrDefaultAsync(kb => kb.Id == id);

            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var response = MapToResponse(knowledgeBase);
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
            // 获取当前用户ID
            var userIdClaim = User.FindFirst("sub");
            if (userIdClaim == null)
            {
                return Unauthorized(new ErrorResponse { Message = "无法获取用户信息" });
            }
            var currentUserId = userIdClaim.Value;

            // 获取当前用户所属的工作空间ID列表
            var userWorkspaceIds = await dbContext.WorkspaceMembers
                .Where(m => m.UserId == currentUserId)
                .Select(m => m.WorkspaceId)
                .ToListAsync();

            // 查询用户工作空间关联的知识库
            var query = from kb in dbContext.KnowledgeBases.Include(x => x.WorkspaceLinks).ThenInclude(x => x.Workspace)
                        join link in dbContext.KnowledgeBaseWorkspaces on kb.Id equals link.KnowledgeBaseId
                        where userWorkspaceIds.Contains(link.WorkspaceId)
                        select kb;

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
                .Distinct()
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
        /// 挂载知识库到工作空间
        /// </summary>
        [HttpPost("{id}/workspaces", Name = "挂载知识库到工作空间")]
        [ProducesResponseType(typeof(KnowledgeBaseWorkspaceResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MountToWorkspace(string id, [FromBody] MountKnowledgeBaseRequest request)
        {
            var tenantId = GetTenantId();

            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(id);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = $"知识库 {id} 不存在" });
            }

            var workspace = await dbContext.Workspaces.FindAsync(request.WorkspaceId);
            if (workspace == null)
            {
                return NotFound(new ErrorResponse { Message = $"工作空间 {request.WorkspaceId} 不存在" });
            }

            // 检查是否已经挂载
            var existingLink = await dbContext.KnowledgeBaseWorkspaces
                .FirstOrDefaultAsync(link => link.KnowledgeBaseId == id && link.WorkspaceId == request.WorkspaceId);

            if (existingLink != null)
            {
                return BadRequest(new ErrorResponse { Message = "该知识库已挂载到此工作空间" });
            }

            var link = new KnowledgeBaseWorkspace
            {
                TenantId = tenantId,
                KnowledgeBaseId = id,
                WorkspaceId = request.WorkspaceId,
                AliasName = request.AliasName?.Trim(),
                SortOrder = request.SortOrder ?? 0,
                CreatedAt = DateTimeOffset.UtcNow,

            };

            dbContext.KnowledgeBaseWorkspaces.Add(link);
            await dbContext.SaveChangesAsync();

            var response = new KnowledgeBaseWorkspaceResponse
            {
                Id = link.Id,
                KnowledgeBaseId = link.KnowledgeBaseId,
                KnowledgeBaseName = knowledgeBase.Name,
                WorkspaceId = link.WorkspaceId,
                WorkspaceName = workspace.Name,
                AliasName = link.AliasName,
                SortOrder = link.SortOrder,
                CreatedAt = link.CreatedAt
            };

            return CreatedAtAction(nameof(GetKnowledgeBase), new { id }, response);
        }

        /// <summary>
        /// 从工作空间卸载知识库
        /// </summary>
        [HttpDelete("{id}/workspaces/{workspaceId}", Name = "从工作空间卸载知识库")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnmountFromWorkspace(string id, string workspaceId)
        {
            var link = await dbContext.KnowledgeBaseWorkspaces
                .FirstOrDefaultAsync(l => l.KnowledgeBaseId == id && l.WorkspaceId == workspaceId);
              //if(await dbContext.KnowledgeBaseWorkspaces.Where(x=>x.))
            if (link == null)
            {
                return NotFound(new ErrorResponse { Message = "该知识库未挂载到此工作空间" });
            }

            dbContext.KnowledgeBaseWorkspaces.Remove(link);
            await dbContext.SaveChangesAsync();

            return NoContent();
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
                WorkspaceCount = kb.WorkspaceLinks?.Count ?? 0,
                Workspaces = kb.WorkspaceLinks?.Select(link => new WorkspaceRef
                {
                    Id = link.WorkspaceId,
                    Name = link.Workspace.Name,
                    AliasName = link.AliasName,
                    SortOrder = link.SortOrder
                }).ToList()
            };
        }
    }
}
