<template>
  <div class="chat-container">
    <div class="chat-sidebar desktop-only">
      <div class="sidebar-header">
        <el-button type="primary" @click="handleNewChat">
          <el-icon><Plus /></el-icon>
          新对话
        </el-button>
      </div>

      <div class="session-list">
        <div
          v-for="session in sessions"
          :key="session.id"
          class="session-item"
          :class="{ active: session.id === currentSessionId }"
          @click="handleSelectSession(session.id)"
        >
          <div class="session-title">{{ session.title }}</div>
          <el-icon class="delete-icon" @click.stop="handleDeleteSession(session.id)">
            <Delete />
          </el-icon>
        </div>
      </div>
    </div>

    <div class="chat-main">
      <!-- Mobile Header -->
      <div class="chat-header mobile-only">
        <el-button text @click="showMobileSessions = true">
          <el-icon size="20"><Menu /></el-icon>
        </el-button>
        <span class="header-title">AI 对话</span>
        <el-button text @click="handleNewChat">
          <el-icon size="20"><Plus /></el-icon>
        </el-button>
      </div>

      <!-- Messages -->
      <div class="messages-container" ref="messagesContainer">
        <div v-if="currentMessages.length === 0" class="empty-state">
          <el-icon size="80" color="#909399"><ChatDotRound /></el-icon>
          <p>开始新的对话</p>
        </div>

        <div v-else class="messages">
          <div
            v-for="(message, index) in currentMessages"
            :key="message.id"
            class="message"
            :class="message.role"
          >
            <div class="message-avatar">
              <el-avatar v-if="message.role === 'user'" :size="36">
                <el-icon><User /></el-icon>
              </el-avatar>
              <el-avatar v-else :size="36">
                <el-icon><Service /></el-icon>
              </el-avatar>
            </div>
            <div class="message-content">
              <div v-if="message.files && message.files.length > 0" class="message-files">
                <el-tag
                  v-for="file in message.files"
                  :key="file.id"
                  class="file-tag"
                  @click="handlePreviewFile(file)"
                >
                  <el-icon><Document /></el-icon>
                  {{ file.name }}
                </el-tag>
              </div>
              <div class="message-text" v-html="renderMessage(message.content)"></div>
              <div class="message-time">{{ formatTime(message.timestamp) }}</div>
            </div>
          </div>

          <!-- Streaming indicator -->
          <div v-if="isStreaming" class="message assistant streaming">
            <div class="message-avatar">
              <el-avatar :size="36">
                <el-icon><Service /></el-icon>
              </el-avatar>
            </div>
            <div class="message-content">
              <div class="typing-indicator">
                <span></span>
                <span></span>
                <span></span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Input Area -->
      <div class="input-area">
        <div v-if="selectedFiles.length > 0" class="selected-files">
          <el-tag
            v-for="file in selectedFiles"
            :key="file.id"
            closable
            @close="handleRemoveFile(file.id)"
          >
            <el-icon><Document /></el-icon>
            {{ file.name }}
          </el-tag>
        </div>

        <div class="input-box">
          <el-input
            v-model="inputMessage"
            type="textarea"
            :autosize="{ minRows: 1, maxRows: 6 }"
            placeholder="输入消息，支持上传文档、图片等..."
            @keydown.enter.prevent="handleEnter"
          />
          <div class="input-actions">
            <el-upload
              ref="uploadRef"
              :auto-upload="false"
              :show-file-list="false"
              :on-change="handleFileChange"
              multiple
              accept=".pdf,.doc,.docx,.ppt,.pptx,.md,.txt,.jpg,.jpeg,.png,.gif"
            >
              <el-button text>
                <el-icon><Plus /></el-icon>
                上传文件
              </el-button>
            </el-upload>
            <el-button
              type="primary"
              :loading="isStreaming"
              :disabled="!inputMessage.trim() && selectedFiles.length === 0"
              @click="handleSend"
            >
              <el-icon><Promotion /></el-icon>
              发送
            </el-button>
          </div>
        </div>
      </div>
    </div>

    <!-- Mobile Sessions Drawer -->
    <el-drawer v-model="showMobileSessions" direction="ltr" size="70%" class="mobile-only">
      <template #header>
        <span>对话历史</span>
      </template>
      <el-button type="primary" style="width: 100%; margin-bottom: 16px;" @click="handleNewChat">
        <el-icon><Plus /></el-icon>
        新对话
      </el-button>
      <div class="session-list">
        <div
          v-for="session in sessions"
          :key="session.id"
          class="session-item"
          :class="{ active: session.id === currentSessionId }"
          @click="handleSelectSession(session.id); showMobileSessions = false"
        >
          <div class="session-title">{{ session.title }}</div>
          <el-icon class="delete-icon" @click.stop="handleDeleteSession(session.id)">
            <Delete />
          </el-icon>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import type { UploadFile, UploadInstance, UploadUserFile } from 'element-plus'
import { marked } from 'marked'
import {
  Menu,
  Plus,
  User,
  Service,
  Document,
  Delete,
  ChatDotRound,
  Promotion
} from '@element-plus/icons-vue'
import { useChatStore } from '../stores/chat'
import { sendMessageStream, uploadFile, createSession } from '../api/chat'
import type { Attachment } from '../types'

const chatStore = useChatStore()

const inputMessage = ref('')
const messagesContainer = ref<HTMLElement>()
const uploadRef = ref<UploadInstance>()
const selectedFiles = ref<Attachment[]>([])
const showMobileSessions = ref(false)

const sessions = computed(() => chatStore.sessions)
const currentSessionId = computed(() => chatStore.currentSessionId)
const currentMessages = computed(() => chatStore.currentSession?.messages || [])
const isStreaming = computed(() => chatStore.isStreaming)

function formatTime(timestamp: string) {
  const date = new Date(timestamp)
  const now = new Date()
  const diff = now.getTime() - date.getTime()

  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  return date.toLocaleDateString()
}

function renderMessage(content: string) {
  return marked(content)
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
    }
  })
}

async function handleNewChat() {
  await createSession()
  chatStore.createSession()
  scrollToBottom()
}

function handleSelectSession(sessionId: string) {
  chatStore.currentSessionId = sessionId
  scrollToBottom()
}

async function handleDeleteSession(sessionId: string) {
  chatStore.deleteSession(sessionId)
  ElMessage.success('已删除对话')
}

async function handleFileChange(file: UploadFile) {
  if (!file.raw) return

  try {
    const uploaded = await uploadFile(file.raw)
    selectedFiles.value.push(uploaded)
    ElMessage.success(`已添加 ${file.name}`)
  } catch (error) {
    ElMessage.error('文件上传失败')
  }
}

function handleRemoveFile(fileId: string) {
  const index = selectedFiles.value.findIndex(f => f.id === fileId)
  if (index > -1) {
    selectedFiles.value.splice(index, 1)
  }
}

function handlePreviewFile(file: Attachment) {
  // TODO: Implement file preview
  ElMessage.info('文件预览功能开发中')
}

function handleEnter(event: KeyboardEvent) {
  if (!event.shiftKey) {
    handleSend()
  }
}

async function handleSend() {
  if (!inputMessage.value.trim() && selectedFiles.value.length === 0) return
  if (isStreaming.value) return

  // Create session if needed
  let sessionId = currentSessionId.value
  if (!sessionId) {
    const newSession = await createSession()
    chatStore.createSession(newSession.title)
    sessionId = newSession.id
  }

  const content = inputMessage.value.trim()
  const files = [...selectedFiles.value]

  // Add user message
  chatStore.addMessage(sessionId, {
    id: Date.now().toString(),
    role: 'user',
    content,
    timestamp: new Date().toISOString(),
    files
  })

  // Clear input
  inputMessage.value = ''
  selectedFiles.value = []

  // Add assistant message placeholder
  const assistantMessageId = `${Date.now()}_assistant`
  chatStore.addMessage(sessionId, {
    id: assistantMessageId,
    role: 'assistant',
    content: '',
    timestamp: new Date().toISOString()
  })

  const messageIndex = chatStore.currentSession?.messages.length - 1 || 0

  // Stream response
  chatStore.isStreaming = true
  scrollToBottom()

  try {
    let fullResponse = ''

    await sendMessageStream(
      sessionId,
      content,
      files,
      (chunk) => {
        fullResponse += chunk
        chatStore.updateMessage(sessionId, messageIndex, fullResponse)
        scrollToBottom()
      },
      () => {
        chatStore.isStreaming = false
      },
      (error) => {
        chatStore.isStreaming = false
        ElMessage.error('发送失败')
        console.error('Stream error:', error)
      }
    )
  } catch (error) {
    chatStore.isStreaming = false
    ElMessage.error('发送失败')
    console.error('Send error:', error)
  }
}

onMounted(() => {
  // Load sessions if needed
  scrollToBottom()
})
</script>

<style scoped lang="scss">
.chat-container {
  display: flex;
  height: 100%;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

// Sidebar
.chat-sidebar {
  width: 260px;
  border-right: 1px solid #e4e7ed;
  display: flex;
  flex-direction: column;

  .sidebar-header {
    padding: 16px;
    border-bottom: 1px solid #e4e7ed;

    .el-button {
      width: 100%;
    }
  }

  .session-list {
    flex: 1;
    overflow-y: auto;
    padding: 8px;
  }

  .session-item {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 12px;
    margin-bottom: 8px;
    border-radius: 8px;
    cursor: pointer;
    transition: background 0.2s;

    &:hover {
      background: #f5f7fa;

      .delete-icon {
        opacity: 1;
      }
    }

    &.active {
      background: #ecf5ff;
      color: #409eff;
    }

    .session-title {
      flex: 1;
      font-size: 14px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .delete-icon {
      opacity: 0;
      color: #909399;
      transition: opacity 0.2s;

      &:hover {
        color: #f56c6c;
      }
    }
  }
}

// Main
.chat-main {
  flex: 1;
  display: flex;
  flex-direction: column;
}

.chat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;

  .header-title {
    font-size: 16px;
    font-weight: 500;
  }
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 20px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #909399;

  p {
    margin-top: 16px;
    font-size: 14px;
  }
}

.messages {
  .message {
    display: flex;
    margin-bottom: 20px;

    &.user {
      flex-direction: row-reverse;

      .message-content {
        align-items: flex-end;
      }

      .message-text {
        background: #409eff;
        color: white;
      }
    }

    &.assistant {
      .message-text {
        background: #f5f7fa;
      }
    }

    &.streaming .message-text {
      background: #f5f7fa;
    }
  }

  .message-avatar {
    flex-shrink: 0;
    margin: 0 12px;
  }

  .message-content {
    display: flex;
    flex-direction: column;
    max-width: 70%;
  }

  .message-files {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-bottom: 8px;
  }

  .file-tag {
    cursor: pointer;
  }

  .message-text {
    padding: 12px 16px;
    border-radius: 12px;
    word-wrap: break-word;
    white-space: pre-wrap;
    line-height: 1.6;

    :deep(img) {
      max-width: 100%;
      border-radius: 8px;
    }

    :deep(pre) {
      background: #f5f7fa;
      padding: 12px;
      border-radius: 8px;
      overflow-x: auto;
    }

    :deep(code) {
      background: #f5f7fa;
      padding: 2px 6px;
      border-radius: 4px;
      font-family: 'Courier New', monospace;
    }
  }

  .message-time {
    margin-top: 4px;
    font-size: 12px;
    color: #909399;
  }
}

.typing-indicator {
  display: flex;
  gap: 4px;
  padding: 12px 16px;

  span {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: #409eff;
    animation: typing 1.4s infinite;
  }

  span:nth-child(2) {
    animation-delay: 0.2s;
  }

  span:nth-child(3) {
    animation-delay: 0.4s;
  }
}

@keyframes typing {
  0%, 60%, 100% {
    transform: translateY(0);
  }
  30% {
    transform: translateY(-10px);
  }
}

.input-area {
  border-top: 1px solid #e4e7ed;
  padding: 16px;
}

.selected-files {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
}

.input-box {
  display: flex;
  flex-direction: column;
  gap: 12px;

  .input-actions {
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
}

@media (max-width: 768px) {
  .message-content {
    max-width: 85%;
  }

  .messages-container {
    padding: 12px;
  }

  .input-area {
    padding: 12px;
  }
}
</style>
