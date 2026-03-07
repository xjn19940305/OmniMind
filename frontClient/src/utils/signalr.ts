import * as signalR from '@microsoft/signalr'

let connection: signalR.HubConnection | null = null
const reconnectDelay = 3000

export interface SignalRMessage {
  messageId: string
  role: string
  content: string
  isComplete: boolean
  timestamp: string
}

export interface DocumentProgress {
  documentId: string
  title: string
  status: string
  progress: number
  stage: string
  error?: string
  timestamp: string
}

export async function initSignalR() {
  if (connection) {
    return connection
  }

  const token = localStorage.getItem('token')
  const baseUrl = import.meta.env.VITE_API_BASE_URL || ''

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/hubs/ingestion`, {
      accessTokenFactory: () => token || '',
      skipNegotiation: false,
      withCredentials: true
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.previousRetryCount === 0) {
          return 0
        }

        return Math.min(reconnectDelay * Math.pow(2, retryContext.previousRetryCount), 30000)
      }
    })
    .configureLogging(signalR.LogLevel.Information)
    .build()

  connection.onclose(() => {
    connection = null
  })

  await connection.start()
  return connection
}

export async function stopSignalR() {
  if (!connection) {
    return
  }

  try {
    await connection.stop()
  } finally {
    connection = null
  }
}

export function getConnection(): signalR.HubConnection | null {
  return connection
}

export function onDocumentProgress(callback: (progress: DocumentProgress) => void) {
  if (!connection) {
    return
  }

  connection.off('DocumentProgress')
  connection.on('DocumentProgress', (progress: DocumentProgress) => {
    callback(progress)
  })
}

export function offDocumentProgress() {
  connection?.off('DocumentProgress')
}

export function onChatMessage(callback: (data: { conversationId: string; message: SignalRMessage }) => void) {
  if (!connection) {
    return
  }

  connection.off('ChatMessage')
  connection.on('ChatMessage', (...args: any[]) => {
    if (args.length >= 2) {
      callback({
        conversationId: args[0] as string,
        message: args[1] as SignalRMessage
      })
      return
    }

    if (args.length === 1 && typeof args[0] === 'object') {
      callback(args[0] as { conversationId: string; message: SignalRMessage })
    }
  })
}

export function offChatMessage() {
  connection?.off('ChatMessage')
}

export function isConnected(): boolean {
  return connection?.state === signalR.HubConnectionState.Connected
}
