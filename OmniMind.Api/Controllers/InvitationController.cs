using Microsoft.AspNetCore.Authorization;
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
    public class InvitationController : BaseController
    {
        private readonly OmniMindDbContext dbContext;
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly IConfiguration configuration;

        public InvitationController(
            OmniMindDbContext dbContext,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            this.dbContext = dbContext;
            this.httpContextAccessor = httpContextAccessor;
            this.configuration = configuration;
        }

        [HttpPost]
        [ProducesResponseType(typeof(InvitationResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> CreateInvitation([FromBody] CreateInvitationRequest request)
        {
            var knowledgeBase = await dbContext.KnowledgeBases
                .Include(kb => kb.Owner)
                .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);
            if (knowledgeBase == null)
            {
                return NotFound(new ErrorResponse { Message = "知识库不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, GetUserId(), KnowledgeBasePermission.ManageInvitations);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            var code = GenerateInviteCode();
            while (await dbContext.KnowledgeBaseInvitations.AnyAsync(i => i.Code == code))
            {
                code = GenerateInviteCode();
            }

            var invitation = new KnowledgeBaseInvitation
            {
                Id = Guid.CreateVersion7().ToString(),
                KnowledgeBaseId = request.KnowledgeBaseId,
                Code = code,
                Email = request.Email?.Trim().ToLowerInvariant(),
                Role = request.Role,
                RequireApproval = request.RequireApproval,
                Status = InvitationStatus.Pending,
                InviterUserId = GetUserId(),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(request.ExpireDays)
            };

            dbContext.KnowledgeBaseInvitations.Add(invitation);
            await dbContext.SaveChangesAsync();

            return Created(string.Empty, ToInvitationResponse(invitation, knowledgeBase.Name));
        }

        [HttpGet("knowledge-base/{knowledgeBaseId}")]
        [ProducesResponseType(typeof(PagedResponse<InvitationResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetInvitations(
            string knowledgeBaseId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] InvitationStatus? status = null)
        {
            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBaseId, GetUserId(), KnowledgeBasePermission.ManageInvitations);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
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

            return Ok(new PagedResponse<InvitationResponse>
            {
                Items = invitations.Select(inv => ToInvitationResponse(inv, inv.KnowledgeBase?.Name)).ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            });
        }

        [AllowAnonymous]
        [HttpGet("code/{code}")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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

            if (DateTimeOffset.UtcNow > invitation.ExpiresAt)
            {
                if (invitation.Status != InvitationStatus.Expired)
                {
                    invitation.Status = InvitationStatus.Expired;
                    invitation.UpdatedAt = DateTimeOffset.UtcNow;
                    await dbContext.SaveChangesAsync();
                }

                return NotFound(new ErrorResponse { Message = "邀请已过期" });
            }

            var currentUserId = TryGetUserId();
            return Ok(new
            {
                invitation = ToInvitationResponse(invitation, invitation.KnowledgeBase?.Name),
                inviterName = invitation.InviterUser?.NickName ?? invitation.InviterUser?.UserName,
                isCurrentUserInvited = !string.IsNullOrWhiteSpace(currentUserId) && invitation.InviteeUserId == currentUserId
            });
        }

        [HttpPost("respond")]
        [ProducesResponseType(typeof(KnowledgeBaseMemberResponse), StatusCodes.Status201Created)]
        public async Task<IActionResult> RespondToInvitation([FromBody] RespondInvitationRequest request)
        {
            var currentUserId = GetUserId();
            var invitation = await dbContext.KnowledgeBaseInvitations
                .Include(inv => inv.KnowledgeBase)
                .FirstOrDefaultAsync(inv => inv.Code == request.Code);
            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在" });
            }

            if (DateTimeOffset.UtcNow > invitation.ExpiresAt)
            {
                invitation.Status = InvitationStatus.Expired;
                invitation.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync();
                return BadRequest(new ErrorResponse { Message = "邀请已过期" });
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return BadRequest(new ErrorResponse { Message = $"邀请已{GetStatusText(invitation.Status)}" });
            }

            if (!string.IsNullOrEmpty(invitation.Email))
            {
                var user = await dbContext.Users.FindAsync(currentUserId);
                if (user == null || !string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "此邀请并不是发给当前用户的" });
                }
            }

            if (!request.Accept)
            {
                invitation.Status = InvitationStatus.Rejected;
                invitation.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync();
                return Ok(new { message = "已拒绝邀请" });
            }

            var existingMember = await dbContext.KnowledgeBaseMembers
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == invitation.KnowledgeBaseId && m.UserId == currentUserId);

            invitation.InviteeUserId = currentUserId;
            invitation.ApplicationReason = request.ApplicationReason;
            invitation.UpdatedAt = DateTimeOffset.UtcNow;

            if (existingMember != null)
            {
                invitation.Status = InvitationStatus.Accepted;
                invitation.AcceptedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync();
                return Ok(MapToMemberResponse(existingMember));
            }

            if (invitation.RequireApproval)
            {
                await dbContext.SaveChangesAsync();
                return Ok(new { message = "已提交申请，等待管理员审核", requiresApproval = true });
            }

            invitation.Status = InvitationStatus.Accepted;
            invitation.AcceptedAt = DateTimeOffset.UtcNow;

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
            return Created(string.Empty, MapToMemberResponse(member));
        }

        [HttpPost("{id}/approve")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ApprovalInvitation(string id, [FromBody] ApprovalInvitationRequest request)
        {
            var invitation = await dbContext.KnowledgeBaseInvitations
                .Include(inv => inv.KnowledgeBase)
                .FirstOrDefaultAsync(inv => inv.Id == id);
            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(invitation.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.ManageInvitations);
            if (!auth.HasAccess)
            {
                return Forbid(auth.Message);
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return BadRequest(new ErrorResponse { Message = $"邀请状态不正确，当前为{GetStatusText(invitation.Status)}" });
            }

            if (string.IsNullOrWhiteSpace(invitation.InviteeUserId))
            {
                return BadRequest(new ErrorResponse { Message = "邀请尚未被用户接受" });
            }

            if (request.Approved)
            {
                invitation.Status = InvitationStatus.Accepted;
                invitation.AcceptedAt = DateTimeOffset.UtcNow;
                invitation.UpdatedAt = DateTimeOffset.UtcNow;

                var existingMember = await dbContext.KnowledgeBaseMembers
                    .FirstOrDefaultAsync(m => m.KnowledgeBaseId == invitation.KnowledgeBaseId && m.UserId == invitation.InviteeUserId);
                if (existingMember == null)
                {
                    dbContext.KnowledgeBaseMembers.Add(new KnowledgeBaseMember
                    {
                        KnowledgeBaseId = invitation.KnowledgeBaseId,
                        UserId = invitation.InviteeUserId,
                        Role = invitation.Role,
                        InvitedByUserId = invitation.InviterUserId,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
            }
            else
            {
                invitation.Status = InvitationStatus.Rejected;
                invitation.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await dbContext.SaveChangesAsync();
            return Ok(new { message = request.Approved ? "已通过邀请" : "已拒绝邀请" });
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> CancelInvitation(string id)
        {
            var invitation = await dbContext.KnowledgeBaseInvitations.FindAsync(id);
            if (invitation == null)
            {
                return NotFound(new ErrorResponse { Message = "邀请不存在" });
            }

            var auth = await dbContext.AuthorizeKnowledgeBaseAsync(invitation.KnowledgeBaseId, GetUserId(), KnowledgeBasePermission.ManageInvitations);
            if (!auth.HasAccess && invitation.InviterUserId != GetUserId())
            {
                return Forbid(auth.Message);
            }

            if (invitation.Status != InvitationStatus.Pending)
            {
                return BadRequest(new ErrorResponse { Message = "只能取消待处理的邀请" });
            }

            invitation.Status = InvitationStatus.Canceled;
            invitation.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync();
            return NoContent();
        }

        private string GetFrontendUrl()
        {
            return configuration["FrontendUrl"]
                ?? $"{httpContextAccessor.HttpContext?.Request.Scheme}://{httpContextAccessor.HttpContext?.Request.Host.Value}";
        }

        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private InvitationResponse ToInvitationResponse(KnowledgeBaseInvitation invitation, string? knowledgeBaseName)
        {
            var frontendUrl = GetFrontendUrl();
            return new InvitationResponse
            {
                Id = invitation.Id,
                KnowledgeBaseId = invitation.KnowledgeBaseId,
                KnowledgeBaseName = knowledgeBaseName,
                Code = invitation.Code,
                InviteLink = $"{frontendUrl}/invite/{invitation.Code}",
                Email = invitation.Email,
                Role = invitation.Role,
                RequireApproval = invitation.RequireApproval,
                Status = invitation.Status,
                ExpiresAt = invitation.ExpiresAt,
                CreatedAt = invitation.CreatedAt,
                ApplicationReason = invitation.ApplicationReason,
                InviteeUserId = invitation.InviteeUserId,
                InviteeUser = invitation.InviteeUserId == null || invitation.InviteeUser == null
                    ? null
                    : new InviteeUserInfo
                    {
                        Id = invitation.InviteeUser.Id,
                        UserName = invitation.InviteeUser.UserName,
                        NickName = invitation.InviteeUser.NickName,
                        Email = invitation.InviteeUser.Email
                    }
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

        private static string GetStatusText(InvitationStatus status)
        {
            return status switch
            {
                InvitationStatus.Pending => "待处理",
                InvitationStatus.Accepted => "已接受",
                InvitationStatus.Rejected => "已拒绝",
                InvitationStatus.Expired => "已过期",
                InvitationStatus.Canceled => "已取消",
                _ => "未知状态"
            };
        }

        private IActionResult Forbid(string? message)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = message ?? "无权访问此资源" });
        }
    }
}
