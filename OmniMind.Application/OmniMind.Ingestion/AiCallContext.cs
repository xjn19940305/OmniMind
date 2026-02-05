using System.Diagnostics.CodeAnalysis;

namespace OmniMind.Ingestion
{
    /// <summary>
    /// AI 调用上下文，用于传递用户ID、会话ID等信息
    /// 并发安全：使用 AsyncLocal + Stack，支持嵌套调用和并行调用
    /// </summary>
    public static class AiCallContext
    {
        private static readonly AsyncLocal<Stack<AiCallContextScope>> _scopeStack = new();

        /// <summary>
        /// 获取当前上下文（栈顶的上下文，最近创建的）
        /// </summary>
        public static AiCallContextScope? Current
        {
            get
            {
                var stack = _scopeStack.Value;
                return stack?.Count > 0 ? stack.Peek() : null;
            }
        }

        /// <summary>
        /// 创建并推入一个新的上下文作用域（便捷方法）
        /// </summary>
        public static AiCallContextScope BeginScope(
            string userId,
            string? sessionId = null,
            string? documentId = null,
            string? knowledgeBaseId = null)
        {
            return new AiCallContextScope(userId, sessionId, documentId, knowledgeBaseId);
        }

        /// <summary>
        /// 将作用域推入栈中（内部使用）
        /// </summary>
        internal static void PushScope(AiCallContextScope scope)
        {
            // 确保 Stack 存在
            if (_scopeStack.Value == null)
            {
                _scopeStack.Value = new Stack<AiCallContextScope>();
            }

            // 推入栈中
            _scopeStack.Value.Push(scope);
        }

        /// <summary>
        /// 从栈中弹出当前作用域
        /// </summary>
        internal static void PopScope(AiCallContextScope scope)
        {
            var stack = _scopeStack.Value;
            if (stack == null || stack.Count == 0)
                return;

            while (stack.Count > 0)
            {
                var top = stack.Pop();
                if (ReferenceEquals(top, scope))
                    break;
            }
        }
    }

    /// <summary>
    /// AI 调用上下文作用域
    /// 使用 using 语句确保自动清理
    /// </summary>
    public sealed class AiCallContextScope : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// 用户ID（必填）
        /// </summary>
        public required string UserId { get; init; }

        /// <summary>
        /// 会话ID（可选）
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// 文档ID（可选）
        /// </summary>
        public string? DocumentId { get; set; }

        /// <summary>
        /// 知识库ID（可选）
        /// </summary>
        public string? KnowledgeBaseId { get; set; }

        [SetsRequiredMembers]
        public AiCallContextScope(
            string userId,
            string? sessionId = null,
            string? documentId = null,
            string? knowledgeBaseId = null)
        {
            UserId = userId;
            SessionId = sessionId;
            DocumentId = documentId;
            KnowledgeBaseId = knowledgeBaseId;

            // 自动推入栈
            AiCallContext.PushScope(this);
        }

        /// <summary>
        /// 设置会话ID
        /// </summary>
        public AiCallContextScope WithSession(string? sessionId)
        {
            SessionId = sessionId;
            return this;
        }

        /// <summary>
        /// 设置文档ID
        /// </summary>
        public AiCallContextScope WithDocument(string? documentId)
        {
            DocumentId = documentId;
            return this;
        }

        /// <summary>
        /// 设置知识库ID
        /// </summary>
        public AiCallContextScope WithKnowledgeBase(string? knowledgeBaseId)
        {
            KnowledgeBaseId = knowledgeBaseId;
            return this;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 从栈中弹出
            AiCallContext.PopScope(this);
        }
    }
}
