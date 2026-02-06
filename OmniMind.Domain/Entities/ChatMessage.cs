using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OmniMind.Entities
{
    /// <summary>
    /// 聊天消息：会话中的单条消息记录
    /// </summary>
    [Table("chat_messages")]
    [Index(nameof(ConversationId))]
    [Index(nameof(ConversationId), nameof(CreatedAt))]
    public class ChatMessage
    {
        /// <summary>
        /// 消息主键
        /// </summary>
        [Key]
        [Column("id")]
        public string Id { get; set; } = Guid.CreateVersion7().ToString();

        /// <summary>
        /// 所属会话ID
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("conversation_id")]
        public string ConversationId { get; set; } = default!;

        /// <summary>
        /// 所属会话导航属性
        /// </summary>
        [ForeignKey(nameof(ConversationId))]
        public Conversation Conversation { get; set; } = default!;

        /// <summary>
        /// 消息角色：user / assistant / system
        /// </summary>
        [Required]
        [MaxLength(32)]
        [Column("role")]
        public string Role { get; set; } = default!;

        /// <summary>
        /// 消息内容
        /// </summary>
        [Required]
        [Column("content", TypeName = "text")]
        public string Content { get; set; } = default!;

        /// <summary>
        /// 使用的 Token 数量（可选，用于统计）
        /// </summary>
        [Column("tokens")]
        public int? Tokens { get; set; }

        /// <summary>
        /// 消息状态（用于异步流式响应）：pending / streaming / completed / failed
        /// </summary>
        [Required]
        [MaxLength(32)]
        [Column("status")]
        public string Status { get; set; } = "completed";

        /// <summary>
        /// 错误信息（当状态为 failed 时）
        /// </summary>
        [MaxLength(512)]
        [Column("error")]
        public string? Error { get; set; }

        /// <summary>
        /// 关联的知识库ID（该消息使用的知识库，可选）
        /// </summary>
        [MaxLength(64)]
        [Column("knowledge_base_id")]
        public string? KnowledgeBaseId { get; set; }

        /// <summary>
        /// 关联的文档ID（该消息使用的临时文档，可选）
        /// </summary>
        [MaxLength(64)]
        [Column("document_id")]
        public string? DocumentId { get; set; }

        /// <summary>
        /// 检索到的参考文档片段（JSON 格式，用于展示引用来源）
        /// </summary>
        [Column("references", TypeName = "jsonb")]
        public string? References { get; set; }

        /// <summary>
        /// 创建时间（UTC）
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 完成时间（UTC）（用于统计响应时长）
        /// </summary>
        [Column("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
