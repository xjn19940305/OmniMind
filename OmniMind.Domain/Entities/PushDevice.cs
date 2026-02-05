namespace OmniMind.Entities
{
    /// <summary>
    /// 推送设备表（用于个推推送）
    /// </summary>
    public class PushDevice
    {
        /// <summary>
        /// 设备ID（主键）
        /// </summary>
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 个推 ClientId（设备唯一标识，推送必需）
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// 平台类型（Android, iOS, Web）
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// 设备型号（如 iPhone 14, Xiaomi 12）
        /// </summary>
        public string? DeviceModel { get; set; }

        /// <summary>
        /// 设备系统版本（如 iOS 16.0, Android 13）
        /// </summary>
        public string? OsVersion { get; set; }

        /// <summary>
        /// App 版本号
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        /// 设备别名（可用于个推 Alias 绑定）
        /// </summary>
        public string? Alias { get; set; }

        /// <summary>
        /// 是否启用推送
        /// </summary>
        public bool PushEnabled { get; set; } = true;

        /// <summary>
        /// 最后活跃时间
        /// </summary>
        public DateTime? LastActiveAt { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 导航属性 - 关联的用户
        /// </summary>
        public User? User { get; set; }
    }
}
