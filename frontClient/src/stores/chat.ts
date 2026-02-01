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
      session.messages.push(message)
      session.updatedAt = new Date().toISOString()
    }
  }

  function updateMessage(sessionId: string, messageIndex: number, content: string) {
    const session = sessions.value.find(s => s.id === sessionId)
    if (session && session.messages[messageIndex]) {
      session.messages[messageIndex].content = content
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
