using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 刷新令牌
    /// </summary>
    [Table("refresh_tokens")]
    public class RefreshToken
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 令牌字符串
        /// </summary>
        [Required]
        [MaxLength(512)]
        [Column("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// JWT Token ID
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Column("jwt_id")]
        public string JwtId { get; set; } = string.Empty;

        /// <summary>
        /// 用户ID
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("user_id")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 过期时间
        /// </summary>
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 是否已使用
        /// </summary>
        [Column("is_used")]
        public bool IsUsed { get; set; } = false;

        /// <summary>
        /// 是否已撤销
        /// </summary>
        [Column("is_revoked")]
        public bool IsRevoked { get; set; } = false;

        /// <summary>
        /// 撤销时间
        /// </summary>
        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// 替换的令牌ID（用于令牌轮换）
        /// </summary>
        [MaxLength(64)]
        [Column("replaced_by_token_id")]
        public string? ReplacedByTokenId { get; set; }

        /// <summary>
        /// 使用时间
        /// </summary>
        [Column("used_at")]
        public DateTime? UsedAt { get; set; }

        /// <summary>
        /// 设备信息/客户端IP
        /// </summary>
        [MaxLength(512)]
        [Column("device_info")]
        public string? DeviceInfo { get; set; }
    }
}
