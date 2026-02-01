using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 文档处理状态：用于展示与流程控制（解析/索引等）。
    /// </summary>
    public enum DocumentStatus
    {
        /// <summary>
        /// 已上传
        /// </summary>
        Uploaded = 1,

        /// <summary>
        /// 解析中
        /// </summary>
        Parsing = 2,

        /// <summary>
        /// 已解析
        /// </summary>
        Parsed = 3,

        /// <summary>
        /// 索引中（Embedding/Upsert）
        /// </summary>
        Indexing = 4,

        /// <summary>
        /// 已索引（可检索）
        /// </summary>
        Indexed = 5,

        /// <summary>
        /// 失败
        /// </summary>
        Failed = 6
    }
}
