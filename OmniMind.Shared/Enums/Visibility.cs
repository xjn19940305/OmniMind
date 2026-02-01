using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 可见性：用于知识库/资源的可见范围定义。
    /// </summary>
    public enum Visibility
    {
        /// <summary>
        /// 私有：仅创建者/授权用户可见
        /// </summary>
        Private = 1,

        /// <summary>
        /// 内部：租户内可见（视权限控制而定）
        /// </summary>
        Internal = 2,

        /// <summary>
        /// 公开：对外公开（谨慎使用，通常仍建议需要鉴权）
        /// </summary>
        Public = 3
    }
}
