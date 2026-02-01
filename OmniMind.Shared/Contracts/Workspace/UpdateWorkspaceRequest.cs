using OmniMind.Enums;

namespace OmniMind.Contracts.Workspace
{
    /// <summary>
    /// 更新工作空间请求
    /// </summary>
    public record UpdateWorkspaceRequest
    {
        /// <summary>
        /// 工作空间名称
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// 工作空间类型
        /// </summary>
        public WorkspaceType? Type { get; init; }
    }
}
