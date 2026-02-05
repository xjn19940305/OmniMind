namespace OmniMind.Contracts.User
{
    /// <summary>
    /// 完善用户信息请求
    /// </summary>
    public record CompleteUserProfileRequest
    {
        /// <summary>
        /// 行业
        /// </summary>
        public string? Industry { get; init; }

        /// <summary>
        /// 职业
        /// </summary>
        public string? Occupation { get; init; }

        /// <summary>
        /// 了解渠道
        /// </summary>
        public string? SourceChannel { get; init; }

        /// <summary>
        /// 公司/组织名称
        /// </summary>
        public string? Company { get; init; }

        /// <summary>
        /// 职位
        /// </summary>
        public string? Position { get; init; }

        /// <summary>
        /// 个人简介
        /// </summary>
        public string? Bio { get; init; }

        /// <summary>
        /// 兴趣标签
        /// </summary>
        public List<string>? InterestTags { get; init; }
    }

    /// <summary>
    /// 用户资料响应
    /// </summary>
    public record UserProfileResponse
    {
        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 昵称
        /// </summary>
        public string? NickName { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        public string? Picture { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public int? Gender { get; set; }

        /// <summary>
        /// 出生日期
        /// </summary>
        public DateTime? BirthDate { get; set; }

        /// <summary>
        /// 是否已完善信息
        /// </summary>
        public bool IsProfileCompleted { get; set; }

        /// <summary>
        /// 行业
        /// </summary>
        public string? Industry { get; set; }

        /// <summary>
        /// 职业
        /// </summary>
        public string? Occupation { get; set; }

        /// <summary>
        /// 了解渠道
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
        /// 兴趣标签
        /// </summary>
        public List<string>? InterestTags { get; set; }

        /// <summary>
        /// 完善信息时间
        /// </summary>
        public DateTime? CompletedAt { get; set; }
    }
}
