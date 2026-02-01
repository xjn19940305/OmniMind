using OmniMind.Enums;

namespace OmniMind.Contracts.Workspace
{
    /// <summary>
    /// 创建工作空间请求
    /// </summary>
    public record CreateWorkspaceRequest
    {
        /// <summary>
        /// 工作空间名称
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 工作空间类型
        /// </summary>
        public WorkspaceType Type { get; init; } = WorkspaceType.Team;
    }
}
