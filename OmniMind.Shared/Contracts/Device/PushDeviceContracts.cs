namespace OmniMind.Contracts.Device
{
    /// <summary>
    /// 绑定推送设备请求
    /// </summary>
    public record BindPushDeviceRequest
    {
        /// <summary>
        /// 个推 ClientId（必需）
        /// </summary>
        public string ClientId { get; init; } = string.Empty;

        /// <summary>
        /// 平台类型（Android, iOS, Web）
        /// </summary>
        public string Platform { get; init; } = string.Empty;

        /// <summary>
        /// 设备型号
        /// </summary>
        public string? DeviceModel { get; init; }

        /// <summary>
        /// 系统版本
        /// </summary>
        public string? OsVersion { get; init; }

        /// <summary>
        /// App 版本号
        /// </summary>
        public string? AppVersion { get; init; }
    }

    /// <summary>
    /// 推送设备响应
    /// </summary>
    public record PushDeviceResponse
    {
        /// <summary>
        /// 设备ID
        /// </summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>
        /// 个推 ClientId
        /// </summary>
        public string ClientId { get; init; } = string.Empty;

        /// <summary>
        /// 平台类型
        /// </summary>
        public string Platform { get; init; } = string.Empty;

        /// <summary>
        /// 设备型号
        /// </summary>
        public string? DeviceModel { get; set; }

        /// <summary>
        /// 系统版本
        /// </summary>
        public string? OsVersion { get; set; }

        /// <summary>
        /// App 版本
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        /// 是否启用推送
        /// </summary>
        public bool PushEnabled { get; set; }

        /// <summary>
        /// 最后活跃时间
        /// </summary>
        public DateTime? LastActiveAt { get; set; }
    }

    /// <summary>
    /// 推送请求
    /// </summary>
    public record PushNotificationRequest
    {
        /// <summary>
        /// 用户ID（推送给指定用户的所有设备）
        /// </summary>
        public string? UserId { get; init; }

        /// <summary>
        /// 设备ID列表（推送给指定设备，与 UserId 二选一）
        /// </summary>
        public List<string>? DeviceIds { get; init; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// 内容
        /// </summary>
        public string Content { get; init; } = string.Empty;

        /// <summary>
        /// 跳转链接（可选）
        /// </summary>
        public string? Url { get; init; }

        /// <summary>
        /// 额外数据（JSON，可选）
        /// </summary>
        public string? Payload { get; init; }
    }
}
