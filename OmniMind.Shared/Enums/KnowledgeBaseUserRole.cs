using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 知识库用户角色（用于权限判断）
    /// Owner 是通过 OwnerUserId 判断的，不是成员角色
    /// </summary>
    public enum KnowledgeBaseUserRole
    {
        /// <summary>
        /// 拥有者 - 完全控制
        /// </summary>
        Owner = 0,

        /// <summary>
        /// 管理员 - 可管理成员、编辑文档
        /// </summary>
        Admin = 1,

        /// <summary>
        /// 编辑 - 可上传、编辑、删除文档
        /// </summary>
        Editor = 2,

        /// <summary>
        /// 查看者 - 只读权限
        /// </summary>
        Viewer = 3
    }
}
