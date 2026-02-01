using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniMind.Enums
{
    /// <summary>
    /// 内容类型：多模态统一抽象（Document 的 ContentType）。
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// PDF 文档
        /// </summary>
        Pdf = 1,

        /// <summary>
        /// Word 文档
        /// </summary>
        Docx = 2,

        /// <summary>
        /// PowerPoint 演示文稿
        /// </summary>
        Pptx = 3,

        /// <summary>
        /// Markdown 文本
        /// </summary>
        Markdown = 4,

        /// <summary>
        /// 网页（URL）
        /// </summary>
        Web = 5,

        /// <summary>
        /// 图片（PNG/JPG/WebP 等）
        /// </summary>
        Image = 6,

        /// <summary>
        /// 音频（MP3/WAV/M4A 等）
        /// </summary>
        Audio = 7,

        /// <summary>
        /// 视频（MP4 等）
        /// </summary>
        Video = 8
    }
}
