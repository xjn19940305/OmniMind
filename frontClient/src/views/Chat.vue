<template>
  <div class="chat-container">
    <div class="chat-main">
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
            :type="getFileStatusType(file.status)"
            closable
            @close="handleRemoveFile(file.id)"
          >
            <el-icon><Document /></el-icon>
            {{ file.name }}
            <span v-if="file.status !== 5" class="file-status">
              {{ getFileStatusText(file.status) }}
            </span>
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
              :disabled="!canSendMessage"
              @click="handleSend"
            >
              <el-icon><Promotion /></el-icon>
              发送
            </el-button>
          </div>
        </div>
      </div>
    </div>
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
import { chatStream, uploadFile, createSession, checkFileHash } from "../api/chat";
import { getKnowledgeBases } from "../api/knowledge";
import {
  initSignalR,
  stopSignalR,
  onChatMessage,
  onDocumentProgress,
  isConnected,
  type SignalRMessage,
  type DocumentProgress,
} from "../utils/signalr";
import { calculateFileHash } from "../utils/fileHash";
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

// 是否可以发送消息：必须有输入内容，且文件都已就绪
const canSendMessage = computed(() => {
  const hasInput = inputMessage.value.trim().length > 0;
  const hasUnreadyFile = selectedFiles.value.some(f => f.status !== 5); // 5 = Indexed
  const result = hasInput && !hasUnreadyFile && !isStreaming.value;

  // 调试日志
  console.log("[Chat] canSendMessage 检查:", {
    hasInput,
    hasUnreadyFile,
    isStreaming: isStreaming.value,
    selectedFiles: selectedFiles.value.map(f => ({ id: f.id, name: f.name, status: f.status })),
    result
  });

  return result;
});

// 获取文件状态文本
function getFileStatusText(status?: number) {
  if (!status) return "上传中...";
  switch (status) {
    case 1: return "等待处理";
    case 2: return "解析中...";
    case 3: return "已解析";
    case 4: return "索引中...";
    case 5: return "已就绪";
    case 6: return "处理失败";
    default: return "未知状态";
  }
}

// 获取文件状态标签类型
function getFileStatusType(status?: number): "success" | "warning" | "danger" | "info" | "" {
  if (!status) return "info";
  switch (status) {
    case 1: return "info";
    case 2: return "warning";
    case 3: return "info";
    case 4: return "warning";
    case 5: return "success";
    case 6: return "danger";
    default: return "info";
  }
}

// 获取知识库名称
function getKnowledgeBaseName(id: string) {
  const kb = knowledgeBases.value.find((k) => k.id === id);
  return kb?.name || "未知知识库";
}

// 处理文档进度更新
function handleDocumentProgress(progress: DocumentProgress) {
  console.log("[Chat] 收到文档进度:", progress);
  console.log("[Chat] 当前文件列表:", selectedFiles.value);

  const fileIndex = selectedFiles.value.findIndex(f => f.id === progress.documentId);
  if (fileIndex === -1) {
    console.warn("[Chat] 找不到文件:", progress.documentId);
    return;
  }

  // 更新文件状态 - 使用新数组确保响应式更新
  const statusMap: Record<string, number> = {
    "Uploaded": 1,
    "Parsing": 2,
    "Parsed": 3,
    "Indexing": 4,
    "Indexed": 5,
    "Failed": 6
  };

  const newStatus = statusMap[progress.status] || 1;
  console.log(`[Chat] 更新文件状态: ${selectedFiles.value[fileIndex].status} -> ${newStatus}`);

  // 创建新数组以确保响应式更新
  selectedFiles.value = selectedFiles.value.map((f, i) =>
    i === fileIndex ? { ...f, status: newStatus } : f
  );

  // 如果文档已就绪
  if (progress.status === "Indexed") {
    ElMessage.success(`文件 ${progress.title} 已就绪，可以开始聊天`);
  }
  // 如果文档处理失败
  else if (progress.status === "Failed") {
    ElMessage.error(`文件 ${progress.title} 处理失败：${progress.error || "未知错误"}`);
  }

  console.log("[Chat] 更新后的文件列表:", selectedFiles.value);
  console.log("[Chat] canSendMessage:", canSendMessage.value);
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
    // 选择知识库时，清空已选文件
    selectedFiles.value = [];
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
  // 移除多余的空行（连续超过1个空行的情况）
  const cleanedContent = content
    .replace(/\n{3,}/g, '\n\n') // 3个或以上连续换行替换为2个
    .replace(/^\n+/, '') // 移除开头的空行
    .replace(/\n+$/, ''); // 移除结尾的空行

  return marked(cleanedContent);
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
    ElMessage.info(`正在计算 ${file.name} 的哈希值...`);

    // 1. 计算文件哈希
    const fileHash = await calculateFileHash(file.raw);
    console.log("[Chat] 文件哈希:", file.name, fileHash);

    // 2. 检查哈希是否已存在
    ElMessage.info(`正在检查 ${file.name} 是否已上传...`);
    const existingFile = await checkFileHash(fileHash);

    if (existingFile) {
      // 文件已存在，直接复用
      console.log("[Chat] 文件已存在，直接复用:", existingFile);
      selectedKnowledgeBase.value = "";
      selectedFiles.value.push({
        ...existingFile,
        status: 5 // 已存在的文件直接标记为已就绪
      });
      ElMessage.success(`文件 ${file.name} 已存在，已直接复用`);
    } else {
      // 文件不存在，需要上传
      console.log("[Chat] 文件不存在，开始上传");
      ElMessage.info(`正在上传 ${file.name}...`);

      const uploaded = await uploadFile(file.raw, currentSessionId.value, fileHash);

      // 上传文件时，清空知识库选择
      selectedKnowledgeBase.value = "";
      selectedFiles.value.push({
        ...uploaded,
        status: 1 // 初始状态为 Uploaded
      });

      ElMessage.success(`已添加 ${file.name}，正在处理中...`);
    }
  } catch (error: any) {
    console.error("[Chat] 文件处理失败:", error);
    ElMessage.error(error.response?.data?.message || "文件处理失败");
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

  // 获取或创建会话
  let sessionId = currentSessionId.value;
  if (!sessionId) {
    const newSession = chatStore.createSession();
    sessionId = newSession.id;
    console.log("[Chat] 创建新会话:", sessionId);
  }

  const content = inputMessage.value.trim();
  const files = [...selectedFiles.value];

  // 获取第一个文件的ID（如果有）- 在清空之前获取
  const documentId = selectedFiles.value.length > 0 ? selectedFiles.value[0].id : undefined;

  console.log("[Chat] 准备发送消息:", {
    sessionId,
    content,
    documentId,
    selectedFiles: selectedFiles.value.map(f => ({ id: f.id, name: f.name, status: f.status })),
    selectedKnowledgeBase: selectedKnowledgeBase.value,
    currentSessionId: currentSessionId.value
  });

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

  // 获取正确的 messageIndex - 从当前 session 中获取
  const currentSession = sessions.value.find(s => s.id === sessionId);
  const messageIndex = currentSession?.messages.length - 1 || 0;

  console.log("[Chat] 助手消息索引:", {
    sessionId,
    currentSessionId: currentSessionId.value,
    messageIndex,
    messagesLength: currentSession?.messages?.length || 0
  });

  // 设置流式状态
  chatStore.isStreaming = true;
  scrollToBottom();

  try {
    const history = buildHistory();

    // 调用统一聊天接口
    const response = await chatStream(
      content,
      selectedKnowledgeBase.value, // 知识库ID（与documentId互斥）
      documentId, // 文件ID（与knowledgeBase互斥）
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
      documentId ? "(使用临时文件)" : selectedKnowledgeBase.value ? "(使用知识库)" : "(默认聊天)",
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
  console.log("[Chat] ===== SignalR 消息开始处理 =====");
  console.log("[Chat] 接收到的完整数据:", data);
  console.log("[Chat] 当前 sessions:", sessions.value.map(s => ({ id: s.id, title: s.title, messagesCount: s.messages.length })));
  console.log("[Chat] 当前 currentSessionId:", currentSessionId.value);

  const { conversationId, message } = data;
  console.log("[Chat] 解构后 - conversationId:", conversationId);
  console.log("[Chat] 解构后 - message:", message);
  console.log("[Chat] messageId:", message.messageId);
  console.log("[Chat] content:", message.content);
  console.log("[Chat] isComplete:", message.isComplete);

  const pending = pendingMessages.get(message.messageId);
  console.log("[Chat] 查找 pending message:", pending);
  console.log("[Chat] pendingMessages 所有键:", Array.from(pendingMessages.keys()));

  if (pending) {
    console.log("[Chat] 找到 pending message，开始更新...");
    console.log("[Chat] 更新信息:", {
      sessionId: pending.sessionId,
      messageIndex: pending.messageIndex,
      sessionIdInStore: sessions.value.find(s => s.id === pending.sessionId)
    });

    chatStore.updateMessage(
      pending.sessionId,
      pending.messageIndex,
      message.content,
    );
    scrollToBottom();

    if (message.isComplete) {
      console.log("[Chat] 消息完成，清理 pending");
      chatStore.isStreaming = false;
      pendingMessages.delete(message.messageId);
      ElMessage.success("完成");
    }
  } else {
    console.log("[Chat] 未找到 pending message，处理为新消息");
    console.log("[Chat] 尝试在 conversationId 中查找会话:", conversationId);

    chatStore.isStreaming = !message.isComplete;

    const targetSession = sessions.value.find(s => s.id === conversationId);
    console.log("[Chat] 找到的目标 session:", targetSession);

    if (targetSession) {
      const existingMessage = targetSession.messages.find(
        (m) => m.id === message.messageId,
      );
      console.log("[Chat] 查找现有消息:", existingMessage);

      if (existingMessage) {
        console.log("[Chat] 更新现有消息内容");
        existingMessage.content = message.content;
      } else {
        console.log("[Chat] 添加新消息到 session");
        targetSession.messages.push({
          id: message.messageId,
          role: message.role as "user" | "assistant",
          content: message.content,
          timestamp: message.timestamp,
        });
      }
    } else {
      console.warn("[Chat] 未找到对应的 session:", conversationId);
    }

    scrollToBottom();
  }

  console.log("[Chat] ===== SignalR 消息处理完成 =====");
  console.log("[Chat] 处理后的 sessions:", sessions.value.map(s => ({ id: s.id, title: s.title, messagesCount: s.messages.length })));
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
    onDocumentProgress(handleDocumentProgress);

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
    // 清除 SignalR 连接检查定时器
    clearInterval(checkInterval);

    // 停止 SignalR
    stopSignalR();
  });
});
</script>

<style scoped lang="scss">
.chat-container {
  display: flex;
  height: 100%;
  background: #f7f8fa;
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
  overflow: hidden;
}

.connection-status {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 10px 16px;
  background: linear-gradient(135deg, #fef0f0 0%, #ffebeb 100%);
  color: #f56c6c;
  font-size: 13px;
  border-bottom: 1px solid #fde2e2;
  animation: slideDown 0.3s ease;

  .el-icon {
    animation: pulse 2s infinite;
  }
}

.kb-selector {
  display: flex;
  align-items: center;
  padding: 14px 20px;
  background: white;
  border-bottom: 1px solid #ebeef5;
  gap: 12px;
  transition: all 0.3s ease;

  &:hover {
    background: #fafbfc;
  }

  .el-select {
    flex: 1;
  }

  .el-tag {
    animation: fadeIn 0.3s ease;
  }
}

.chat-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: white;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 24px 20px;
  background: linear-gradient(180deg, #ffffff 0%, #f9fafb 100%);

  &::-webkit-scrollbar {
    width: 6px;
  }

  &::-webkit-scrollbar-track {
    background: transparent;
  }

  &::-webkit-scrollbar-thumb {
    background: #dcdfe6;
    border-radius: 3px;

    &:hover {
      background: #c0c4cc;
    }
  }
}

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #909399;
  animation: fadeIn 0.5s ease;

  .el-icon {
    animation: float 3s ease-in-out infinite;
  }

  p {
    margin-top: 20px;
    font-size: 15px;
    font-weight: 500;
    color: #606266;
  }

  .hint {
    font-size: 13px;
    color: #909399;
    margin-top: 10px;
    padding: 8px 16px;
    background: #f5f7fa;
    border-radius: 20px;
  }
}

.messages {
  .message {
    display: flex;
    margin-bottom: 6px;
    animation: messageSlide 0.3s ease;

    &.user {
      flex-direction: row-reverse;

      .message-content {
        align-items: flex-end;
      }

      .message-text {
        background: linear-gradient(135deg, #409eff 0%, #5bb5ff 100%);
        color: white;
        box-shadow: 0 2px 8px rgba(64, 158, 255, 0.2);
      }
    }

    &.assistant {
      .message-text {
        background: white;
        border: 1px solid #e4e7ed;
        box-shadow: 0 1px 4px rgba(0, 0, 0, 0.03);
      }
    }

    &.streaming .message-text {
      background: white;
      border: 1px solid #e4e7ed;
      animation: pulseBorder 2s infinite;
    }
  }

  .message-avatar {
    flex-shrink: 0;
    margin: 0 8px;

    .el-avatar {
      box-shadow: 0 1px 4px rgba(0, 0, 0, 0.1);
      transition: transform 0.2s ease;

      &:hover {
        transform: scale(1.05);
      }
    }
  }

  .message-content {
    display: flex;
    flex-direction: column;
    max-width: 75%;
  }

  .message-files {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
    margin-bottom: 4px;
  }

  .file-tag {
    cursor: pointer;
    transition: all 0.2s ease;

    &:hover {
      transform: translateY(-1px);
      box-shadow: 0 2px 6px rgba(0, 0, 0, 0.1);
    }
  }

  .message-text {
    padding: 8px 12px;
    border-radius: 10px;
    word-wrap: break-word;
    white-space: pre-wrap;
    line-height: 1.5;
    font-size: 14px;
    transition: all 0.3s ease;

    :deep(img) {
      max-width: 100%;
      border-radius: 6px;
      margin: 4px 0;
    }

    :deep(pre) {
      background: #f5f7fa;
      padding: 8px;
      border-radius: 6px;
      overflow-x: auto;
      margin: 4px 0;
      border: 1px solid #e4e7ed;
      font-size: 13px;
    }

    :deep(code) {
      background: rgba(0, 0, 0, 0.05);
      padding: 2px 4px;
      border-radius: 3px;
      font-family: "Consolas", "Monaco", monospace;
      font-size: 13px;
    }

    :deep(p) {
      margin: 2px 0;

      &:first-child {
        margin-top: 0;
      }

      &:last-child {
        margin-bottom: 0;
      }
    }

    :deep(h1),
    :deep(h2),
    :deep(h3) {
      margin: 6px 0 4px;
      font-weight: 600;
      font-size: 15px;
    }

    :deep(h1) {
      font-size: 16px;
    }

    :deep(ul),
    :deep(ol) {
      margin: 4px 0;
      padding-left: 16px;

      li {
        margin: 1px 0;
      }
    }
  }

  .message-time {
    margin-top: 2px;
    font-size: 10px;
    color: #c0c4cc;
    font-weight: 500;
  }
}

.typing-indicator {
  display: flex;
  gap: 6px;
  padding: 16px 20px;
  background: white;
  border: 1px solid #e4e7ed;
  border-radius: 16px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);

  span {
    width: 9px;
    height: 9px;
    border-radius: 50%;
    background: linear-gradient(135deg, #409eff 0%, #5bb5ff 100%);
    animation: typing 1.4s infinite;
    box-shadow: 0 2px 6px rgba(64, 158, 255, 0.3);
  }

  span:nth-child(2) {
    animation-delay: 0.2s;
  }

  span:nth-child(3) {
    animation-delay: 0.4s;
  }
}

.input-area {
  border-top: 1px solid #e4e7ed;
  padding: 16px 20px;
  background: white;
}

.selected-files {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-bottom: 14px;
  animation: fadeIn 0.3s ease;
}

.input-box {
  display: flex;
  flex-direction: column;
  gap: 14px;

  .input-actions {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-top: 8px;

    .el-button {
      border-radius: 8px;
      font-weight: 500;
      transition: all 0.3s ease;

      &:hover {
        transform: translateY(-1px);
      }

      &.el-button--primary {
        box-shadow: 0 2px 8px rgba(64, 158, 255, 0.25);
      }
    }
  }

  :deep(.el-textarea__inner) {
    border-radius: 10px;
    border: 1.5px solid #e4e7ed;
    transition: all 0.3s ease;
    font-size: 14px;

    &:focus {
      border-color: #409eff;
      box-shadow: 0 0 0 3px rgba(64, 158, 255, 0.1);
    }
  }
}

// 动画定义
@keyframes typing {
  0%,
  60%,
  100% {
    transform: translateY(0) scale(1);
    opacity: 0.7;
  }
  30% {
    transform: translateY(-12px) scale(1.1);
    opacity: 1;
  }
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes slideDown {
  from {
    opacity: 0;
    transform: translateY(-20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@keyframes float {
  0%,
  100% {
    transform: translateY(0);
  }
  50% {
    transform: translateY(-10px);
  }
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

@keyframes pulseBorder {
  0%,
  100% {
    border-color: #e4e7ed;
  }
  50% {
    border-color: #409eff;
  }
}

@keyframes messageSlide {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

// 响应式优化
@media (max-width: 768px) {
  .message-content {
    max-width: 85%;
  }

  .messages-container {
    padding: 16px 12px;
  }

  .input-area {
    padding: 12px;
  }

  .kb-selector {
    padding: 10px 12px;
  }
}

@media (max-width: 480px) {
  .message-content {
    max-width: 90%;
  }

  .message-text {
    font-size: 13px;
    padding: 12px 14px;
  }

  .message-avatar {
    margin: 0 8px;

    .el-avatar {
      width: 32px !important;
      height: 32px !important;
    }
  }
}
</style>
