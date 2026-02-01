using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 来源类型：资源的进入方式。
    /// </summary>
    public enum SourceType
    {
        /// <summary>
        /// 上传
        /// </summary>
        Upload = 1,

        /// <summary>
        /// URL
        /// </summary>
        Url = 2,

        /// <summary>
        /// 外部导入（系统对接/批量导入）
        /// </summary>
        Import = 3
    }
}
