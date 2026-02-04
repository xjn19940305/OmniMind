using OmniMind.Api.Swaggers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OmniMind.Abstractions.SignalR;
using OmniMind.Abstractions.Storage;
using OmniMind.Contracts.Chat;
using OmniMind.Contracts.Common;
using OmniMind.Entities;
using OmniMind.Enums;
using OmniMind.Persistence.PostgreSql;

namespace App.Controllers
{
    /// <summary>
    /// 测试控制器
    /// </summary>
    [ApiGroup(ApiGroupNames.USER)]
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : BaseController
    {
        private readonly IObjectStorage storage;
        private readonly IServiceScopeFactory serviceScopeFactory;
        private readonly IChatClient chatClient;
        private readonly IRealtimeNotifier realtimeNotifier;
        private readonly ILogger<TestController> logger;

        public TestController(
            IObjectStorage storage,
            IServiceScopeFactory serviceScopeFactory,
            IChatClient chatClient,
            IRealtimeNotifier realtimeNotifier,
            ILogger<TestController> logger)
        {
            this.storage = storage;
            this.serviceScopeFactory = serviceScopeFactory;
            this.chatClient = chatClient;
            this.realtimeNotifier = realtimeNotifier;
            this.logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload()
        {
            //await storage.PutAsync("11",);
            return Ok();
        }

        /// <summary>
        /// 生成文档总结
        /// </summary>
        [HttpPost("generate-summary")]
        [ProducesResponseType(typeof(GenerateSummaryResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GenerateSummary([FromBody] GenerateSummaryRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentId))
            {
                return BadRequest(new ErrorResponse { Message = "文档ID不能为空" });
            }

            var userId = GetUserId();
            var messageId = Guid.NewGuid().ToString();
            var conversationId = Guid.NewGuid().ToString();

            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            try
            {
                // 1. 验证文档存在且属于该用户
                var document = await dbContext.Documents
                    .FirstOrDefaultAsync(d => d.Id == request.DocumentId && d.CreatedByUserId == userId);

                if (document == null)
                {
                    return BadRequest(new ErrorResponse { Message = "文档不存在" });
                }

                // 2. 检查文档状态
                if (document.Status != DocumentStatus.Indexed)
                {
                    return BadRequest(new ErrorResponse { Message = $"文档尚未完成处理，当前状态：{document.Status}" });
                }

                // 3. 后台处理总结生成
                _ = Task.Run(() => ProcessSummaryAsync(messageId, conversationId, userId, document, request.SessionId));

                return Ok(new GenerateSummaryResponse
                {
                    MessageId = messageId,
                    ConversationId = conversationId
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Test] 生成文档总结失败: DocumentId={DocumentId}", request.DocumentId);
                return BadRequest(new ErrorResponse { Message = "服务器内部错误" });
            }
        }

        /// <summary>
        /// 处理总结生成（后台任务）
        /// </summary>
        private async Task ProcessSummaryAsync(
         string messageId,
         string conversationId,
         string userId,
         Document document,
         string? sessionId,
         CancellationToken cancellationToken = default)
        {
            using var scope = serviceScopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<OmniMindDbContext>();

            try
            {
                var systemPrompt = """
                你是一名【企业级内容分析与结构化会议总结专家】，专门处理【音频/视频转写文本】（会议、访谈、培训、讨论、业务沟通、经验分享等）。

                你的任务是：基于提供的转写文本切片，生成一份【高质量、结构清晰、可直接阅读与分发的会议总结】，风格参考【飞书会议纪要 + 企业内部分析总结】。

                ━━━━━━━━━━━━━━
                【总体目标】
                输出应让未参会人员快速理解：
                1. 为什么召开此次讨论
                2. 当前面临的核心问题
                3. 达成了哪些决策与共识
                4. 后续谁负责做什么
                5. 项目接下来如何推进

                ━━━━━━━━━━━━━━
                【核心原则】
                1. 严格忠于原始内容，不编造、不臆测、不补充未出现信息
                2. 自动过滤口语废话、重复表达、寒暄内容
                3. 将分散讨论自动合并为清晰结构
                4. 对重复讨论内容进行合并去重
                5. 不遗漏关键内容：决策、问题、方案、待办事项
                6. 优先提取会议中真正推进项目的信息

                ━━━━━━━━━━━━━━
                【内容优先级】
                ⭐⭐⭐ 已达成的决策、行动项、责任分工、时间节点
                ⭐⭐ 问题争议、解决方案、风险与建议
                ⭐ 项目进展、背景说明、现状信息
                ☆ 闲聊、寒暄、重复表达（自动忽略）

                ━━━━━━━━━━━━━━
                【输出结构规范】
                必须使用 Markdown 层级结构，并遵循以下逻辑顺序：

                # 会议总结标题（自动生成）

                ## 一、核心结论概览（必须输出）
                用 3~6 条总结本次讨论最终形成的关键共识或推进结果。

                ## 二、关键问题与讨论内容
                按议题或模块分类整理问题与讨论重点。

                ## 三、决策与解决方案
                集中列出已达成共识的解决方案或决定。

                ## 四、人员分工与任务调整（如存在）
                整理人员职责变化与模块责任划分。

                ## 五、行动项与后续安排（重要）
                将会议中出现的待办事项整理为可执行条目。

                ## 六、项目当前目标与推进方向
                总结当前阶段目标与下一步推进重点。

                ━━━━━━━━━━━━━━
                【内容编写规范】
                1. 使用 Markdown，保证前端可直接渲染
                2. 优先使用列表组织内容
                3. 每条信息应为清晰可读的信息点
                4. 关键决策或共识用 **加粗** 强调
                5. 不确定信息需标注：[待确认] [讨论中] [待跟进] [需补充]

                ━━━━━━━━━━━━━━
                【行动项输出规范】
                - 事项内容
                - 责任人（若出现）
                - 时间节点（若出现）
                - 备注或依赖条件（若存在）
                若责任人或时间未出现，不得编造，可标注：[责任人未明确] / [时间未确定]

                ━━━━━━━━━━━━━━
                【去重与压缩规则】
                同一问题多次出现：合并为一个条目，不重复输出，保留最终结论。

                ━━━━━━━━━━━━━━
                【重要约束】
                只输出最终结构化会议总结内容，不输出思考过程/推理过程/解释/示例。

                ━━━━━━━━━━━━━━
                现在开始基于提供的转写文本生成总结：
                """;

                //var systemPrompt = "作为一名专业内容总结专家，请帮助我将视频、音频转写的文字内容进行结构化总结，首先提供整体概述，然后详细说明每个模块的功能点，清楚列出主要类别和子类别，使用户能够直观理解内容的结构和重点";
                var chunks = await dbContext.Chunks
                    .Where(c => c.DocumentId == document.Id)
                    .OrderBy(c => c.ChunkIndex)
                    .Select(c => new { c.ChunkIndex, c.Content })
                    .ToListAsync(cancellationToken);

                chunks = chunks.Where(x => !string.IsNullOrWhiteSpace(x.Content)).ToList();

                if (chunks.Count == 0)
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, "文档内容为空，无法生成总结", true);
                    return;
                }

                var messages = new List<Microsoft.Extensions.AI.ChatMessage>(2 + chunks.Count)
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User,
    "任务：请基于下列文档切片生成结构化会议总结（Markdown）。" +
    "输出必须先给出【核心结论概览】，再展开后续章节，不得先输出问题或过程内容。" +
    "要求：合并去重；不得编造；材料缺失需标注；" +
    "并在关键要点或结论后标注引用 片段 编号，例如【片段-003】。")
                };
                foreach (var c in chunks)
                {
                    messages.Add(new(ChatRole.User, $"[片段-{c.ChunkIndex:000}]\n{c.Content}"));
                }

                var options = new ChatOptions
                {
                    ModelId = "deepseek-v3.2",
                    AdditionalProperties = new AdditionalPropertiesDictionary()
                };

                // ✅ 只有你真的想开思考增强时再开（否则更稳定）
                // options.AdditionalProperties["enable_thinking"] = true;
                options.AdditionalProperties["temperature"] = 0f;
                options.AdditionalProperties["top_p"] = 1f;
                var sb = new System.Text.StringBuilder(4096);

                var minInterval = TimeSpan.FromMilliseconds(150);
                var minCharsDelta = 40;
                var lastSendAt = DateTime.UtcNow;
                var lastRawLen = 0;

                // 用“长度 + 尾部指纹”做去重（稳定、便宜）
                int lastSentLen = 0;
                string lastTail = "";

                await foreach (var update in chatClient.CompleteStreamingAsync(messages, options, cancellationToken))
                {
                    if (string.IsNullOrEmpty(update.Text)) continue;

                    sb.Append(update.Text);

                    var now = DateTime.UtcNow;
                    var shouldSend = (now - lastSendAt) >= minInterval || (sb.Length - lastRawLen) >= minCharsDelta;
                    if (!shouldSend) continue;

                    var full = CleanExtraNewlines(sb.ToString());

                    var tail = full.Length <= 120 ? full : full[^120..];

                    if (full.Length == lastSentLen && tail == lastTail)
                        continue;

                    lastSentLen = full.Length;
                    lastTail = tail;

                    await SendStreamingChunkAsync(userId, conversationId, messageId, full, false);

                    lastSendAt = now;
                    lastRawLen = sb.Length;
                }

                var finalContent = CleanExtraNewlines(sb.ToString());

                // final 如果没变化：你可以只发完成信号（content 为空）
                if (finalContent.Length == lastSentLen && (finalContent.Length <= 120 ? finalContent : finalContent[^120..]) == lastTail)
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, finalContent, true);
                    // 如果你前端允许 content 为空：
                    // await SendStreamingChunkAsync(userId, conversationId, messageId, "", true);
                }
                else
                {
                    await SendStreamingChunkAsync(userId, conversationId, messageId, finalContent, true);
                }
            }
            catch (OperationCanceledException)
            {
                await SendStreamingChunkAsync(userId, conversationId, messageId, "已取消生成总结", true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Summary] 生成总结失败: DocumentId={DocumentId}", document.Id);
                await SendStreamingChunkAsync(userId, conversationId, messageId, "服务异常：总结生成失败", true);
            }
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
    }

    #region 请求/响应模型

    /// <summary>
    /// 生成总结请求
    /// </summary>
    public class GenerateSummaryRequest
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// 会话ID（可选，用于关联到现有会话）
        /// </summary>
        public string? SessionId { get; set; }
    }

    /// <summary>
    /// 生成总结响应
    /// </summary>
    public class GenerateSummaryResponse
    {
        /// <summary>
        /// 消息ID（用于跟踪流式响应）
        /// </summary>
        public string MessageId { get; set; } = string.Empty;

        /// <summary>
        /// 会话ID
        /// </summary>
        public string ConversationId { get; set; } = string.Empty;
    }

    #endregion
}
