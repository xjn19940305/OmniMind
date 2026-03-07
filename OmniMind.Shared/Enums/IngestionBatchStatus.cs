using System;

namespace OmniMind.Enums
{
    /// <summary>
    /// 导入批次状态。
    /// </summary>
    public enum IngestionBatchStatus
    {
        /// <summary>
        /// 运行中
        /// </summary>
        Running = 1,

        /// <summary>
        /// 全部成功
        /// </summary>
        Success = 2,

        /// <summary>
        /// 部分成功
        /// </summary>
        PartialSuccess = 3,

        /// <summary>
        /// 全部失败
        /// </summary>
        Failed = 4,

        /// <summary>
        /// 已取消
        /// </summary>
        Canceled = 5
    }
}
