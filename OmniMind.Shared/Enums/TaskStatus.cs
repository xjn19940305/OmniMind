using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 通用任务状态
    /// </summary>
    public enum TaskStatus
    {
        /// <summary>
        /// 运行中
        /// </summary>
        Running = 1,

        /// <summary>
        /// 成功
        /// </summary>
        Success = 2,

        /// <summary>
        /// 失败
        /// </summary>
        Failed = 3
    }
}
