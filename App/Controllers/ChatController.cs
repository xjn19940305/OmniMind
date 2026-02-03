using App.Swaggers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OmniMind.Abstractions.SignalR;
using OmniMind.Abstractions.Storage;
using OmniMind.Contracts.Chat;
using OmniMind.Contracts.Common;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Messages;
using OmniMind.Messaging.Abstractions;
using OmniMind.Persistence.MySql;
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
        private readonly OmniMindDbContext dbContext;
        private readonly IChatClient chatClient;
        private readonly IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
        private readonly IVectorStore vectorStore;
        private readonly IObjectStorage objectStorage;
        private readonly IRealtimeNotifier realtimeNotifier;
        private readonly IMessagePublisher messagePublisher;
        private readonly ILogger<ChatController> logger;

        public ChatController(
            OmniMindDbContext dbContext,
            IChatClient chatClient,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IVectorStore vectorStore,
            IObjectStorage objectStorage,
            IRealtimeNotifier realtimeNotifier,
            IMessagePublisher messagePublisher,
            ILogger<ChatController> logger)
        {
            this.dbContext = dbContext;
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
        [HttpPost("chatStream", Name = "统一聊天流式")]
        [ProducesResponseType(typeof(ChatStreamResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ChatStream([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new ErrorResponse { Message = "消息内容不能为空" });
            }

            var tenantId = GetTenantId();
            var userId = GetUserId();
            var messageId = Guid.NewGuid().ToString();
            var conversationId = request.SessionId ?? Guid.NewGuid().ToString();

            try
            {
                // 构建消息列表
                var messages = BuildMessagesList(request.History, request.Message);

                // 如果提供了知识库ID，使用 RAG 检索增强
                if (!string.IsNullOrWhiteSpace(request.KnowledgeBaseId) || !string.IsNullOrWhiteSpace(request.DocumentId))
                {
                    var knowledgeBase = await dbContext.KnowledgeBases
                        .FirstOrDefaultAsync(kb => kb.Id == request.KnowledgeBaseId && kb.TenantId == tenantId);
                    var document = await dbContext.Documents.Where(x => x.Id == request.DocumentId).FirstOrDefaultAsync();
                    if (knowledgeBase != null)
                    {
                        // 后台处理 RAG 聊天
                        _ = ProcessRagChatAsync(messageId, conversationId, tenantId, userId, knowledgeBase, request, messages);
                    }
                    else if (document != null)
                    {

                    }
                    else
                    {
                        return BadRequest(new ErrorResponse { Message = "知识库不存在" });
                    }

                }
                else
                {
                    // 后台处理简单聊天
                    var aiMessages = ConvertToAiMessages(messages);
                    _ = ProcessSimpleChatAsync(messageId, conversationId, userId, aiMessages, request);
                }

                return Ok(new ChatStreamResponse { MessageId = messageId, ConversationId = conversationId });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 聊天请求处理失败");
                return BadRequest(new ErrorResponse { Message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 上传临时文件（用于聊天的附件）
        /// </summary>
        [HttpPost("upload", Name = "上传临时文件")]
        [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadTemporaryFile([FromForm] UploadFileRequest request)
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new ErrorResponse { Message = "文件不能为空" });
            }

            var tenantId = GetTenantId();
            var userId = GetUserId();
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
            var contentType = GetContentType(request.File.FileName);

            try
            {
                var objectKey = $"temp/{tenantId}/{sessionId}/{Guid.CreateVersion7()}/{request.File.FileName}";

                logger.LogInformation("[Chat] 上传临时文件: {FileName}, Size: {Size}", request.File.FileName, request.File.Length);

                await objectStorage.PutAsync(objectKey, request.File.OpenReadStream(), contentType, ct: Response.HttpContext.RequestAborted);

                var document = new Document
                {
                    Id = Guid.CreateVersion7().ToString(),
                    TenantId = tenantId,
                    KnowledgeBaseId = null,
                    FolderId = null,
                    WorkspaceId = null,
                    Title = request.File.FileName,
                    ContentType = contentType,
                    SourceType = SourceType.Upload,
                    SourceUri = null,
                    ObjectKey = objectKey,
                    FileHash = null,
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
                        TenantId = tenantId,
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
        [HttpGet("files/{documentId}", Name = "获取临时文件")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTemporaryFile(string documentId)
        {
            var tenantId = GetTenantId();
            var userId = GetUserId();

            var document = await dbContext.Documents
                .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenantId && d.CreatedByUserId == userId);

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
            try
            {
                var options = BuildChatOptions(request);
                options.ModelId = request.Model;
                var fullContent = string.Empty;
                var sb = new System.Text.StringBuilder();
                await foreach (var update in chatClient.CompleteStreamingAsync(aiMessages, options))
                {
                    if (!string.IsNullOrEmpty(update.Text))
                    {
                        sb.Append(update.Text);
                        await SendStreamingChunkAsync(userId, conversationId, messageId, update.Text, isComplete: false);
                    }
                }
                await SendStreamingChunkAsync(userId, conversationId, messageId, fullContent, isComplete: true);
                logger.LogInformation("[Chat] 简单聊天完成: MessageId={MessageId}", messageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] 简单聊天处理失败: MessageId={MessageId}", messageId);
                await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);
            }
        }

        /// <summary>
        /// 处理 RAG 聊天（使用文档检索）
        /// </summary>
        private async Task ProcessRagChatAsync(
            string messageId,
            string conversationId,
            string tenantId,
            string userId,
            KnowledgeBase knowledgeBase,
            ChatRequest request,
            List<OmniMind.Contracts.Chat.ChatMessage> baseMessages)
        {
            try
            {
                // 1. 生成查询向量并检索相关文档
                logger.LogInformation("[Chat] 正在检索相关文档: MessageId={MessageId}", messageId);
                var context = await RetrieveRelevantContextAsync(tenantId, knowledgeBase.Id, request.Message, request.TopK);

                if (string.IsNullOrEmpty(context))
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, "知识库中没有找到相关文档", isComplete: true);
                    logger.LogWarning("[Chat] 未找到相关文档: MessageId={MessageId}", messageId);
                    return;
                }

                // 2. 构建带上下文的消息列表
                var messages = BuildRagMessages(context, baseMessages);

                // 3. 转换为 AI 消息并调用 LLM
                var aiMessages = ConvertToAiMessages(messages);
                var options = BuildChatOptions(request);
                var fullContent = string.Empty;

                logger.LogInformation("[Chat] 正在调用 LLM 生成回复: MessageId={MessageId}", messageId);

                await foreach (var update in chatClient.CompleteStreamingAsync(aiMessages, options))
                {
                    if (update.Text != null)
                    {
                        fullContent += update.Text;
                        await SendStreamingChunkAsync(userId, conversationId, messageId, fullContent, isComplete: false);
                    }
                }

                await SendStreamingChunkAsync(userId, conversationId, messageId, fullContent, isComplete: true);
                logger.LogInformation("[Chat] RAG 聊天完成: MessageId={MessageId}", messageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Chat] RAG 聊天处理失败: MessageId={MessageId}", messageId);
                await SendStreamingChunkAsync(userId, conversationId, messageId, string.Empty, isComplete: true);
            }
        }

        /// <summary>
        /// 检索相关上下文
        /// </summary>
        private async Task<string?> RetrieveRelevantContextAsync(string tenantId, string knowledgeBaseId, string query, int topK)
        {
            var queryEmbedding = await embeddingGenerator.GenerateAsync(new[] { query });
            var queryVector = queryEmbedding.First().Vector.ToArray();

            var collectionName = QdrantHttpVectorStore.GenerateTenantCollectionName(tenantId, knowledgeBaseId);
            var searchResults = await vectorStore.SearchAsync(collectionName, queryVector, new VectorSearchOptions(
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

            logger.LogInformation("[Chat] 检索到 {Count} 个相关文档片段", contextParts.Count);
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

        #endregion
    }
}
