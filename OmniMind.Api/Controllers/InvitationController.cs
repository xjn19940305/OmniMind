using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Common;
using OmniMind.Contracts.KnowledgeBase;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;
using Microsoft.Extensions.Configuration;

namespace App.Controllers
{
    /// <summary>
    /// 知识库邀请模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class InvitationController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly ILogger<InvitationController> logger;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IConfiguration configuration;

        public InvitationController(
            OmniMindDbContext dbContext,
            ILogger<InvitationController> logger,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            this.dbContext = dbContext;
            this.logger = logger;
            this.httpContextAccessor = httpContextAccessor;
            this.configuration = configuration;
        }

        /// <summary>
        /// 获取前端URL
        /// </summary>
        private string GetFrontendUrl()
        {
            return configuration["FrontendUrl"] ?? httpContextAccessor.HttpContext?.Request.Scheme + "://" + httpContextAccessor.HttpContext?.Request.Host.Value;
        }

        /// <summary>
        /// 生成邀请码
        /// </summary>
        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 去除容易混淆的字符
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// 创建邀请
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request)
        {
            var currentUserId = GetUserId();

            // 验证知识库是否存在
            var knowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.Owner)
                .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = "知识库不存在" });
            }

            // 验证用户是否有权限邀请（必须是 Owner 或 Admin）
            var isOwner = knowledgeBase.OwnerUserId == currentUserId;
            var isAdmin = await dbContext.KnowledgeBaseMembers
                .AnyAsync(m => m.KnowledgeBaseId == request.KnowledgeBaseId
                    && m.UserId == currentUserId
                    && m.Role == KnowledgeBaseMemberRole.Admin);

            if (!isOwner && !isAdmin)
            {
                return StatusCode(403, new ErrorResponse { Message = "只有知识库拥有者或管理员可以发送邀请" });
            }

            // 生成邀请码（8位随机码）
            var code = GenerateInviteCode();
            var invitationId = Guid.CreateVersion7().ToString();
            var expiresAt = DateTimeOffset.UtcNow.AddDays(request.ExpireDays);

            var invitation = new KnowledgeBaseInvitation
            {
                Id = invitationId,
                KnowledgeBaseId = request.KnowledgeBaseId,
                Code = code,
                Email = request.Email?.Trim().ToLowerInvariant(),
                Role = request.Role,
                RequireApproval = request.RequireApproval,
                Status = InvitationStatus.Pending,
                InviterUserId = currentUserId,
                ExpiresAt = expiresAt
            };

            dbContext.KnowledgeBaseInvitations.Add(invitation);
            await dbContext.SaveChangesAsync();

            // 生成邀请链接
            var frontendUrl = GetFrontendUrl();
            var inviteLink = $"{frontendUrl}/invite/{code}";

            var response = new InvitationResponse
            {
                Id = invitation.Id,
                KnowledgeBaseId = invitation.KnowledgeBaseId,
                KnowledgeBaseName = knowledgeBase.Name,
                Code = invitation.Code,
                InviteLink = inviteLink,
                Email = invitation.Email,
                Role = invitation.Role,
                RequireApproval = invitation.RequireApproval,
                Status = invitation.Status,
                ExpiresAt = invitation.ExpiresAt,
                CreatedAt = invitation.CreatedAt,
                ApplicationReason = invitation.ApplicationReason
            };

            return Created(string.Empty, response);
        }

        /// <summary>
        /// 获取知识库邀请列表
        /// </summary>
        [HttpGet("knowledge-base/{knowledgeBaseId}")]
        [ProducesResponseType(typeof(PagedResponse<InvitationResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInvitations(
            string knowledgeBaseId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] InvitationStatus? status = null)
        {
            var currentUserId = GetUserId();

            // 验证权限
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(knowledgeBaseId);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = "知识库不存在" });
            }

            var isOwner = knowledgeBase.OwnerUserId == currentUserId;
            var member = await dbContext.KnowledgeBaseMembers
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == knowledgeBaseId && m.UserId == currentUserId);

            if (!isOwner && (member == null || member.Role < KnowledgeBaseMemberRole.Editor))
            {
                return StatusCode(403, new ErrorResponse { Message = "没有权限查看邀请" });
            }

            var query = dbContext.KnowledgeBaseInvitations
                .Include(inv => inv.KnowledgeBase)
                .Include(inv => inv.InviteeUser)
                .Where(inv => inv.KnowledgeBaseId == knowledgeBaseId);

            if (status.HasValue)
            {
                query = query.Where(inv => inv.Status == status.Value);
            }

            var totalCount = await query.CountAsync();

            var invitations = await query
                .OrderByDescending(inv => inv.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 生成邀请链接
            var frontendUrl = GetFrontendUrl();

            var items = invitations.Select(inv => new InvitationResponse
            {
                Id = inv.Id,
                KnowledgeBaseId = inv.KnowledgeBaseId,
                KnowledgeBaseName = inv.KnowledgeBase?.Name,
                Code = inv.Code,
                InviteLink = $"{frontendUrl}/invite/{inv.Code}",
                Email = inv.Email,
                Role = inv.Role,
                RequireApproval = inv.RequireApproval,
                Status = inv.Status,
                ExpiresAt = inv.ExpiresAt,
                CreatedAt = inv.CreatedAt,
                ApplicationReason = inv.ApplicationReason,
                InviteeUserId = inv.InviteeUserId,
                InviteeUser = inv.InviteeUserId != null ? new InviteeUserInfo
                {
                    Id = inv.InviteeUser!.Id,
                    UserName = inv.InviteeUser.UserName,
                    NickName = inv.InviteeUser.NickName,
                    Email = inv.InviteeUser.Email
                } : null
            }).ToList();

            return Ok(new PagedResponse<InvitationResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        /// <summary>
        /// 获取邀请详情（通过邀请码）
        /// </summary>
        [HttpGet("code/{code}")]
        [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetInvitation(string code)
        {
            var invitation = await dbContext.KnowledgeBaseInvitations
                .Include(inv => inv.KnowledgeBase)
                .Include(inv => inv.InviterUser)
                .FirstOrDefaultAsync(inv => inv.Code == code);

            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在或已过期" });
            }

            // 检查是否过期
            if (DateTimeOffset.UtcNow > invitation.ExpiresAt)
            {
                // 更新状态为已过期
                invitation.Status = InvitationStatus.Expired;
                await dbContext.SaveChangesAsync();
                return NotFound(new ErrorResponse { Message = "邀请已过期" });
            }

            // 检查用户是否已登录
            var currentUserId = GetUserId();

            // 生成邀请链接
            var frontendUrl = GetFrontendUrl();

            var response = new InvitationResponse
            {
                Id = invitation.Id,
                KnowledgeBaseId = invitation.KnowledgeBaseId,
                KnowledgeBaseName = invitation.KnowledgeBase?.Name,
                Code = invitation.Code,
                InviteLink = $"{frontendUrl}/invite/{invitation.Code}",
                Email = invitation.Email,
                Role = invitation.Role,
                RequireApproval = invitation.RequireApproval,
                Status = invitation.Status,
                ExpiresAt = invitation.ExpiresAt,
                CreatedAt = invitation.CreatedAt,
                ApplicationReason = invitation.ApplicationReason
            };

            // 添加邀请者信息
            var inviterName = invitation.InviterUser?.NickName ?? invitation.InviterUser?.UserName;
            var isCurrentUserInvited = currentUserId != null && invitation.InviteeUserId == currentUserId;

            return Ok(new { invitation = response, inviterName, isCurrentUserInvited });
        }

        /// <summary>
        /// 接受/拒绝邀请
        /// </summary>
        [HttpPost("respond")]
        [ProducesResponseType(typeof(KnowledgeBaseMemberResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> RespondToInvitation([FromBody] RespondInvitationRequest request)
        {
            var currentUserId = GetUserId();
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(new ErrorResponse { Message = "请先登录" });
            }

            var invitation = await dbContext.KnowledgeBaseInvitations
                .Include(inv => inv.KnowledgeBase)
                .FirstOrDefaultAsync(inv => inv.Code == request.Code);

            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在" });
            }

            // 检查是否过期
            if (DateTimeOffset.UtcNow > invitation.ExpiresAt)
            {
                invitation.Status = InvitationStatus.Expired;
                await dbContext.SaveChangesAsync();
                return BadRequest(new ErrorResponse { Message = "邀请已过期" });
            }

            // 检查邀请状态
            if (invitation.Status != InvitationStatus.Pending)
            {
                return BadRequest(new ErrorResponse { Message = $"邀请已{GetStatusText(invitation.Status)}" });
            }

            // 检查邮箱是否匹配（如果指定了邮箱）
            if (!string.IsNullOrEmpty(invitation.Email))
            {
                var user = await dbContext.Users.FindAsync(currentUserId);
                if (user == null || user.Email?.ToLowerInvariant() != invitation.Email)
                {
                    return StatusCode(403, new ErrorResponse { Message = "此邀请不是发送给您的" });
                }
            }

            // 拒绝邀请
            if (!request.Accept)
            {
                invitation.Status = InvitationStatus.Rejected;
                await dbContext.SaveChangesAsync();
                return Ok(new { message = "已拒绝邀请" });
            }

            // 接受邀请
            // 如果需要审核，标记为待审核（保持 Pending 状态，但记录用户ID）
            if (invitation.RequireApproval)
            {
                invitation.InviteeUserId = currentUserId;
                invitation.ApplicationReason = request.ApplicationReason;
                // 状态保持为 Pending，等待管理员审核
                await dbContext.SaveChangesAsync();

                return Ok(new { message = "已接受邀请，等待管理员审核", requiresApproval = true });
            }

            // 不需要审核，直接加入
            invitation.InviteeUserId = currentUserId;
            invitation.Status = InvitationStatus.Accepted;
            invitation.AcceptedAt = DateTimeOffset.UtcNow;

            // 创建成员记录
            var member = new KnowledgeBaseMember
            {
                KnowledgeBaseId = invitation.KnowledgeBaseId,
                UserId = currentUserId,
                Role = invitation.Role,
                InvitedByUserId = invitation.InviterUserId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.KnowledgeBaseMembers.Add(member);
            await dbContext.SaveChangesAsync();

            var memberResponse = new KnowledgeBaseMemberResponse
            {
                Id = member.Id,
                KnowledgeBaseId = member.KnowledgeBaseId,
                UserId = member.UserId,
                Role = member.Role,
                CreatedAt = member.CreatedAt
            };

            return Created(string.Empty, memberResponse);
        }

        /// <summary>
        /// 审核邀请（管理员/拥有者）
        /// </summary>
        [HttpPost("{id}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ApprovalInvitation(string id, [FromBody] ApprovalInvitationRequest request)
        {
            var currentUserId = GetUserId();
            var invitation = await dbContext.KnowledgeBaseInvitations
                .Include(inv => inv.KnowledgeBase)
                .FirstOrDefaultAsync(inv => inv.Id == id);

            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在" });
            }

            // 验证权限（只有知识库 Owner 或 Admin 可以审核）
            var isOwner = invitation.KnowledgeBase.OwnerUserId == currentUserId;
            var isAdmin = await dbContext.KnowledgeBaseMembers
                .AnyAsync(m => m.KnowledgeBaseId == invitation.KnowledgeBaseId
                    && m.UserId == currentUserId
                    && m.Role == KnowledgeBaseMemberRole.Admin);

            if (!isOwner && !isAdmin)
            {
                return StatusCode(403, new ErrorResponse { Message = "只有知识库拥有者或管理员可以审核邀请" });
            }

            // 检查邀请状态 - 应该是 Pending（用户已接受，等待审核）
            if (invitation.Status != InvitationStatus.Pending)
            {
                return BadRequest(new ErrorResponse { Message = $"邀请状态不正确，当前状态：{GetStatusText(invitation.Status)}" });
            }

            // 被邀请用户ID
            var inviteeUserId = invitation.InviteeUserId;
            if (string.IsNullOrEmpty(inviteeUserId))
            {
                return BadRequest(new ErrorResponse { Message = "邀请尚未被用户接受" });
            }

            if (request.Approved)
            {
                // 审核通过，更新邀请状态并创建成员记录
                invitation.Status = InvitationStatus.Accepted;
                invitation.AcceptedAt = DateTimeOffset.UtcNow;

                var member = new KnowledgeBaseMember
                {
                    KnowledgeBaseId = invitation.KnowledgeBaseId,
                    UserId = inviteeUserId,
                    Role = invitation.Role,
                    InvitedByUserId = invitation.InviterUserId,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                dbContext.KnowledgeBaseMembers.Add(member);
            }
            else
            {
                // 审核拒绝，更新邀请状态
                invitation.Status = InvitationStatus.Rejected;
            }

            await dbContext.SaveChangesAsync();

            return Ok(new { message = request.Approved ? "已通过邀请" : "已拒绝邀请" });
        }

        /// <summary>
        /// 取消邀请
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelInvitation(string id)
        {
            var invitation = await dbContext.KnowledgeBaseInvitations.FindAsync(id);
            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在" });
            }

            var currentUserId = GetUserId();

            // 验证权限（只有邀请者或知识库 Owner 可以取消）
            var isOwner = await dbContext.KnowledgeBases
                .AnyAsync(kb => kb.Id == invitation.KnowledgeBaseId && kb.OwnerUserId == currentUserId);
            var isInviter = invitation.InviterUserId == currentUserId;

            if (!isOwner && !isInviter)
            {
                return StatusCode(403, new ErrorResponse { Message = "只有邀请者或知识库拥有者可以取消邀请" });
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return BadRequest(new ErrorResponse { Message = "只能取消待处理的邀请" });
            }

            invitation.Status = InvitationStatus.Canceled;
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        private static string GetStatusText(InvitationStatus status)
        {
            return status switch
            {
                InvitationStatus.Pending => "待处理",
                InvitationStatus.Accepted => "已接受",
                InvitationStatus.Rejected => "已拒绝",
                InvitationStatus.Expired => "已过期",
                InvitationStatus.Canceled => "已取消",
                _ => "未知"
            };
        }
    }
}
