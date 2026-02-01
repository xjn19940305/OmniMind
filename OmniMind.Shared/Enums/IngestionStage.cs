using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 导入阶段：用于任务进度展示（SignalR 推送也会用到）。
    /// </summary>
    public enum IngestionStage
    {
        /// <summary>
        /// 上传阶段
        /// </summary>
        Upload = 1,

        /// <summary>
        /// 解析阶段
        /// </summary>
        Parse = 2,

        /// <summary>
        /// 切片阶段
        /// </summary>
        Chunk = 3,

        /// <summary>
        /// 向量化阶段
        /// </summary>
        Embed = 4,

        /// <summary>
        /// 索引阶段
        /// </summary>
        Index = 5
    }
}
