using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 工作空间类型：用于区分个人空间、团队空间、共享空间等。
    /// </summary>
    public enum WorkspaceType
    {
        /// <summary>
        /// 个人空间
        /// </summary>
        Personal = 1,

        /// <summary>
        /// 团队空间
        /// </summary>
        Team = 2,

        /// <summary>
        /// 共享空间
        /// </summary>
        Shared = 3
    }
}
