using Microsoft.EntityFrameworkCore;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;

namespace OmniMind.Api.Extensions
{
    public enum KnowledgeBasePermission
    {
        View,
        Edit,
        ManageMembers,
        ManageInvitations,
        Delete
    }

    public static class KnowledgeBaseAuthorizationExtensions
    {
        public sealed class AuthorizationResult
        {
            public bool HasAccess { get; init; }
            public string? Message { get; init; }
            public bool IsOwner { get; init; }
            public KnowledgeBaseMemberRole? MemberRole { get; init; }
        }

        public static Task<AuthorizationResult> CheckKnowledgeBaseAccessAsync(
            this OmniMindDbContext dbContext,
            string knowledgeBaseId,
            string currentUserId)
        {
            return dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBaseId, currentUserId, KnowledgeBasePermission.View);
        }

        public static Task<AuthorizationResult> CheckKnowledgeBaseAccessAsync(
            this OmniMindDbContext dbContext,
            KnowledgeBase knowledgeBase,
            string currentUserId)
        {
            return dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, currentUserId, KnowledgeBasePermission.View);
        }

        public static async Task<AuthorizationResult> AuthorizeKnowledgeBaseAsync(
            this OmniMindDbContext dbContext,
            string knowledgeBaseId,
            string currentUserId,
            KnowledgeBasePermission permission)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FirstOrDefaultAsync(kb => kb.Id == knowledgeBaseId);
            if (knowledgeBase == null)
            {
                return Denied("知识库不存在");
            }

            return await dbContext.AuthorizeKnowledgeBaseAsync(knowledgeBase, currentUserId, permission);
        }

        public static async Task<AuthorizationResult> AuthorizeKnowledgeBaseAsync(
            this OmniMindDbContext dbContext,
            KnowledgeBase knowledgeBase,
            string currentUserId,
            KnowledgeBasePermission permission)
        {
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Denied("未登录用户不能访问知识库");
            }

            var isOwner = knowledgeBase.OwnerUserId == currentUserId;
            if (isOwner)
            {
                return Allowed(isOwner: true);
            }

            var member = await dbContext.KnowledgeBaseMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.KnowledgeBaseId == knowledgeBase.Id && m.UserId == currentUserId);

            return permission switch
            {
                KnowledgeBasePermission.View => AuthorizeView(knowledgeBase, member),
                KnowledgeBasePermission.Edit => AuthorizeEdit(member),
                KnowledgeBasePermission.ManageMembers => AuthorizeManageMembers(member),
                KnowledgeBasePermission.ManageInvitations => AuthorizeManageInvitations(member),
                KnowledgeBasePermission.Delete => AuthorizeDelete(member),
                _ => Denied("无权访问此知识库")
            };
        }

        private static AuthorizationResult AuthorizeView(KnowledgeBase knowledgeBase, KnowledgeBaseMember? member)
        {
            if (member != null)
            {
                return Allowed(member.Role);
            }

            return knowledgeBase.Visibility switch
            {
                Visibility.Public => Allowed(),
                Visibility.Internal => Denied("只有知识库成员可以访问此知识库"),
                Visibility.Private => Denied("只有知识库拥有者可以访问此知识库"),
                _ => Denied("无权访问此知识库")
            };
        }

        private static AuthorizationResult AuthorizeEdit(KnowledgeBaseMember? member)
        {
            if (member?.Role is KnowledgeBaseMemberRole.Admin or KnowledgeBaseMemberRole.Editor)
            {
                return Allowed(member.Role);
            }

            return Denied("只有拥有者、管理员或编辑可以修改知识库内容");
        }

        private static AuthorizationResult AuthorizeManageMembers(KnowledgeBaseMember? member)
        {
            if (member?.Role == KnowledgeBaseMemberRole.Admin)
            {
                return Allowed(member.Role);
            }

            return Denied("只有拥有者或管理员可以管理成员");
        }

        private static AuthorizationResult AuthorizeManageInvitations(KnowledgeBaseMember? member)
        {
            if (member?.Role == KnowledgeBaseMemberRole.Admin)
            {
                return Allowed(member.Role);
            }

            return Denied("只有拥有者或管理员可以管理邀请");
        }

        private static AuthorizationResult AuthorizeDelete(KnowledgeBaseMember? member)
        {
            if (member?.Role == KnowledgeBaseMemberRole.Admin)
            {
                return Allowed(member.Role);
            }

            return Denied("只有拥有者或管理员可以删除知识库");
        }

        private static AuthorizationResult Allowed(KnowledgeBaseMemberRole? role = null, bool isOwner = false)
        {
            return new AuthorizationResult
            {
                HasAccess = true,
                IsOwner = isOwner,
                MemberRole = role
            };
        }

        private static AuthorizationResult Denied(string message)
        {
            return new AuthorizationResult
            {
                HasAccess = false,
                Message = message
            };
        }
    }
}
