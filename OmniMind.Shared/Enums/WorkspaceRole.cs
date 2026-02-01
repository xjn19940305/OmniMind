using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 工作空间成员角色
    /// </summary>
    public enum WorkspaceRole
    {
        /// <summary>
        /// 所有者
        /// </summary>
        Owner = 1,

        /// <summary>
        /// 管理员
        /// </summary>
        Admin = 2,

        /// <summary>
        /// 成员
        /// </summary>
        Member = 3,

        /// <summary>
        /// 只读访客
        /// </summary>
        Viewer = 4
    }
}
