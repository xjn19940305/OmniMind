using OmniMind.Enums;

namespace OmniMind.Entities
{
    /// <summary>
    /// 刷新令牌
    /// </summary>
    public class RefreshToken : ITenantEntity
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 令牌字符串
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// JWT Token ID
        /// </summary>
        public string JwtId { get; set; } = string.Empty;

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 过期时间
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 是否已使用
        /// </summary>
        public bool IsUsed { get; set; } = false;

        /// <summary>
        /// 是否已撤销
        /// </summary>
        public bool IsRevoked { get; set; } = false;

        /// <summary>
        /// 撤销时间
        /// </summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// 替换的令牌ID（用于令牌轮换）
        /// </summary>
        public long? ReplacedByTokenId { get; set; }

        /// <summary>
        /// 使用时间
        /// </summary>
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// 设备信息/客户端IP
        /// </summary>
        public string? DeviceInfo { get; set; }

        /// <summary>
        /// 租户ID
        /// </summary>
        public long TenantId { get; set; }
    }
}
