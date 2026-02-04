namespace OmniMind.Enums
{
    /// <summary>
    /// 邀请状态
    /// </summary>
    public enum InvitationStatus
    {
        /// <summary>
        /// 待处理（等待接受或审核）
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 已接受（直接加入或审核通过）
        /// </summary>
        Accepted = 1,

        /// <summary>
        /// 已拒绝
        /// </summary>
        Rejected = 2,

        /// <summary>
        /// 已过期
        /// </summary>
        Expired = 3,

        /// <summary>
        /// 已取消
        /// </summary>
        Canceled = 4
    }
}
