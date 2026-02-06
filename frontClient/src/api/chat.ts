import request from '../utils/request'
import type { ChatMessage, ChatSession, Attachment, Conversation, ConversationDetail, ConversationListResponse, ChatMessageDto } from '../types'

/**
 * 生成文档总结（流式输出）
 */
export function generateSummary(documentId: string, conversationId?: string) {
  return request<{
    messageId: string
    conversationId: string
  }>({
    url: '/api/Test/generate-summary',
    method: 'post',
    data: { documentId, sessionId: conversationId }
  })
}

/**
 * 统一聊天接口（通过 SignalR 流式响应）
 * 三种互斥模式：
 * 1. 纯AI对话 - 不带 knowledgeBaseId 和 documentId
 * 2. 知识库聊天 - 带 knowledgeBaseId
 * 3. 临时文件聊天 - 带 documentId
 * 返回 messageId 和 conversationId，实际内容通过 SignalR 推送
 */
export function chatStream(
  message: string,
  knowledgeBaseId?: string,
  documentId?: string,
  conversationId?: string,
  topK?: number,
  model?: string,
  history?: ChatMessage[]
) {
  return request<{
    messageId: string
    conversationId: string
  }>({
    url: '/api/Chat/chatStream',
    method: 'post',
    data: {
      sessionId: conversationId,
      message,
      knowledgeBaseId,
      documentId,
      topK,
      model,
      history
    }
  })
}

/**
 * 检查文件哈希是否存在（用于文件复用）
 */
export function checkFileHash(fileHash: string) {
  return request<Attachment | null>({
    url: '/api/Chat/check-file-hash',
    method: 'post',
    data: { fileHash }
  })
}

/**
 * 上传文件（支持 conversationId 和 fileHash）
 */
export function uploadFile(file: File, conversationId?: string, fileHash?: string) {
  const formData = new FormData()
  formData.append('File', file)
  if (conversationId) {
    formData.append('SessionId', conversationId)
  }
  if (fileHash) {
    formData.append('FileHash', fileHash)
  }

  return request<Attachment>({
    url: '/api/Chat/upload',
    method: 'post',
    data: formData,
    headers: {
      'Content-Type': 'multipart/form-data'
    }
  })
}

// ==================== 会话管理 API ====================

/**
 * 获取会话列表
 */
export function getConversations(params?: {
  page?: number
  pageSize?: number
  type?: string
}) {
  return request<ConversationListResponse>({
    url: '/api/Chat/conversations',
    method: 'get',
    params
  })
}

/**
 * 获取会话详情（包含消息列表）
 */
export function getConversation(conversationId: string) {
  return request<ConversationDetail>({
    url: `/api/Chat/conversations/${conversationId}`,
    method: 'get'
  })
}

/**
 * 更新会话标题
 */
export function updateConversationTitle(conversationId: string, title: string) {
  return request<Conversation>({
    url: `/api/Chat/conversations/${conversationId}/title`,
    method: 'put',
    data: { title }
  })
}

/**
 * 置顶/取消置顶会话
 */
export function toggleConversationPin(conversationId: string, isPinned: boolean) {
  return request<Conversation>({
    url: `/api/Chat/conversations/${conversationId}/pin`,
    method: 'put',
    data: { isPinned }
  })
}

/**
 * 删除会话
 */
export function deleteConversation(conversationId: string) {
  return request({
    url: `/api/Chat/conversations/${conversationId}`,
    method: 'delete'
  })
}

/**
 * 取消流式消息生成
 */
export function cancelStreamingMessage(messageId: string) {
  return request({
    url: `/api/Chat/cancel/${messageId}`,
    method: 'post'
  })
}

// ==================== 旧 API（兼容） ====================

/**
 * @deprecated 使用 getConversations 替代
 */
export function getSessions() {
  return request<ChatSession[]>({
    url: '/chat/sessions',
    method: 'get'
  })
}

/**
 * @deprecated 后端已自动创建会话
 */
export function createSession(title?: string) {
  return request<ChatSession>({
    url: '/chat/sessions',
    method: 'post',
    data: { title }
  })
}

/**
 * @deprecated 使用 deleteConversation 替代
 */
export function deleteSession(sessionId: string) {
  return request({
    url: `/chat/sessions/${sessionId}`,
    method: 'delete'
  })
}

/**
 * @deprecated 使用 getConversation 替代
 */
export function getSessionMessages(sessionId: string) {
  return request<ChatMessage[]>({
    url: `/chat/sessions/${sessionId}/messages`,
    method: 'get'
  })
}
