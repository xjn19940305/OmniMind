using OmniMind.Enums;

namespace OmniMind.Contracts.Workspace
{
    /// <summary>
    /// 添加成员请求
    /// </summary>
    public record AddMemberRequest
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 角色
        /// </summary>
        public WorkspaceRole Role { get; init; } = WorkspaceRole.Member;
    }

    /// <summary>
    /// 更新成员请求
    /// </summary>
    public record UpdateMemberRequest
    {
        /// <summary>
        /// 角色
        /// </summary>
        public WorkspaceRole Role { get; init; }
    }
}
