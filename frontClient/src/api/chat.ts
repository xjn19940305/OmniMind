import request from '../utils/request'
import type { ChatMessage, ChatSession, Attachment } from '../types'

/**
 * 统一聊天接口（通过 SignalR 流式响应）
 * 如果提供了 knowledgeBaseId 则使用 RAG 检索增强回答，否则直接调用模型
 * 返回 messageId 和 conversationId，实际内容通过 SignalR 推送
 */
export function chatStream(
  message: string,
  knowledgeBaseId?: string,
  sessionId?: string,
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
      sessionId,
      message,
      knowledgeBaseId,
      topK,
      model,
      history
    }
  })
}

/**
 * 上传文件（支持 sessionId）
 */
export function uploadFile(file: File, sessionId?: string) {
  const formData = new FormData()
  formData.append('File', file)
  if (sessionId) {
    formData.append('SessionId', sessionId)
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

/**
 * 获取会话列表
 */
export function getSessions() {
  return request<ChatSession[]>({
    url: '/chat/sessions',
    method: 'get'
  })
}

/**
 * 创建会话
 */
export function createSession(title?: string) {
  return request<ChatSession>({
    url: '/chat/sessions',
    method: 'post',
    data: { title }
  })
}

/**
 * 删除会话
 */
export function deleteSession(sessionId: string) {
  return request({
    url: `/chat/sessions/${sessionId}`,
    method: 'delete'
  })
}

/**
 * 获取会话消息
 */
export function getSessionMessages(sessionId: string) {
  return request<ChatMessage[]>({
    url: `/chat/sessions/${sessionId}/messages`,
    method: 'get'
  })
}
