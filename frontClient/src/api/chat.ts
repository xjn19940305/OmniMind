import request from '../utils/request'
import type { ChatMessage, ChatSession, Attachment } from '../types'

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
 * 上传文件（支持 sessionId 和 fileHash）
 */
export function uploadFile(file: File, sessionId?: string, fileHash?: string) {
  const formData = new FormData()
  formData.append('File', file)
  if (sessionId) {
    formData.append('SessionId', sessionId)
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
