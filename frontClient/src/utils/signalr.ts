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

/**
 * 初始化 SignalR 连接
 */
export async function initSignalR(userId: string) {
  if (connection) {
    return connection
  }

  const token = localStorage.getItem('token')
  const baseUrl = import.meta.env.VITE_API_BASE_URL || ''

  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/hubs/ingestion?userId=${userId}`, {
      accessTokenFactory: () => token || '',
      skipNegotiation: false,
      withCredentials: true
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        // 指数退避重连策略
        if (retryContext.previousRetryCount === 0) {
          return 0
        }
        return Math.min(retryDelay * Math.pow(2, retryContext.previousRetryCount), 30000)
      }
    })
    .configureLogging(signalR.LogLevel.Information)
    .build()

  try {
    await connection.start()
    console.log('[SignalR] 连接成功')
  } catch (error) {
    console.error('[SignalR] 连接失败:', error)
    connection = null
    throw error
  }

  // 监听连接关闭事件
  connection.onclose(async () => {
    console.log('[SignalR] 连接已关闭')
    connection = null
  })

  // 监听重连事件
  connection.onreconnecting(() => {
    console.log('[SignalR] 正在重连...')
  })

  connection.onreconnected(() => {
    console.log('[SignalR] 重连成功')
  })

  return connection
}

/**
 * 停止 SignalR 连接
 */
export async function stopSignalR() {
  if (connection) {
    try {
      await connection.stop()
      console.log('[SignalR] 连接已停止')
    } catch (error) {
      console.error('[SignalR] 停止连接失败:', error)
    }
    connection = null
  }
}

/**
 * 获取当前连接
 */
export function getConnection(): signalR.HubConnection | null {
  return connection
}

/**
 * 监听文档进度
 */
export function onDocumentProgress(callback: (progress: DocumentProgress) => void) {
  if (!connection) {
    console.warn('[SignalR] 连接未建立，无法监听文档进度')
    return
  }

  connection.on('DocumentProgress', (progress: DocumentProgress) => {
    console.log('[SignalR] 文档进度:', progress)
    callback(progress)
  })
}

/**
 * 取消监听文档进度
 */
export function offDocumentProgress() {
  if (!connection) return
  connection.off('DocumentProgress')
}

/**
 * 监听聊天消息
 */
export function onChatMessage(callback: (data: { conversationId: string; message: SignalRMessage }) => void) {
  if (!connection) {
    console.warn('[SignalR] 连接未建立，无法监听聊天消息')
    return
  }

  connection.on('ChatMessage', (data: { conversationId: string; message: SignalRMessage }) => {
    console.log('[SignalR] 聊天消息:', data)
    callback(data)
  })
}

/**
 * 取消监听聊天消息
 */
export function offChatMessage() {
  if (!connection) return
  connection.off('ChatMessage')
}

/**
 * 检查连接状态
 */
export function isConnected(): boolean {
  return connection?.state === signalR.HubConnectionState.Connected
}
