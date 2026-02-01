import request from '../utils/request'
import type { ChatMessage, ChatSession, Attachment } from '../types'

export function sendMessage(sessionId: string, content: string, files?: Attachment[]) {
  return request<{
    messageId: string
    content: string
    sessionId: string
  }>({
    url: '/chat/message',
    method: 'post',
    data: { sessionId, content, files }
  })
}

export function sendMessageStream(
  sessionId: string,
  content: string,
  onMessage: (message: string) => void,
  onComplete: () => void,
  onError: (error: Error) => void,
  files?: Attachment[]
) {
  const token = localStorage.getItem('token')
  const baseUrl = import.meta.env.VITE_API_BASE_URL || '/api'

  return fetch(`${baseUrl}/chat/stream`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${token}`
    },
    body: JSON.stringify({ sessionId, content, files })
  }).then(async (response) => {
    if (!response.ok) {
      throw new Error('Network response was not ok')
    }

    const reader = response.body?.getReader()
    const decoder = new TextDecoder()

    if (!reader) {
      throw new Error('Response body is null')
    }

    try {
      while (true) {
        const { done, value } = await reader.read()

        if (done) {
          onComplete()
          break
        }

        const chunk = decoder.decode(value)
        const lines = chunk.split('\n')

        for (const line of lines) {
          if (line.startsWith('data: ')) {
            const data = line.slice(6)
            if (data === '[DONE]') {
              onComplete()
              return
            }
            try {
              const parsed = JSON.parse(data)
              onMessage(parsed.content || '')
            } catch (e) {
              // Ignore parsing errors for incomplete chunks
            }
          }
        }
      }
    } catch (error) {
      onError(error as Error)
    }
  })
}

export function getSessions() {
  return request<ChatSession[]>({
    url: '/chat/sessions',
    method: 'get'
  })
}

export function createSession(title?: string) {
  return request<ChatSession>({
    url: '/chat/sessions',
    method: 'post',
    data: { title }
  })
}

export function deleteSession(sessionId: string) {
  return request({
    url: `/chat/sessions/${sessionId}`,
    method: 'delete'
  })
}

export function getSessionMessages(sessionId: string) {
  return request<ChatMessage[]>({
    url: `/chat/sessions/${sessionId}/messages`,
    method: 'get'
  })
}

export function uploadFile(file: File) {
  const formData = new FormData()
  formData.append('file', file)

  return request<Attachment>({
    url: '/chat/upload',
    method: 'post',
    data: formData,
    headers: {
      'Content-Type': 'multipart/form-data'
    }
  })
}
