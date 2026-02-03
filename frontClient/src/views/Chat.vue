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
          <el-icon
            class="delete-icon"
            @click.stop="handleDeleteSession(session.id)"
          >
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

      <!-- Knowledge Base Selector -->
      <div class="kb-selector">
        <el-select
          v-model="selectedKnowledgeBase"
          placeholder="选择知识库（可选）"
          clearable
          filterable
          style="width: 100%"
          @change="handleKnowledgeBaseChange"
        >
          <el-option
            v-for="kb in knowledgeBases"
            :key="kb.id"
            :label="kb.name"
            :value="kb.id"
          >
            <span>{{ kb.name }}</span>
            <span style="float: right; color: #8492a6; font-size: 12px">
              {{ kb.documentCount || 0 }} 文档
            </span>
          </el-option>
        </el-select>
        <el-tag
          v-if="selectedKnowledgeBase"
          type="success"
          size="small"
          style="margin-left: 8px"
        >
          已启用知识库检索
        </el-tag>
      </div>

      <!-- Connection Status -->
      <div v-if="!isSignalRConnected" class="connection-status">
        <el-icon><Warning /></el-icon>
        <span>实时连接断开，正在重连...</span>
      </div>

      <!-- Messages -->
      <div class="messages-container" ref="messagesContainer">
        <div v-if="currentMessages.length === 0" class="empty-state">
          <el-icon size="80" color="#909399"><ChatDotRound /></el-icon>
          <p>{{ selectedKnowledgeBase ? "基于知识库提问" : "开始新的对话" }}</p>
          <p v-if="selectedKnowledgeBase" class="hint">
            已选择知识库：{{ getKnowledgeBaseName(selectedKnowledgeBase) }}
          </p>
        </div>

        <div v-else class="messages">
          <div
            v-for="message in currentMessages"
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
              <div
                v-if="message.files && message.files.length > 0"
                class="message-files"
              >
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
              <div
                class="message-text"
                v-html="renderMessage(message.content)"
              ></div>
              <div class="message-time">
                {{ formatTime(message.timestamp) }}
              </div>
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
            :placeholder="
              selectedKnowledgeBase ? '基于知识库回答问题...' : '输入消息...'
            "
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
    <el-drawer
      v-model="showMobileSessions"
      direction="ltr"
      size="70%"
      class="mobile-only"
    >
      <template #header>
        <span>对话历史</span>
      </template>
      <el-button
        type="primary"
        style="width: 100%; margin-bottom: 16px"
        @click="handleNewChat"
      >
        <el-icon><Plus /></el-icon>
        新对话
      </el-button>
      <div class="session-list">
        <div
          v-for="session in sessions"
          :key="session.id"
          class="session-item"
          :class="{ active: session.id === currentSessionId }"
          @click="
            handleSelectSession(session.id);
            showMobileSessions = false;
          "
        >
          <div class="session-title">{{ session.title }}</div>
          <el-icon
            class="delete-icon"
            @click.stop="handleDeleteSession(session.id)"
          >
            <Delete />
          </el-icon>
        </div>
      </div>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, onUnmounted } from "vue";
import { ElMessage } from "element-plus";
import type { UploadFile, UploadInstance } from "element-plus";
import { marked } from "marked";
import {
  Menu,
  Plus,
  User,
  Service,
  Document,
  Delete,
  ChatDotRound,
  Promotion,
  Warning,
} from "@element-plus/icons-vue";
import { useChatStore } from "../stores/chat";
import { useUserStore } from "../stores/user";
import { chatStream, uploadFile, createSession } from "../api/chat";
import { getKnowledgeBases } from "../api/knowledge";
import {
  initSignalR,
  stopSignalR,
  onChatMessage,
  isConnected,
  type SignalRMessage,
} from "../utils/signalr";
import type { Attachment, ChatMessage as ApiChatMessage } from "../types";

const chatStore = useChatStore();
const userStore = useUserStore();

const inputMessage = ref("");
const messagesContainer = ref<HTMLElement>();
const uploadRef = ref<UploadInstance>();
const selectedFiles = ref<Attachment[]>([]);
const showMobileSessions = ref(false);
const isSignalRConnected = ref(false);
const selectedKnowledgeBase = ref<string>("");
const knowledgeBases = ref<any[]>([]);

const sessions = computed(() => chatStore.sessions);
const currentSessionId = computed(() => chatStore.currentSessionId);
const currentMessages = computed(
  () => chatStore.currentSession?.messages || [],
);
const isStreaming = computed(() => chatStore.isStreaming);

// 获取知识库名称
function getKnowledgeBaseName(id: string) {
  const kb = knowledgeBases.value.find((k) => k.id === id);
  return kb?.name || "未知知识库";
}

// 加载知识库列表
async function loadKnowledgeBases() {
  try {
    const data = await getKnowledgeBases({ pageSize: 100 });
    knowledgeBases.value = data?.items || [];
  } catch (error) {
    console.error("加载知识库失败:", error);
  }
}

// 知识库变化时记录日志
function handleKnowledgeBaseChange(value: string | undefined) {
  if (value) {
    console.log("[Chat] 已选择知识库:", getKnowledgeBaseName(value));
    ElMessage.info(`已切换到知识库：${getKnowledgeBaseName(value)}`);
  } else {
    console.log("[Chat] 已取消知识库，使用默认聊天");
  }
}

// 构建对话历史
function buildHistory(): ApiChatMessage[] {
  return currentMessages.value
    .filter((m) => m.role !== "system")
    .map((m) => ({
      role: m.role,
      content: m.content,
    }));
}

function formatTime(timestamp: string) {
  const date = new Date(timestamp);
  const now = new Date();
  const diff = now.getTime() - date.getTime();

  if (diff < 60000) return "刚刚";
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`;
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`;
  return date.toLocaleDateString();
}

function renderMessage(content: string) {
  return marked(content);
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    }
  });
}

async function handleNewChat() {
  await createSession();
  chatStore.createSession();
  selectedKnowledgeBase.value = "";
  scrollToBottom();
}

function handleSelectSession(sessionId: string) {
  chatStore.currentSessionId = sessionId;
  scrollToBottom();
}

async function handleDeleteSession(sessionId: string) {
  chatStore.deleteSession(sessionId);
  ElMessage.success("已删除对话");
}

async function handleFileChange(file: UploadFile) {
  if (!file.raw) return;

  try {
    const uploaded = await uploadFile(file.raw, currentSessionId.value);
    selectedFiles.value.push(uploaded);
    ElMessage.success(`已添加 ${file.name}`);
  } catch (error) {
    ElMessage.error("文件上传失败");
  }
}

function handleRemoveFile(fileId: string) {
  const index = selectedFiles.value.findIndex((f) => f.id === fileId);
  if (index > -1) {
    selectedFiles.value.splice(index, 1);
  }
}

function handlePreviewFile(file: Attachment) {
  ElMessage.info("文件预览功能开发中");
}

function handleEnter(event: KeyboardEvent) {
  if (!event.shiftKey) {
    handleSend();
  }
}

async function handleSend() {
  if (!inputMessage.value.trim() && selectedFiles.value.length === 0) return;
  if (isStreaming.value) return;

  // 创建会话
  // let sessionId = currentSessionId.value;
  // if (!sessionId) {
  //   const newSession = await createSession();
  // chatStore.createSession(newSession.title);
  //   sessionId = newSession.id;
  // }
  let sessionId = "1";
  const content = inputMessage.value.trim();
  const files = [...selectedFiles.value];

  // 添加用户消息
  chatStore.addMessage(sessionId, {
    id: Date.now().toString(),
    role: "user",
    content,
    timestamp: new Date().toISOString(),
    files,
  });

  // 清空输入
  inputMessage.value = "";
  selectedFiles.value = [];

  // 添加助手消息占位
  const assistantMessageId = `${Date.now()}_assistant`;
  chatStore.addMessage(sessionId, {
    id: assistantMessageId,
    role: "assistant",
    content: "",
    timestamp: new Date().toISOString(),
  });

  const messageIndex = chatStore.currentSession?.messages.length - 1 || 0;

  // 设置流式状态
  chatStore.isStreaming = true;
  scrollToBottom();

  try {
    const history = buildHistory();

    // 调用统一聊天接口
    const response = await chatStream(
      content,
      selectedKnowledgeBase.value, // 知识库ID（可选）
      sessionId,
      undefined, // topK 使用默认值
      "deepseek-v3.2", // model 使用默认值
      history,
    );

    // 存储消息映射
    pendingMessages.set(response.messageId, {
      sessionId,
      messageIndex,
      localMessageId: assistantMessageId,
    });

    console.log(
      "[Chat] 消息已发送:",
      response.messageId,
      selectedKnowledgeBase.value ? "(使用知识库)" : "(默认聊天)",
    );
  } catch (error: any) {
    chatStore.isStreaming = false;
    ElMessage.error(error.response?.data?.message || "发送失败");
    console.error("发送失败:", error);
  }
}

// SignalR 消息处理
const pendingMessages = new Map<
  string,
  {
    sessionId: string;
    messageIndex: number;
    localMessageId: string;
  }
>();

function handleSignalRChatMessage(data: {
  conversationId: string;
  message: SignalRMessage;
}) {
  const { conversationId, message } = data;

  const pending = pendingMessages.get(message.messageId);
  if (pending) {
    chatStore.updateMessage(
      pending.sessionId,
      pending.messageIndex,
      message.content,
    );
    scrollToBottom();

    if (message.isComplete) {
      chatStore.isStreaming = false;
      pendingMessages.delete(message.messageId);
      ElMessage.success("完成");
    }
  } else {
    chatStore.isStreaming = !message.isComplete;

    const existingMessage = currentMessages.value.find(
      (m) => m.id === message.messageId,
    );
    if (existingMessage) {
      existingMessage.content = message.content;
    } else {
      chatStore.addMessage(conversationId, {
        id: message.messageId,
        role: message.role as "user" | "assistant",
        content: message.content,
        timestamp: message.timestamp,
      });
    }
    scrollToBottom();
  }
}

// 初始化 SignalR
async function initializeSignalR() {
  try {
    const userId = userStore.userInfo?.id || userStore.tenantId;
    if (!userId) {
      console.warn("[Chat] 没有 user ID，无法连接 SignalR");
      return;
    }

    await initSignalR(userId);
    isSignalRConnected.value = true;
    onChatMessage(handleSignalRChatMessage);

    console.log("[Chat] SignalR 连接成功");
  } catch (error) {
    console.error("[Chat] SignalR 连接失败:", error);
    isSignalRConnected.value = false;
  }
}

onMounted(async () => {
  // 加载知识库列表
  await loadKnowledgeBases();

  // 初始化 SignalR
  await initializeSignalR();

  // 定期检查连接状态
  const checkInterval = setInterval(() => {
    isSignalRConnected.value = isConnected();
  }, 5000);

  onUnmounted(() => {
    clearInterval(checkInterval);
  });
});

onUnmounted(() => {
  stopSignalR();
});
</script>

<style scoped lang="scss">
.chat-container {
  display: flex;
  height: 100%;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.connection-status {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: #fef0f0;
  color: #f56c6c;
  font-size: 12px;
  border-bottom: 1px solid #fde2e2;
}

.kb-selector {
  display: flex;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #e4e7ed;
  gap: 8px;
}

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

  .hint {
    font-size: 12px;
    color: #a8abb2;
    margin-top: 8px;
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
      font-family: "Courier New", monospace;
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
  0%,
  60%,
  100% {
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
