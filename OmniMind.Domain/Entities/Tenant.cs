using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 租户实体
    /// </summary>
    [Table("tenants")]
    public class Tenant
    {
        /// <summary>
        /// 租户ID
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 租户名称
        /// </summary>
        [Required]
        [MaxLength(128)]
        [Column("name")]
        public string Name { get; set; } = default!;

        /// <summary>
        /// 租户代码（用于登录时识别）
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("code")]
        public string Code { get; set; } = default!;

        /// <summary>
        /// 描述
        /// </summary>
        [MaxLength(500)]
        [Column("description")]
        public string? Description { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        [Column("is_enabled")]
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 更新时间
        /// </summary>
        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// 导航属性 - 关联的用户
        /// </summary>
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
