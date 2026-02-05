using System.ComponentModel.DataAnnotations;

namespace OmniMind.Entities
{
    /// <summary>
    /// 用户扩展信息表（完善信息）
    /// </summary>
    public class UserProfile
    {
        /// <summary>
        /// 用户ID（与 Users 表一对一关系）
        /// </summary>
        [Key]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 行业
        /// </summary>
        public string? Industry { get; set; }

        /// <summary>
        /// 职业
        /// </summary>
        public string? Occupation { get; set; }

        /// <summary>
        /// 了解渠道（从哪里知道的）
        /// </summary>
        public string? SourceChannel { get; set; }

        /// <summary>
        /// 公司/组织名称
        /// </summary>
        public string? Company { get; set; }

        /// <summary>
        /// 职位
        /// </summary>
        public string? Position { get; set; }

        /// <summary>
        /// 个人简介
        /// </summary>
        public string? Bio { get; set; }

        /// <summary>
        /// 兴趣标签（JSON 数组存储，如 ["AI", "编程", "阅读"]）
        /// </summary>
        public string? InterestTags { get; set; }

        /// <summary>
        /// 完善信息时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 导航属性 - 关联的用户
        /// </summary>
        public User? User { get; set; }
    }
}
