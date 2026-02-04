using Microsoft.EntityFrameworkCore;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;

namespace OmniMind.Api.Extensions
{
    /// <summary>
    /// 知识库权限检查扩展
    /// </summary>
    public static class KnowledgeBaseAuthorizationExtensions
    {
        /// <summary>
        /// 权限检查结果
        /// </summary>
        public class AuthorizationResult
        {
            public bool HasAccess { get; set; }
            public string? Message { get; set; }
        }

        /// <summary>
        /// 检查用户是否有权访问知识库
        /// </summary>
        /// <param name="dbContext">数据库上下文</param>
        /// <param name="knowledgeBaseId">知识库ID</param>
        /// <param name="currentUserId">当前用户ID</param>
        /// <returns>权限检查结果</returns>
        public static async Task<AuthorizationResult> CheckKnowledgeBaseAccessAsync(
            this OmniMindDbContext dbContext,
            string knowledgeBaseId,
            string currentUserId)
        {
            var knowledgeBase = await dbContext.KnowledgeBases.FindAsync(knowledgeBaseId);
            if (knowledgeBase == null)
            {
                return new AuthorizationResult
                {
                    HasAccess = false,
                    Message = "知识库不存在"
                };
            }

            return await CheckKnowledgeBaseAccessAsync(dbContext, knowledgeBase, currentUserId);
        }

        /// <summary>
        /// 检查用户是否有权访问知识库
        /// </summary>
        /// <param name="dbContext">数据库上下文</param>
        /// <param name="knowledgeBase">知识库实体</param>
        /// <param name="currentUserId">当前用户ID</param>
        /// <returns>权限检查结果</returns>
        public static async Task<AuthorizationResult> CheckKnowledgeBaseAccessAsync(
            this OmniMindDbContext dbContext,
            KnowledgeBase knowledgeBase,
            string currentUserId)
        {
            var isOwner = knowledgeBase.OwnerUserId == currentUserId;

            // 拥有者始终有权限
            if (isOwner)
            {
                return new AuthorizationResult { HasAccess = true };
            }

            var isPrivate = knowledgeBase.Visibility == Visibility.Private;
            var isInternal = knowledgeBase.Visibility == Visibility.Internal;
            var isPublic = knowledgeBase.Visibility == Visibility.Public;

            // 私有和内部知识库，只有拥有者能访问
            if (isPrivate || isInternal)
            {
                return new AuthorizationResult
                {
                    HasAccess = false,
                    Message = "知识库当前为私密状态，只有拥有者可以访问"
                };
            }

            // 公开知识库，成员可访问
            if (isPublic)
            {
                var isMember = await dbContext.KnowledgeBaseMembers
                    .AnyAsync(m => m.KnowledgeBaseId == knowledgeBase.Id && m.UserId == currentUserId);

                if (isMember)
                {
                    return new AuthorizationResult { HasAccess = true };
                }

                return new AuthorizationResult
                {
                    HasAccess = false,
                    Message = "只有知识库成员可以访问"
                };
            }

            return new AuthorizationResult
            {
                HasAccess = false,
                Message = "无权访问此知识库"
            };
        }
    }
}
