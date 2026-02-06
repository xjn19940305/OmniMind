import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { ChatMessage, ChatSession, Conversation, ConversationDetail } from '../types'
import { getConversations, getConversation, deleteConversation as apiDeleteConversation } from '../api/chat'

export const useChatStore = defineStore('chat', () => {
  // 本地会话列表（兼容旧逻辑，逐步迁移到后端管理）
  const sessions = ref<ChatSession[]>([])
  const currentConversationId = ref<string | null>(null)
  const isStreaming = ref(false)

  // 后端会话列表
  const conversations = ref<Conversation[]>([])
  const currentConversationDetail = ref<ConversationDetail | null>(null)
  const isLoadingConversations = ref(false)

  const currentSession = computed(() =>
    sessions.value.find(s => s.id === currentConversationId.value)
  )

  const currentConversation = computed(() =>
    currentConversationDetail.value
  )

  const currentMessages = computed(() => {
    // 优先使用后端数据
    if (currentConversationDetail.value) {
      return currentConversationDetail.value.messages.map(m => ({
        id: m.id,
        role: m.role as 'user' | 'assistant' | 'system',
        content: m.content,
        timestamp: m.createdAt,
        status: m.status as any,
        error: m.error,
        completedAt: m.completedAt
      }))
    }
    // 回退到本地数据
    return currentSession.value?.messages || []
  })

  // 从后端加载会话列表
  async function loadConversations() {
    try {
      isLoadingConversations.value = true
      const response = await getConversations({ pageSize: 100 })
      conversations.value = response.conversations
    } catch (error) {
      console.error('加载会话列表失败:', error)
    } finally {
      isLoadingConversations.value = false
    }
  }

  // 从后端加载会话详情（包含消息）
  async function loadConversationDetail(conversationId: string) {
    try {
      const detail = await getConversation(conversationId)
      currentConversationDetail.value = detail
      currentConversationId.value = conversationId
      return detail
    } catch (error) {
      console.error('加载会话详情失败:', error)
      return null
    }
  }

  // 切换会话
  async function selectConversation(conversationId: string) {
    currentConversationId.value = conversationId
    await loadConversationDetail(conversationId)
  }

  // 新建会话（清空当前选择，由后端自动创建）
  function createNewConversation() {
    currentConversationId.value = null
    currentConversationDetail.value = null
  }

  // 删除会话
  async function deleteConversation(conversationId: string) {
    try {
      await apiDeleteConversation(conversationId)
      conversations.value = conversations.value.filter(c => c.id !== conversationId)

      if (currentConversationId.value === conversationId) {
        currentConversationId.value = null
        currentConversationDetail.value = null
      }
      return true
    } catch (error) {
      console.error('删除会话失败:', error)
      return false
    }
  }

  // 更新当前会话的流式消息（本地更新，不调用后端）
  function updateStreamingMessage(messageId: string, content: string, isComplete: boolean) {
    if (!currentConversationDetail.value) return

    const message = currentConversationDetail.value.messages.find(m => m.id === messageId)
    if (message) {
      if (isComplete) {
        // 完成时直接赋值完整内容
        message.content = content
        message.status = 'completed'
        message.completedAt = new Date().toISOString()
      } else {
        // 流式过程中追加内容
        message.content = (message.content || '') + content
        message.status = 'streaming'
      }
    } else {
      // 新消息
      currentConversationDetail.value.messages.push({
        id: messageId,
        role: 'assistant',
        content,
        status: isComplete ? 'completed' : 'streaming',
        createdAt: new Date().toISOString(),
        completedAt: isComplete ? new Date().toISOString() : undefined
      })
    }
  }

  // ==================== 旧方法（兼容） ====================

  function createSession(title?: string) {
    const newSession: ChatSession = {
      id: Date.now().toString(),
      title: title || '新对话',
      messages: [],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
    sessions.value.unshift(newSession)
    currentConversationId.value = newSession.id
    return newSession
  }

  function addMessage(conversationId: string, message: ChatMessage) {
    const session = sessions.value.find(s => s.id === conversationId)
    if (session) {
      session.messages.push(message)
      session.updatedAt = new Date().toISOString()
    }

    // 同时更新后端数据结构
    if (currentConversationDetail.value && currentConversationDetail.value.id === conversationId) {
      currentConversationDetail.value.messages.push({
        id: message.id,
        role: message.role,
        content: message.content,
        status: 'completed',
        createdAt: message.timestamp
      })
    }
  }

  function updateMessage(sessionId: string, messageIndex: number, content: string, isComplete: boolean = false) {
    const session = sessions.value.find(s => s.id === sessionId)
    if (session && session.messages[messageIndex]) {
      const existingMessage = session.messages[messageIndex]
      session.messages[messageIndex] = {
        ...existingMessage,
        // 完成时直接赋值，否则追加
        content: isComplete ? content : (existingMessage.content || '') + content,
        status: isComplete ? 'completed' : 'streaming',
        completedAt: isComplete ? new Date().toISOString() : existingMessage.completedAt
      }
    }
  }

  function deleteSession(sessionId: string) {
    const index = sessions.value.findIndex(s => s.id === sessionId)
    if (index > -1) {
      sessions.value.splice(index, 1)
      if (currentConversationId.value === sessionId) {
        currentConversationId.value = sessions.value[0]?.id || null
      }
    }
  }

  function clearSessions() {
    sessions.value = []
    currentConversationId.value = null
  }

  return {
    sessions,
    currentSessionId,
    currentSession,
    isStreaming,
    conversations,
    currentConversation,
    currentConversationDetail,
    currentMessages,
    isLoadingConversations,
    loadConversations,
    loadConversationDetail,
    selectConversation,
    createNewConversation,
    deleteConversation,
    updateStreamingMessage,
    createSession,
    addMessage,
    updateMessage,
    deleteSession,
    clearSessions
  }
})
