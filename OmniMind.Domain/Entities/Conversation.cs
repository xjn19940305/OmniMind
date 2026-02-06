using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 聊天会话：用户的聊天对话容器
    /// </summary>
    [Table("conversations")]
    [Index(nameof(UserId))]
    [Index(nameof(UserId), nameof(UpdatedAt))]
    public class Conversation
    {
        /// <summary>
        /// 会话主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 会话标题（根据首条消息自动生成，用户可修改）
        /// </summary>
        [Required]
        [MaxLength(255)]
        [Column("title")]
        public string Title { get; set; } = default!;

        /// <summary>
        /// 所属用户ID
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("user_id")]
        public string UserId { get; set; } = default!;

        /// <summary>
        /// 所属用户导航属性
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = default!;

        /// <summary>
        /// 关联的知识库ID（可选，用于 RAG 聊天）
        /// </summary>
        [MaxLength(64)]
        [Column("knowledge_base_id")]
        public string? KnowledgeBaseId { get; set; }

        /// <summary>
        /// 关联的知识库导航属性
        /// </summary>
        [ForeignKey(nameof(KnowledgeBaseId))]
        public KnowledgeBase? KnowledgeBase { get; set; }

        /// <summary>
        /// 关联的临时文档ID（可选，用于临时文件聊天）
        /// </summary>
        [MaxLength(64)]
        [Column("document_id")]
        public string? DocumentId { get; set; }

        /// <summary>
        /// 关联的临时文档导航属性
        /// </summary>
        [ForeignKey(nameof(DocumentId))]
        public Document? Document { get; set; }

        /// <summary>
        /// 使用的模型ID
        /// </summary>
        [MaxLength(64)]
        [Column("model_id")]
        public string? ModelId { get; set; }

        /// <summary>
        /// 会话类型：simple(纯AI) / knowledge_base(RAG) / document(临时文件)
        /// </summary>
        [Required]
        [MaxLength(32)]
        [Column("conversation_type")]
        public string ConversationType { get; set; } = "simple";

        /// <summary>
        /// 是否置顶
        /// </summary>
        [Column("is_pinned")]
        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 更新时间（UTC）
        /// </summary>
        [Column("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        /// <summary>
        /// 聊天消息集合
        /// </summary>
        public ICollection<Entities.ChatMessage> Messages { get; set; } = new List<Entities.ChatMessage>();
    }
}
