import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { ChatMessage, ChatSession } from '../types'

export const useChatStore = defineStore('chat', () => {
  const sessions = ref<ChatSession[]>([])
  const currentSessionId = ref<string | null>(null)
  const isStreaming = ref(false)

  const currentSession = computed(() =>
    sessions.value.find(s => s.id === currentSessionId.value)
  )

  function createSession(title?: string) {
    const newSession: ChatSession = {
      id: Date.now().toString(),
      title: title || '新对话',
      messages: [],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString()
    }
    sessions.value.unshift(newSession)
    currentSessionId.value = newSession.id
    return newSession
  }

  function addMessage(sessionId: string, message: ChatMessage) {
    const session = sessions.value.find(s => s.id === sessionId)
    if (session) {
      // 使用 push 触发响应式
      session.messages.push(message)
      session.updatedAt = new Date().toISOString()
      console.log('[Chat Store] 添加消息后，消息数量:', session.messages.length)
    }
  }

  function updateMessage(sessionId: string, messageIndex: number, content: string) {
    const session = sessions.value.find(s => s.id === sessionId)
    if (session && session.messages[messageIndex]) {
      // 创建新对象来确保 Vue 3 响应式更新
      session.messages[messageIndex] = {
        ...session.messages[messageIndex],
        content
      }
      console.log('[Chat Store] 更新消息:', { sessionId, messageIndex, contentLength: content.length })
    } else {
      console.warn('[Chat Store] 更新消息失败: session=', session, 'messageIndex=', messageIndex)
    }
  }

  function deleteSession(sessionId: string) {
    const index = sessions.value.findIndex(s => s.id === sessionId)
    if (index > -1) {
      sessions.value.splice(index, 1)
      if (currentSessionId.value === sessionId) {
        currentSessionId.value = sessions.value[0]?.id || null
      }
    }
  }

  function clearSessions() {
    sessions.value = []
    currentSessionId.value = null
  }

  return {
    sessions,
    currentSessionId,
    currentSession,
    isStreaming,
    createSession,
    addMessage,
    updateMessage,
    deleteSession,
    clearSessions
  }
})
