using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OmniMind.Abstractions.SignalR;
using OmniMind.Abstractions.Storage;
using OmniMind.Api.Swaggers;
using OmniMind.Contracts.Chat;
using OmniMind.Contracts.Common;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Ingestion;
using OmniMind.Messages;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.PostgreSql;
using OmniMind.Vector.Qdrant;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;
using AiChatRole = Microsoft.Extensions.AI.ChatRole;
using IRealtimeNotifier = OmniMind.Abstractions.SignalR.IRealtimeNotifier;

namespace App.Controllers
{
    /// <summary>
    /// 聊天模块
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : BaseController
    {
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly IChatClient chatClient;
        private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
        private readonly IVectorStore vectorStore;
        private readonly IObjectStorage objectStorage;
        private readonly IRealtimeNotifier realtimeNotifier;
        private readonly IMessagePublisher messagePublisher;
        private readonly ILogger<ChatController> logger;

        // 流式消息取消令牌管理
        private static readonly Dictionary<string, CancellationTokenSource> _cancellationTokenSources = new();

        public ChatController(
            IServiceScopeFactory serviceScopeFactory,
            IChatClient chatClient,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IVectorStore vectorStore,
            IObjectStorage objectStorage,
            IRealtimeNotifier realtimeNotifier,
            IMessagePublisher messagePublisher,
            ILogger<ChatController> logger)
        {
            this.serviceScopeFactory = serviceScopeFactory;
            this.chatClient = chatClient;
            this.embeddingGenerator = embeddingGenerator;
            this.vectorStore = vectorStore;
            this.objectStorage = objectStorage;
            this.realtimeNotifier = realtimeNotifier;
            this.messagePublisher = messagePublisher;
            this.logger = logger;
        }

        /// <summary>
        /// 统一聊天接口（通过 SignalR 流式响应）
        /// 如果提供了 KnowledgeBaseId 则使用 RAG 检索增强回答，否则直接调用模型
        /// </summary>
        [HttpPost("chatStream")]
        [ProducesResponseType(typeof(ChatStreamResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChatStream([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new ErrorResponse { Message = "消息内容不能为空" });
            }

            var userId = GetUserId();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            // 初始化会话并保存消息
            var (conversationId, assistantMessageId) = await InitializeConversationAsync(dbContext, userId, request);

            try
            {
                // 构建消息列表
                var messages = BuildMessagesList(request.History, request.Message);

                // 三种互斥的聊天模式：优先级 DocumentId > KnowledgeBaseId > 纯AI对话
                if (!string.IsNullOrWhiteSpace(request.DocumentId))
                {
                    // 临时文件聊天 - 验证文档存在且属于该用户
                    var document = await dbContext.Documents
                        .FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.CreatedByUserId == userId);

                    if (document == null)
                    {
                        return BadRequest(new ErrorResponse { Message = "文档不存在" });
                    }

                    // 后台处理临时文件聊天（使用 Task.Run 确保独立执行）
                    _ = Task.Run(() => ProcessDocumentChatAsync(assistantMessageId, conversationId, userId, request.DocumentId, request, messages));
                }
                else if (!string.IsNullOrWhiteSpace(request.KnowledgeBaseId))
                {
                    // 知识库聊天
                    var knowledgeBase = await dbContext.KnowledgeBases
                        .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId);

                    if (knowledgeBase == null)
                    {
                        return BadRequest(new ErrorResponse { Message = "知识库不存在" });
                    }

                    // 后台处理 RAG 聊天（使用 Task.Run 确保独立执行）
                    _ = Task.Run(() => ProcessRagChatAsync(assistantMessageId, conversationId, userId, knowledgeBase, request, messages));
                }
                else
                {
                    _ = Task.Run(async () =>
                    {
                        using (AiCallContext.BeginScope(userId, sessionId: conversationId))
                        {
                            logger.LogInformation("[Chat] 纯AI对话开始: MessageId={MessageId}, ConversationId={ConversationId}", assistantMessageId, conversationId);
                            // 后台处理简单聊天（使用 Task.Run 确保独立执行）
                            var aiMessages = ConvertToAiMessages(messages);
                            await ProcessSimpleChatAsync(assistantMessageId, conversationId, userId, aiMessages, request);
                        }
                    });
                }

                return Ok(new ChatStreamResponse
                {
                    MessageId = assistantMessageId,
                    ConversationId = conversationId
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 聊天请求处理失败");
                return BadRequest(new ErrorResponse { Message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 初始化会话：获取或创建会话，保存用户消息，创建助手消息记录
        /// </summary>
        private async Task<(string conversationId, string assistantMessageId)> InitializeConversationAsync(
            OmniMindDbContext dbContext,
            string userId,
            ChatRequest request)
        {
            // 获取或创建会话
            var conversationId = request.SessionId;
            Conversation? conversation = null;

            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                conversation = await dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);
            }

            if (conversation == null)
            {
                // 新建会话
                conversationId = IdGenerator.NewId();

                // 确定会话类型
                var conversationType = !string.IsNullOrWhiteSpace(request.DocumentId) ? "document"
                    : !string.IsNullOrWhiteSpace(request.KnowledgeBaseId) ? "knowledge_base"
                    : "simple";

                conversation = new Conversation
                {
                    Id = conversationId,
                    Title = GenerateConversationTitle(request.Message),
                    UserId = userId,
                    KnowledgeBaseId = request.KnowledgeBaseId,
                    DocumentId = request.DocumentId,
                    ModelId = request.Model,
                    ConversationType = conversationType,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                dbContext.Conversations.Add(conversation);
            }
            else
            {
                // 更新会话时间
                conversation.UpdatedAt = DateTimeOffset.UtcNow;
                dbContext.Conversations.Update(conversation);
            }

            await dbContext.SaveChangesAsync();

            // 保存用户消息
            var userMessage = new OmniMind.Entities.ChatMessage
            {
                Id = IdGenerator.NewId(),
                ConversationId = conversationId,
                Role = "user",
                Content = request.Message,
                Status = "completed",
                KnowledgeBaseId = request.KnowledgeBaseId,
                DocumentId = request.DocumentId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.ChatMessages.Add(userMessage);

            // 创建助手消息记录（初始状态为 streaming）
            var assistantMessageId = IdGenerator.NewId();
            var assistantMessage = new OmniMind.Entities.ChatMessage
            {
                Id = assistantMessageId,
                ConversationId = conversationId,
                Role = "assistant",
                Content = string.Empty,
                Status = "streaming",
                KnowledgeBaseId = request.KnowledgeBaseId,
                DocumentId = request.DocumentId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.ChatMessages.Add(assistantMessage);

            await dbContext.SaveChangesAsync();

            return (conversationId, assistantMessageId);
        }

        /// <summary>
        /// 检查文件哈希是否存在（用于文件去重复用）
        /// </summary>
        [HttpPost("check-file-hash")]
        [ProducesResponseType(typeof(CheckFileHashResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckFileHash([FromBody] CheckFileHashRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FileHash))
            {
                return BadRequest(new ErrorResponse { Message = "文件哈希不能为空" });
            }

            var userId = GetUserId();

            // 创建 scope 来获取 DbContext
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            // 查找当前用户的、相同哈希的、已索引完成的文档
            var existingDocument = await dbContext.Documents
                .Where(d => d.CreatedByUserId == userId
                    && d.FileHash == request.FileHash
                    && d.Status == DocumentStatus.Indexed)
                .OrderByDescending(d => d.CreatedAt)
                .FirstOrDefaultAsync();

            if (existingDocument != null)
            {
                logger.LogInformation("[Chat] 文件哈希已存在，返回已存在文件: DocumentId={DocumentId}, FileHash={FileHash}",
                    existingDocument.Id, request.FileHash);

                return Ok(new CheckFileHashResponse
                {
                    Id = existingDocument.Id,
                    Name = existingDocument.Title,
                    Type = GetAttachmentType(existingDocument.ContentType),
                    Url = $"/api/Chat/files/{existingDocument.Id}",
                    Size = 0, // 数据库中没有 Size 字段，设为 0
                    Status = 5 // Indexed
                });
            }

            // 文件不存在，返回 null（前端会继续上传流程）
            logger.LogInformation("[Chat] 文件哈希不存在，需要上传: FileHash={FileHash}", request.FileHash);
            return Ok(null);
        }

        /// <summary>
        /// 上传临时文件（用于聊天的附件）
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadTemporaryFile([FromForm] UploadFileRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ErrorResponse { Message = "文件不能为空" });
            }

            var userId = GetUserId();
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
            var contentType = GetContentType(request.File.FileName);

            try
            {
                var objectKey = $"temp/{sessionId}/{Guid.CreateVersion7()}/{request.File.FileName}";

                logger.LogInformation("[Chat] 上传临时文件: {FileName}, Size: {Size}, FileHash: {FileHash}",
                    request.File.FileName, request.File.Length, request.FileHash ?? "(未提供)");

                await objectStorage.PutAsync(objectKey, request.File.OpenReadStream(), contentType, ct: Response.HttpContext.RequestAborted);

                // 创建 scope 来获取 DbContext
                using var scope = serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

                var document = new Document
                {
                    Id = Guid.CreateVersion7().ToString(),
                    KnowledgeBaseId = null,
                    FolderId = null,
                    Title = request.File.FileName,
                    ContentType = contentType,
                    SourceType = SourceType.Upload,
                    SourceUri = null,
                    ObjectKey = objectKey,
                    FileHash = request.FileHash, // 保存文件哈希值
                    Language = null,
                    Status = DocumentStatus.Uploaded,
                    Error = null,
                    Duration = null,
                    Transcription = null,
                    SessionId = sessionId,
                    CreatedByUserId = userId,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                dbContext.Documents.Add(document);
                await dbContext.SaveChangesAsync(Response.HttpContext.RequestAborted);

                logger.LogInformation("[Chat] 临时文件上传成功: DocumentId={DocumentId}", document.Id);

                // 发布文档上传消息到队列
                try
                {
                    var uploadMessage = new DocumentUploadMessage
                    {
                        DocumentId = document.Id,
                        KnowledgeBaseId = string.Empty,
                        ObjectKey = objectKey,
                        FileName = request.File.FileName,
                        ContentType = contentType
                    };

                    await messagePublisher.PublishDocumentUploadAsync(uploadMessage);
                    logger.LogInformation("[Chat] 已发布文档上传消息: DocumentId={DocumentId}", document.Id);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "[Chat] 发布文档上传消息失败: DocumentId={DocumentId}", document.Id);
                }

                return Ok(new UploadResponse
                {
                    Id = document.Id,
                    Name = document.Title,
                    Type = GetAttachmentType(contentType),
                    Url = $"/api/Chat/files/{document.Id}",
                    Size = (int)request.File.Length,
                    SessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 上传临时文件失败");
                return BadRequest(new ErrorResponse { Message = "文件上传失败" });
            }
        }

        /// <summary>
        /// 获取临时文件内容
        /// </summary>
        [HttpGet("files/{documentId}")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTemporaryFile(string documentId)
        {
            var userId = GetUserId();

            // 创建 scope 来获取 DbContext
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var document = await dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.CreatedByUserId == userId);

            if (document == null)
            {
                return NotFound(new ErrorResponse { Message = "文件不存在" });
            }

            try
            {
                var stream = await objectStorage.GetAsync(document.ObjectKey);
                return File(stream, document.ContentType, document.Title);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 获取文件失败: DocumentId={DocumentId}", documentId);
                return NotFound(new ErrorResponse { Message = "文件获取失败" });
            }
        }

        /// <summary>
        /// 获取用户的会话列表
        /// </summary>
        [HttpGet("conversations")]
        [ProducesResponseType(typeof(ConversationListResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetConversations(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? type = null)
        {
            var userId = GetUserId();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var query = dbContext.Conversations
                .Where(c => c.UserId == userId);

            // 按类型过滤
            if (!string.IsNullOrWhiteSpace(type))
            {
                query = query.Where(c => c.ConversationType == type);
            }

            // 总数
            var total = await query.CountAsync();

            // 分页查询，置顶的排在前面，然后按更新时间降序
            var conversations = await query
                .OrderByDescending(c => c.IsPinned)
                .ThenByDescending(c => c.UpdatedAt)
                .ThenByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ConversationResponse
                {
                    Id = c.Id,
                    Title = c.Title,
                    ConversationType = c.ConversationType,
                    KnowledgeBaseId = c.KnowledgeBaseId,
                    DocumentId = c.DocumentId,
                    ModelId = c.ModelId,
                    IsPinned = c.IsPinned,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    MessageCount = dbContext.ChatMessages
                        .Count(m => m.ConversationId == c.Id),
                    LastMessage = dbContext.ChatMessages
                        .Where(m => m.ConversationId == c.Id)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    LastMessageAt = dbContext.ChatMessages
                        .Where(m => m.ConversationId == c.Id)
                        .OrderByDescending(m => m.CreatedAt)
                        .Select(m => (DateTimeOffset?)m.CreatedAt)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(new ConversationListResponse
            {
                Conversations = conversations,
                Total = total
            });
        }

        /// <summary>
        /// 获取会话详情（包含消息列表）
        /// </summary>
        [HttpGet("conversations/{conversationId}")]
        [ProducesResponseType(typeof(ConversationDetailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetConversation(string conversationId)
        {
            var userId = GetUserId();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

            if (conversation == null)
            {
                return NotFound(new ErrorResponse { Message = "会话不存在" });
            }

            var messages = await dbContext.ChatMessages
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    Role = m.Role,
                    Content = m.Content,
                    Status = m.Status,
                    Error = m.Error,
                    KnowledgeBaseId = m.KnowledgeBaseId,
                    DocumentId = m.DocumentId,
                    References = m.References,
                    CreatedAt = m.CreatedAt,
                    CompletedAt = m.CompletedAt
                })
                .ToListAsync();

            return Ok(new ConversationDetailResponse
            {
                Id = conversation.Id,
                Title = conversation.Title,
                ConversationType = conversation.ConversationType,
                KnowledgeBaseId = conversation.KnowledgeBaseId,
                DocumentId = conversation.DocumentId,
                ModelId = conversation.ModelId,
                IsPinned = conversation.IsPinned,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                Messages = messages
            });
        }

        /// <summary>
        /// 更新会话标题
        /// </summary>
        [HttpPut("conversations/{conversationId}/title")]
        [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateConversationTitle(
            string conversationId,
            [FromBody] UpdateConversationTitleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(new ErrorResponse { Message = "标题不能为空" });
            }

            var userId = GetUserId();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

            if (conversation == null)
            {
                return NotFound(new ErrorResponse { Message = "会话不存在" });
            }

            conversation.Title = request.Title;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();

            return Ok(new ConversationResponse
            {
                Id = conversation.Id,
                Title = conversation.Title,
                ConversationType = conversation.ConversationType,
                KnowledgeBaseId = conversation.KnowledgeBaseId,
                DocumentId = conversation.DocumentId,
                ModelId = conversation.ModelId,
                IsPinned = conversation.IsPinned,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                MessageCount = await dbContext.ChatMessages.CountAsync(m => m.ConversationId == conversationId)
            });
        }

        /// <summary>
        /// 置顶/取消置顶会话
        /// </summary>
        [HttpPut("conversations/{conversationId}/pin")]
        [ProducesResponseType(typeof(ConversationResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ToggleConversationPin(
            string conversationId,
            [FromBody] TogglePinRequest request)
        {
            var userId = GetUserId();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

            if (conversation == null)
            {
                return NotFound(new ErrorResponse { Message = "会话不存在" });
            }

            conversation.IsPinned = request.IsPinned;
            conversation.UpdatedAt = DateTimeOffset.UtcNow;

            await dbContext.SaveChangesAsync();

            return Ok(new ConversationResponse
            {
                Id = conversation.Id,
                Title = conversation.Title,
                ConversationType = conversation.ConversationType,
                KnowledgeBaseId = conversation.KnowledgeBaseId,
                DocumentId = conversation.DocumentId,
                ModelId = conversation.ModelId,
                IsPinned = conversation.IsPinned,
                CreatedAt = conversation.CreatedAt,
                UpdatedAt = conversation.UpdatedAt,
                MessageCount = await dbContext.ChatMessages.CountAsync(m => m.ConversationId == conversationId)
            });
        }

        /// <summary>
        /// 删除会话
        /// </summary>
        [HttpDelete("conversations/{conversationId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteConversation(string conversationId)
        {
            var userId = GetUserId();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var conversation = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId);

            if (conversation == null)
            {
                return NotFound(new ErrorResponse { Message = "会话不存在" });
            }

            dbContext.Conversations.Remove(conversation);
            await dbContext.SaveChangesAsync();

            return NoContent();
        }

        /// <summary>
        /// 取消流式消息生成
        /// </summary>
        [HttpPost("cancel/{messageId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public IActionResult CancelStreamingMessage(string messageId)
        {
            if (_cancellationTokenSources.TryGetValue(messageId, out var cts))
            {
                logger.LogInformation("[Chat] 收到取消请求: MessageId={MessageId}", messageId);
                cts.Cancel();
                _cancellationTokenSources.Remove(messageId);
                return Ok(new { Message = "已取消" });
            }
            return NotFound(new ErrorResponse { Message = "消息不存在或已完成" });
        }

        #region 私有辅助方法

        /// <summary>
        /// 处理简单聊天（不使用文档）
        /// </summary>
        private async Task ProcessSimpleChatAsync(
            string messageId,
            string conversationId,
            string userId,
            List<AiChatMessage> aiMessages,
            ChatRequest request)
        {
            // 创建取消令牌
            var cts = new CancellationTokenSource();
            _cancellationTokenSources[messageId] = cts;

            var sb = new System.Text.StringBuilder();

            try
            {
                var options = BuildChatOptions(request);
                options.ModelId = request.Model;

                using (AiCallContext.BeginScope(userId, sessionId: conversationId))
                {
                    await foreach (var update in chatClient.CompleteStreamingAsync(aiMessages, options, cts.Token))
                    {
                        if (!string.IsNullOrEmpty(update.Text))
                        {
                            // 清理多余的空行后再发送
                            var cleanedContent = CleanExtraNewlines(update.Text);
                            sb.Append(cleanedContent);
                            await SendStreamingChunkAsync(userId, conversationId, messageId, cleanedContent, isComplete: false);
                        }
                    }
                }

                // 发送完整内容
                var finalContent = CleanExtraNewlines(sb.ToString());
                await SendStreamingChunkAsync(userId, conversationId, messageId, finalContent, isComplete: true);

                // 保存助手消息到数据库
                await SaveAssistantMessageAsync(messageId, conversationId, finalContent, status: "completed");

                logger.LogInformation("[Chat] 简单聊天完成: MessageId={MessageId}", messageId);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[Chat] 聊天已取消: MessageId={MessageId}", messageId);
                var partialContent = sb.ToString();
                if (!string.IsNullOrEmpty(partialContent))
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, partialContent, isComplete: true);
                    await SaveAssistantMessageAsync(messageId, conversationId, partialContent, status: "completed");
                }
                else
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);
                    await SaveAssistantMessageAsync(messageId, conversationId, string.Empty, status: "failed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 简单聊天处理失败: MessageId={MessageId}", messageId);
                await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);

                // 保存失败状态
                await SaveAssistantMessageAsync(messageId, conversationId, string.Empty, status: "failed");
            }
            finally
            {
                // 清理取消令牌
                _cancellationTokenSources.Remove(messageId);
            }
        }

        /// <summary>
        /// 处理 RAG 聊天（使用文档检索）
        /// </summary>
        private async Task ProcessRagChatAsync(
            string messageId,
            string conversationId,
            string userId,
            KnowledgeBase knowledgeBase,
            ChatRequest request,
            List<OmniMind.Contracts.Chat.ChatMessage> baseMessages)
        {
            // 创建取消令牌
            var cts = new CancellationTokenSource();
            _cancellationTokenSources[messageId] = cts;

            // 创建新的 scope 以避免 DbContext 被释放
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var sb = new System.Text.StringBuilder();

            try
            {
                // 1. 生成查询向量并检索相关文档
                logger.LogInformation("[Chat] 正在检索相关文档: MessageId={MessageId}", messageId);
                var context = await RetrieveRelevantContextAsync(dbContext, knowledgeBase.Id, request.Message, request.TopK, cts.Token);

                if (string.IsNullOrEmpty(context))
                {
                    var notFoundMsg = "知识库中没有找到相关文档";
                    await SendStreamingChunkAsync(userId, conversationId, messageId, notFoundMsg, isComplete: true);
                    // 保存消息到数据库
                    await SaveAssistantMessageAsync(messageId, conversationId, notFoundMsg, status: "completed");
                    logger.LogWarning("[Chat] 未找到相关文档: MessageId={MessageId}", messageId);
                    return;
                }
                // 2. 构建带上下文的消息列表
                var messages = BuildRagMessages(context, baseMessages);
                // 3. 转换为 AI 消息并调用 LLM
                var aiMessages = ConvertToAiMessages(messages);
                var options = BuildChatOptions(request);

                logger.LogInformation("[Chat] 正在调用 LLM 生成回复: MessageId={MessageId}", messageId);

                await foreach (var update in chatClient.CompleteStreamingAsync(aiMessages, options, cts.Token))
                {
                    if (update.Text != null)
                    {
                        sb.Append(update.Text);
                        // 清理多余的空行后再发送
                        var cleanedContent = CleanExtraNewlines(sb.ToString());
                        await SendStreamingChunkAsync(userId, conversationId, messageId, cleanedContent, isComplete: false);
                    }
                }
                var finalContent = CleanExtraNewlines(sb.ToString());
                await SendStreamingChunkAsync(userId, conversationId, messageId, finalContent, isComplete: true);

                // 保存助手消息到数据库
                await SaveAssistantMessageAsync(messageId, conversationId, finalContent, status: "completed");

                logger.LogInformation("[Chat] RAG 聊天完成: MessageId={MessageId}", messageId);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[Chat] RAG 聊天已取消: MessageId={MessageId}", messageId);
                // 取消时保存部分内容
                var partialContent = sb.ToString();
                if (!string.IsNullOrEmpty(partialContent))
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, partialContent, isComplete: true);
                    await SaveAssistantMessageAsync(messageId, conversationId, partialContent, status: "completed");
                }
                else
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);
                    await SaveAssistantMessageAsync(messageId, conversationId, string.Empty, status: "failed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] RAG 聊天处理失败: MessageId={MessageId}", messageId);
                await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);

                // 保存失败状态
                await SaveAssistantMessageAsync(messageId, conversationId, string.Empty, status: "failed");
            }
            finally
            {
                // 清理取消令牌
                _cancellationTokenSources.Remove(messageId);
            }
        }

        /// <summary>
        /// 处理临时文件聊天（使用临时上传的文档检索）
        /// </summary>
        private async Task ProcessDocumentChatAsync(
            string messageId,
            string conversationId,
            string userId,
            string documentId,
            ChatRequest request,
            List<OmniMind.Contracts.Chat.ChatMessage> baseMessages)
        {
            // 创建取消令牌
            var cts = new CancellationTokenSource();
            _cancellationTokenSources[messageId] = cts;

            // 创建新的 scope 以避免 DbContext 被释放
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var sb = new System.Text.StringBuilder();

            try
            {
                // 1. 验证文档存在且属于该用户
                var document = await dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == documentId && d.CreatedByUserId == userId, cts.Token);

                if (document == null)
                {
                    var notFoundMsg = "文档不存在";
                    await SendStreamingChunkAsync(userId, conversationId, messageId, notFoundMsg, isComplete: true);
                    // 保存消息到数据库
                    await SaveAssistantMessageAsync(messageId, conversationId, notFoundMsg, status: "completed");
                    logger.LogWarning("[Chat] 文档不存在: DocumentId={DocumentId}", documentId);
                    return;
                }

                // 2. 检查文档状态，只有已索引的文档才能用于聊天
                if (document.Status != OmniMind.Enums.DocumentStatus.Indexed)
                {
                    var statusText = document.Status switch
                    {
                        OmniMind.Enums.DocumentStatus.Uploaded => "文件已上传，正在等待处理...",
                        OmniMind.Enums.DocumentStatus.Parsing => "文件正在解析中...",
                        OmniMind.Enums.DocumentStatus.Parsed => "文件已解析，正在建立索引...",
                        OmniMind.Enums.DocumentStatus.Indexing => "文件正在建立索引，请稍候...",
                        OmniMind.Enums.DocumentStatus.Failed => $"文件处理失败：{document.Error ?? "未知错误"}",
                        _ => "文件状态异常"
                    };
                    await SendStreamingChunkAsync(userId, conversationId, messageId, statusText, isComplete: true);
                    // 保存消息到数据库
                    await SaveAssistantMessageAsync(messageId, conversationId, statusText, status: "completed");
                    logger.LogWarning("[Chat] 文档未就绪: DocumentId={DocumentId}, Status={Status}", documentId, document.Status);
                    return;
                }

                // 3. 检索相关上下文（从 documents_kb_ collection）
                logger.LogInformation("[Chat] 正在检索临时文档: MessageId={MessageId}, DocumentId={DocumentId}", messageId, documentId);
                var context = await RetrieveDocumentContextAsync(dbContext, documentId, request.Message, request.TopK, cts.Token);

                if (string.IsNullOrEmpty(context))
                {
                    var notFoundMsg = "文档中没有找到相关内容";
                    await SendStreamingChunkAsync(userId, conversationId, messageId, notFoundMsg, isComplete: true);
                    // 保存消息到数据库
                    await SaveAssistantMessageAsync(messageId, conversationId, notFoundMsg, status: "completed");
                    logger.LogWarning("[Chat] 未找到相关文档片段: MessageId={MessageId}", messageId);
                    return;
                }

                // 3. 构建带上下文的消息列表
                var messages = BuildRagMessages(context, baseMessages);

                // 4. 转换为 AI 消息并调用 LLM
                var aiMessages = ConvertToAiMessages(messages);
                var options = BuildChatOptions(request);

                logger.LogInformation("[Chat] 正在调用 LLM 生成回复: MessageId={MessageId}", messageId);

                await foreach (var update in chatClient.CompleteStreamingAsync(aiMessages, options, cts.Token))
                {
                    if (update.Text != null)
                    {
                        sb.Append(update.Text);
                        // 清理多余的空行后再发送
                        var cleanedContent = CleanExtraNewlines(sb.ToString());
                        await SendStreamingChunkAsync(userId, conversationId, messageId, cleanedContent, isComplete: false);
                    }
                }

                var finalContent = CleanExtraNewlines(sb.ToString());
                await SendStreamingChunkAsync(userId, conversationId, messageId, finalContent, isComplete: true);

                // 保存助手消息到数据库
                await SaveAssistantMessageAsync(messageId, conversationId, finalContent, status: "completed");

                logger.LogInformation("[Chat] 临时文件聊天完成: MessageId={MessageId}", messageId);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[Chat] 临时文件聊天已取消: MessageId={MessageId}", messageId);
                // 取消时保存部分内容
                var partialContent = sb.ToString();
                if (!string.IsNullOrEmpty(partialContent))
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, partialContent, isComplete: true);
                    await SaveAssistantMessageAsync(messageId, conversationId, partialContent, status: "completed");
                }
                else
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);
                    await SaveAssistantMessageAsync(messageId, conversationId, string.Empty, status: "failed");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 临时文件聊天处理失败: MessageId={MessageId}", messageId);
                await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);

                // 保存失败状态
                await SaveAssistantMessageAsync(messageId, conversationId, string.Empty, status: "failed");
            }
            finally
            {
                // 清理取消令牌
                _cancellationTokenSources.Remove(messageId);
            }
        }

        /// <summary>
        /// 检索相关上下文
        /// </summary>
        private async Task<string?> RetrieveRelevantContextAsync(
            OmniMindDbContext dbContext,
            string knowledgeBaseId,
            string query,
            int topK,
            CancellationToken cancellationToken = default)
        {
            var queryEmbedding = await embeddingGenerator.GenerateAsync(new[] { query }, null, cancellationToken);
            var queryVector = queryEmbedding.First().Vector.ToArray();

            // 直接使用 knowledgeBaseId，不要调用 GenerateTenantCollectionName（会在 IVectorStore 内部添加前缀）
            var searchResults = await vectorStore.SearchAsync(knowledgeBaseId, queryVector, new VectorSearchOptions(
                Limit: topK,
                WithPayload: true
            ));

            if (searchResults.Count == 0)
            {
                return null;
            }

            var chunkIds = searchResults.Select(r => r.Id).ToList();
            var chunks = await dbContext.Chunks
                .Include(c => c.Document)
                .Where(c => chunkIds.Contains(c.VectorPointId ?? c.Id))
                .ToListAsync(cancellationToken);

            var contextParts = new List<string>();
            foreach (var hit in searchResults)
            {
                var chunk = chunks.FirstOrDefault(c => (c.VectorPointId ?? c.Id) == hit.Id);
                if (chunk != null)
                {
                    contextParts.Add($"【{chunk.Document.Title}】\n{chunk.Content}");
                }
            }

            logger.LogInformation("[Chat] 检索到 {Count} 个相关文档片段", contextParts.Count);
            return string.Join("\n\n", contextParts);
        }

        /// <summary>
        /// 从固定 collection 检索特定文档的向量（用于临时文件聊天）
        /// </summary>
        private async Task<string?> RetrieveDocumentContextAsync(
            OmniMindDbContext dbContext,
            string documentId,
            string query,
            int topK,
            CancellationToken cancellationToken = default)
        {
            // 检查文档的向量点是否存在
            var chunkCount = await dbContext.Chunks
                .CountAsync(c => c.DocumentId == documentId && c.VectorPointId != null, cancellationToken);

            logger.LogInformation("[Chat] 文档向量检查: DocumentId={DocumentId}, ChunkCount={ChunkCount}",
                documentId, chunkCount);

            if (chunkCount == 0)
            {
                logger.LogWarning("[Chat] 文档没有向量数据: DocumentId={DocumentId}", documentId);
                throw new InvalidOperationException($"文档尚未完成向量化处理，请稍后再试 (DocumentId: {documentId})");
            }

            var queryEmbedding = await embeddingGenerator.GenerateAsync(new[] { query }, null, cancellationToken);
            var queryVector = queryEmbedding.First().Vector.ToArray();

            // 临时文件使用固定的 collection，通过 document_id 过滤器区分文档
            var collectionName = string.Empty;

            logger.LogInformation("[Chat] 临时文件检索: DocumentId={DocumentId}, Collection={Collection}",
                documentId, string.IsNullOrEmpty(collectionName) ? "documents_kb_" : collectionName);

            // 添加 document_id 过滤器
            var filter = new VectorFilter(new[]
            {
                new VectorCondition("document_id", "match", documentId)
            });

            var searchOptions = new VectorSearchOptions(
                Limit: topK,
                Filter: filter,
                WithPayload: true
            );

            IReadOnlyList<VectorSearchHit> searchResults;
            try
            {
                searchResults = await vectorStore.SearchAsync(collectionName, queryVector, searchOptions);
                logger.LogInformation("[Chat] 检索完成: DocumentId={DocumentId}, ResultCount={Count}",
                    documentId, searchResults.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 临时文件检索失败: DocumentId={DocumentId}, Collection={Collection}",
                    documentId, collectionName);
                throw new InvalidOperationException($"向量检索失败: {ex.Message}", ex);
            }

            if (searchResults.Count == 0)
            {
                return null;
            }

            var chunkIds = searchResults.Select(r => r.Id).ToList();
            var chunks = await dbContext.Chunks
                .Include(c => c.Document)
                .Where(c => chunkIds.Contains(c.VectorPointId ?? c.Id))
                .ToListAsync();

            var contextParts = new List<string>();
            foreach (var hit in searchResults)
            {
                var chunk = chunks.FirstOrDefault(c => (c.VectorPointId ?? c.Id) == hit.Id);
                if (chunk != null)
                {
                    contextParts.Add($"【{chunk.Document.Title}】\n{chunk.Content}");
                }
            }

            logger.LogInformation("[Chat] 从临时文件检索到 {Count} 个相关片段", contextParts.Count);
            return string.Join("\n\n", contextParts);
        }

        /// <summary>
        /// 构建消息列表
        /// </summary>
        private static List<OmniMind.Contracts.Chat.ChatMessage> BuildMessagesList(
            List<OmniMind.Contracts.Chat.ChatMessage>? history,
            string currentMessage)
        {
            var messages = new List<OmniMind.Contracts.Chat.ChatMessage>();

            if (history != null)
            {
                messages.AddRange(history);
            }

            messages.Add(new OmniMind.Contracts.Chat.ChatMessage { Role = "user", Content = currentMessage });
            return messages;
        }

        /// <summary>
        /// 构建 RAG 消息列表（添加系统提示词）
        /// </summary>
        private static List<OmniMind.Contracts.Chat.ChatMessage> BuildRagMessages(
            string context,
            List<OmniMind.Contracts.Chat.ChatMessage> baseMessages)
        {
            var systemPrompt = $"你是一个智能助手。请根据以下参考文档回答用户的问题。如果参考文档中没有相关信息，请明确告知。\n\n参考文档：\n{context}";

            var messages = new List<OmniMind.Contracts.Chat.ChatMessage>
            {
                new OmniMind.Contracts.Chat.ChatMessage { Role = "system", Content = systemPrompt }
            };

            // 跳过历史消息中的 system 消息
            foreach (var msg in baseMessages.Where(m => m.Role != "system"))
            {
                messages.Add(msg);
            }

            return messages;
        }

        /// <summary>
        /// 转换为 Microsoft.Extensions.AI 的 ChatMessage
        /// </summary>
        private static List<AiChatMessage> ConvertToAiMessages(List<OmniMind.Contracts.Chat.ChatMessage> messages)
        {
            return messages.Select(m => new AiChatMessage(GetChatRole(m.Role), m.Content)).ToList();
        }

        /// <summary>
        /// 构建聊天选项
        /// </summary>
        private static ChatOptions BuildChatOptions(ChatRequest request)
        {
            var options = new ChatOptions();

            if (!string.IsNullOrWhiteSpace(request.Model))
            {
                options.ModelId = request.Model;
            }

            if (request.Temperature.HasValue)
            {
                options.AdditionalProperties["temperature"] = request.Temperature.Value;
            }

            if (request.MaxTokens.HasValue)
            {
                options.AdditionalProperties["max_tokens"] = request.MaxTokens.Value;
            }

            return options;
        }

        /// <summary>
        /// 清理多余的空行（保留最多1个空行）
        /// </summary>
        private static string CleanExtraNewlines(string content)
        {
            if (string.IsNullOrEmpty(content))
                return content;

            // 使用正则表达式：将3个或以上的连续换行符替换为2个（保留1个空行）
            var cleaned = System.Text.RegularExpressions.Regex.Replace(content, @"\n{3,}", "\n\n");

            // 移除开头的空行
            cleaned = cleaned.TrimStart('\n', '\r');

            // 移除结尾的空行
            cleaned = cleaned.TrimEnd('\n', '\r');

            return cleaned;
        }

        /// <summary>
        /// 发送流式消息片段
        /// </summary>
        private async Task SendStreamingChunkAsync(
            string userId,
            string conversationId,
            string messageId,
            string content,
            bool isComplete)
        {
            await realtimeNotifier.NotifyChatMessageAsync(userId, conversationId,
                new SignalRChatMessage
                {
                    MessageId = messageId,
                    Role = "assistant",
                    Content = content,
                    IsComplete = isComplete,
                    Timestamp = DateTimeOffset.UtcNow
                });
        }

        /// <summary>
        /// 将字符串角色转换为 AiChatRole
        /// </summary>
        private static AiChatRole GetChatRole(string role)
        {
            return role.ToLower() switch
            {
                "user" => AiChatRole.User,
                "assistant" => AiChatRole.Assistant,
                "system" => AiChatRole.System,
                _ => AiChatRole.User
            };
        }

        /// <summary>
        /// 获取 Content Type
        /// </summary>
        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" or ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".ppt" or ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".md" or ".markdown" => "text/markdown",
                ".txt" => "text/plain",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".mp3" => "audio/mpeg",
                ".wav" => "audio/wav",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// 获取附件类型
        /// </summary>
        private static string GetAttachmentType(string contentType)
        {
            return contentType switch
            {
                "application/pdf" => "pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "word",
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => "ppt",
                "text/markdown" => "markdown",
                "text/plain" => "txt",
                "image/jpeg" or "image/png" or "image/gif" => "image",
                "audio/mpeg" or "audio/wav" => "audio",
                "video/mp4" or "video/webm" => "video",
                _ => "file"
            };
        }

        /// <summary>
        /// 生成会话标题（基于首条用户消息）
        /// </summary>
        private static string GenerateConversationTitle(string firstMessage)
        {
            // 移除换行符和多余空格
            var cleaned = System.Text.RegularExpressions.Regex.Replace(firstMessage, @"\s+", " ").Trim();

            // 限制标题长度
            if (cleaned.Length <= 50)
                return cleaned;

            return cleaned.Substring(0, 47) + "...";
        }

        /// <summary>
        /// 保存助手消息到数据库
        /// </summary>
        private async Task SaveAssistantMessageAsync(
            string messageId,
            string conversationId,
            string content,
            string status = "completed")
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            var message = await dbContext.ChatMessages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.ConversationId == conversationId);

            if (message != null)
            {
                message.Content = content;
                message.Status = status;
                message.CompletedAt = status == "completed" ? DateTimeOffset.UtcNow : null;

                // 更新会话的更新时间
                var conversation = await dbContext.Conversations
                    .FirstOrDefaultAsync(c => c.Id == conversationId);
                if (conversation != null)
                {
                    conversation.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await dbContext.SaveChangesAsync();
            }
        }

        #endregion
    }
}
