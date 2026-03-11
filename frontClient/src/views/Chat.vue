<template>
  <div class="chat-container">
    <div class="chat-main">
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
              <div
                v-if="message.role === 'assistant' && message.references?.length"
                class="message-references"
              >
                <div class="references-title">引用来源</div>
                <div class="reference-card-list">
                  <div
                    v-for="reference in message.references"
                    :key="reference.documentId"
                    class="reference-card"
                    @click="handleReferenceClick(reference)"
                  >
                    <div class="reference-card-header">
                      <span class="reference-name">{{ reference.documentTitle }}</span>
                      <span class="reference-source">
                        {{ getReferenceSourceLabel(reference.sourceType) }}
                      </span>
                    </div>
                    <div class="reference-snippet">{{ reference.snippet }}</div>
                    <div v-if="reference.hitCount > 1" class="reference-hit-count">
                      命中 {{ reference.hitCount }} 处
                    </div>
                    <div v-if="typeof reference.score === 'number'" class="reference-score">
                      相关度 {{ formatReferenceScore(reference.score) }}
                    </div>
                  </div>
                </div>
              </div>
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
              <div class="streaming-actions">
                <el-button
                  size="small"
                  type="danger"
                  :icon="VideoPause"
                  @click="handleCancelStreaming"
                >
                  停止生成
                </el-button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Input Area -->
      <div class="input-area">
        <div class="composer-shell">
          <div
            v-if="selectedFiles.length > 0 || selectedKnowledgeBase"
            class="selected-files"
          >
            <el-tag
              v-if="selectedKnowledgeBase"
              class="selected-knowledge-base"
              type="success"
              closable
              @close="handleClearKnowledgeBase"
            >
              <el-icon><Collection /></el-icon>
              {{ getKnowledgeBaseName(selectedKnowledgeBase) }}
            </el-tag>
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
            <div class="editor-shell">
              <el-input
                v-model="inputMessage"
                type="textarea"
                :autosize="{ minRows: 3, maxRows: 6 }"
                :placeholder="
                  selectedKnowledgeBase ? '基于知识库回答问题...' : '输入消息...'
                "
                @keydown.enter.prevent="handleEnter"
              />
            </div>

            <div class="toolbar-actions">
              <div class="input-tools">
                <el-select
                  v-model="selectedModel"
                  class="model-select"
                  :disabled="isLoadingModels"
                  placeholder="选择模型"
                >
                  <el-option
                    v-for="model in modelOptions"
                    :key="model"
                    :label="model"
                    :value="model"
                  />
                </el-select>
                <el-upload
                  ref="uploadRef"
                  :auto-upload="false"
                  :show-file-list="false"
                  :on-change="handleFileChange"
                  accept=".pdf,.docx,.pptx,.xlsx,.md,.txt,.jpg,.jpeg,.png,.gif"
                >
                  <el-button text class="tool-button">
                    <el-icon><Plus /></el-icon>
                    上传文件
                  </el-button>
                </el-upload>
                <el-select
                  v-model="selectedKnowledgeBase"
                  class="knowledge-select"
                  placeholder="选择知识库"
                  clearable
                  filterable
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
              </div>

              <el-button
                type="primary"
                class="send-button"
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
  Collection,
  Delete,
  ChatDotRound,
  Promotion,
  Warning,
  VideoPause,
} from "@element-plus/icons-vue";
import { useChatStore } from "../stores/chat";
import { useUserStore } from "../stores/user";
import {
  chatStream,
  uploadFile,
  checkFileHash,
  cancelStreamingMessage,
} from "../api/chat";
import { getModelConfig } from "../api/config";
import { getKnowledgeBases } from "../api/knowledge";
import { request } from "../utils/request";
import { resolvePreviewRequestUrl } from "../utils/previewUrl";
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
import {
  DEFAULT_CHAT_MODEL,
  resolveSelectedModel,
  sanitizeModelOptions,
} from "../utils/chatModel";
import { normalizeChatMessageContent } from "../utils/chatMessageContent";
import type {
  Attachment,
  ChatMessage as ApiChatMessage,
  ChatReference,
} from "../types";

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
const currentStreamingMessageId = ref<string | null>(null);
const modelOptions = ref<string[]>([DEFAULT_CHAT_MODEL]);
const selectedModel = ref(DEFAULT_CHAT_MODEL);
const isLoadingModels = ref(false);

const sessions = computed(() => chatStore.sessions);
const conversations = computed(() => chatStore.conversations);
const currentSessionId = computed(() => chatStore.currentConversationId);
const currentMessages = computed(() => chatStore.currentMessages);
const isStreaming = computed(() => chatStore.isStreaming);

// 是否可以发送消息：必须有输入内容，且文件都已就绪
const canSendMessage = computed(() => {
  const hasInput = inputMessage.value.trim().length > 0;
  const hasUnreadyFile = selectedFiles.value.some((f) => f.status !== 5);
  const result = hasInput && !hasUnreadyFile && !isStreaming.value;

  console.log("[Chat] canSendMessage check:", {
    hasInput,
    hasUnreadyFile,
    isStreaming: isStreaming.value,
    selectedFiles: selectedFiles.value.map((f) => ({
      id: f.id,
      name: f.name,
      status: f.status,
    })),
    result,
  });

  return result;
});

function getFileStatusText(status?: number) {
  if (!status) return "上传中...";
  switch (status) {
    case 1:
      return "等待处理";
    case 2:
      return "解析中...";
    case 3:
      return "已解析";
    case 4:
      return "索引中...";
    case 5:
      return "已就绪";
    case 6:
      return "处理失败";
    default:
      return "未知状态";
  }
}

// 获取文件状态标签类型
function getFileStatusType(
  status?: number,
): "success" | "warning" | "danger" | "info" | "" {
  if (!status) return "info";
  switch (status) {
    case 1:
      return "info";
    case 2:
      return "warning";
    case 3:
      return "info";
    case 4:
      return "warning";
    case 5:
      return "success";
    case 6:
      return "danger";
    default:
      return "info";
  }
}

// 获取知识库名称
function getKnowledgeBaseName(id: string) {
  const kb = knowledgeBases.value.find((k) => k.id === id);
  return kb?.name || "未知知识库";
}

function hasKnowledgeBase(id?: string | null) {
  return !!id && knowledgeBases.value.some((kb) => kb.id === id);
}

// 处理文档进度更新
function getReferenceSourceLabel(sourceType: ChatReference["sourceType"]) {
  return sourceType === "document" ? "文件问答" : "知识库";
}

function formatReferenceScore(score: number) {
  return `${Math.round(score * 100)}%`;
}

function handleDocumentProgress(progress: DocumentProgress) {
  console.log("[Chat] 收到文档进度:", progress);
  console.log("[Chat] 当前文件列表:", selectedFiles.value);

  const fileIndex = selectedFiles.value.findIndex(
    (f) => f.id === progress.documentId,
  );
  if (fileIndex === -1) {
    console.warn("[Chat] 找不到文件:", progress.documentId);
    return;
  }

  const statusMap: Record<string, number> = {
    Uploaded: 1,
    Parsing: 2,
    Parsed: 3,
    Indexing: 4,
    Indexed: 5,
    Failed: 6,
  };

  const newStatus = statusMap[progress.status] || 1;
  console.log(
    `[Chat] 更新文件状态: ${selectedFiles.value[fileIndex].status} -> ${newStatus}`,
  );

  selectedFiles.value = selectedFiles.value.map((f, i) =>
    i === fileIndex ? { ...f, status: newStatus } : f,
  );

  if (progress.status === "Indexed") {
    ElMessage.success(`文件 ${progress.title} 已就绪，可以开始聊天`);
  } else if (progress.status === "Failed") {
    ElMessage.error(
      `文件 ${progress.title} 处理失败：${progress.error || "未知错误"}`,
    );
  }

  console.log("[Chat] 更新后的文件列表:", selectedFiles.value);
  console.log("[Chat] canSendMessage:", canSendMessage.value);
}

// 加载知识库列表
async function loadKnowledgeBases() {
  try {
    const data = await getKnowledgeBases({ pageSize: 100 });
    knowledgeBases.value = data?.items || [];

    if (
      selectedKnowledgeBase.value &&
      !hasKnowledgeBase(selectedKnowledgeBase.value)
    ) {
      const staleKnowledgeBaseId = selectedKnowledgeBase.value;
      selectedKnowledgeBase.value = "";
      console.warn(
        "[Chat] cleared stale knowledge base selection:",
        staleKnowledgeBaseId,
      );
      ElMessage.warning("当前选择的知识库已失效，已切换为默认聊天");
    }
  } catch (error) {
    console.error("加载知识库失败:", error);
  }
}

// 知识库变化时记录日志
async function loadChatModels() {
  isLoadingModels.value = true;

  try {
    const config = await getModelConfig();
    const options = sanitizeModelOptions(config.chatModels);

    modelOptions.value = options.length > 0 ? options : [DEFAULT_CHAT_MODEL];
    selectedModel.value = resolveSelectedModel(
      modelOptions.value,
      selectedModel.value,
    );
  } catch (error) {
    modelOptions.value = [DEFAULT_CHAT_MODEL];
    selectedModel.value = resolveSelectedModel(modelOptions.value);
    ElMessage.warning("模型列表加载失败，已使用默认模型");
    console.error("[Chat] 加载模型列表失败:", error);
  } finally {
    isLoadingModels.value = false;
  }
}

function handleKnowledgeBaseChange(value: string | undefined) {
  if (value) {
    selectedFiles.value = [];
    console.log("[Chat] 已选择知识库:", getKnowledgeBaseName(value));
    ElMessage.info(`已切换到知识库：${getKnowledgeBaseName(value)}`);
  } else {
    console.log("[Chat] 已取消知识库，使用默认聊天");
  }
}

function handleClearKnowledgeBase() {
  selectedKnowledgeBase.value = "";
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
  return marked(normalizeChatMessageContent(content));
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight;
    }
  });
}

async function handleNewChat() {
  chatStore.createNewConversation();
  selectedKnowledgeBase.value = "";
  selectedFiles.value = [];
  scrollToBottom();
}

async function handleSelectSession(conversationId: string) {
  await chatStore.selectConversation(conversationId);
  scrollToBottom();
}

async function handleDeleteSession(conversationId: string) {
  const success = await chatStore.deleteConversation(conversationId);
  if (success) {
    ElMessage.success("已删除对话");
  }
}

async function handleFileChange(file: UploadFile) {
  if (!file.raw) return;
  if (selectedFiles.value.length > 0) {
    uploadRef.value?.clearFiles();
    ElMessage.warning("聊天只支持单文件，请先移除当前文件");
    return;
  }

  try {
    ElMessage.info(`正在计算 ${file.name} 的哈希值...`);

    const fileHash = await calculateFileHash(file.raw);
    console.log("[Chat] 文件哈希:", file.name, fileHash);

    ElMessage.info(`正在检查 ${file.name} 是否已上传...`);
    const existingFile = await checkFileHash(fileHash);

    if (existingFile) {
      console.log("[Chat] 文件已存在，直接复用:", existingFile);
      selectedKnowledgeBase.value = "";
      selectedFiles.value.push({
        ...existingFile,
        status: 5,
      });
      ElMessage.success(`文件 ${file.name} 已存在，已直接复用`);
    } else {
      console.log("[Chat] 文件不存在，开始上传");
      ElMessage.info(`正在上传 ${file.name}...`);

      const uploaded = await uploadFile(
        file.raw,
        currentSessionId.value,
        fileHash,
      );

      selectedKnowledgeBase.value = "";
      selectedFiles.value.push({
        ...uploaded,
        status: 1,
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

function legacyHandlePreviewPlaceholder(file: Attachment) {
  ElMessage.info("文件预览功能开发中");
}

async function openPreviewUrl(previewUrl: string) {
  const blob = await request<Blob>({
    url: resolvePreviewRequestUrl(previewUrl),
    method: "get",
    responseType: "blob",
  });
  const objectUrl = URL.createObjectURL(blob);
  const previewWindow = window.open(objectUrl, "_blank", "noopener,noreferrer");

  if (!previewWindow) {
    URL.revokeObjectURL(objectUrl);
    throw new Error("preview-window-blocked");
  }

  setTimeout(() => {
    URL.revokeObjectURL(objectUrl);
  }, 60_000);
}

async function legacyHandlePreviewFile(file: Attachment) {
  try {
    await openPreviewUrl(file.url);
  } catch (error) {
    console.error("[Chat] file preview failed:", error);
    ElMessage.error("鏂囦欢棰勮澶辫触");
  }
}

async function legacyHandleReferenceClick(reference: ChatReference) {
  try {
    await openPreviewUrl(reference.previewUrl);
  } catch (error) {
    console.error("[Chat] reference preview failed:", error);
    ElMessage.error("寮曠敤鏂囦欢鎵撳紑澶辫触");
  }
}

async function handlePreviewFile(file: Attachment) {
  try {
    await openPreviewUrl(file.url);
  } catch (error) {
    console.error("[Chat] file preview failed:", error);
    ElMessage.error("文件预览失败");
  }
}

async function handleReferenceClick(reference: ChatReference) {
  try {
    await openPreviewUrl(reference.previewUrl);
  } catch (error) {
    console.error("[Chat] reference preview failed:", error);
    ElMessage.error("引用文件打开失败");
  }
}

function handleEnter(event: KeyboardEvent) {
  if (!event.shiftKey) {
    handleSend();
  }
}

// 取消流式消息生成
function generateClientMessageId() {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `${Date.now()}_${Math.random().toString(36).slice(2)}_assistant`;
}

async function handleCancelStreaming() {
  if (!currentStreamingMessageId.value) return;

  try {
    await cancelStreamingMessage(currentStreamingMessageId.value);
    console.log("[Chat] 已取消消息生成:", currentStreamingMessageId.value);
  } catch (error: any) {
    console.error("[Chat] 取消失败:", error);
  } finally {
    currentStreamingMessageId.value = null;
    chatStore.isStreaming = false;
  }
}

async function handleSend() {
  if (!inputMessage.value.trim() && selectedFiles.value.length === 0) return;
  if (isStreaming.value) return;

  const conversationId = currentSessionId.value;
  const localConversationId = conversationId || `temp-${Date.now()}`;
  const content = inputMessage.value.trim();
  const files = [...selectedFiles.value];
  chatStore.ensureSession(localConversationId, content.slice(0, 20) || "新对话");

  const knowledgeBaseId = hasKnowledgeBase(selectedKnowledgeBase.value)
    ? selectedKnowledgeBase.value
    : undefined;

  if (selectedKnowledgeBase.value && !knowledgeBaseId) {
    console.warn(
      "[Chat] blocked stale knowledge base ID:",
      selectedKnowledgeBase.value,
    );
    selectedKnowledgeBase.value = "";
    ElMessage.warning("当前选择的知识库不存在或无权访问，已切换为默认聊天");
  }

  const documentId =
    selectedFiles.value.length > 0 ? selectedFiles.value[0].id : undefined;

  console.log("[Chat] 准备发送消息:", {
    conversationId: localConversationId,
    content,
    documentId,
    selectedFiles: selectedFiles.value.map((f) => ({
      id: f.id,
      name: f.name,
      status: f.status,
    })),
    selectedKnowledgeBase: knowledgeBaseId,
  });

  chatStore.addMessage(localConversationId, {
    id: Date.now().toString(),
    role: "user",
    content,
    timestamp: new Date().toISOString(),
    files,
  });

  inputMessage.value = "";
  selectedFiles.value = [];

  const assistantMessageId = generateClientMessageId();
  chatStore.addMessage(localConversationId, {
    id: assistantMessageId,
    role: "assistant",
    content: "",
    timestamp: new Date().toISOString(),
  });

  const currentSession = sessions.value.find((s) => s.id === localConversationId);
  const messageIndex = currentSession?.messages.length - 1 || 0;

  console.log("[Chat] 助手消息索引:", {
    conversationId: localConversationId,
    messageIndex,
    messagesLength: currentSession?.messages?.length || 0,
  });

  chatStore.isStreaming = true;
  scrollToBottom();

  try {
    const history = buildHistory();
    pendingMessages.set(assistantMessageId, {
      sessionId: localConversationId,
      messageIndex,
      localMessageId: assistantMessageId,
    });

    const response = await chatStream(
      content,
      knowledgeBaseId,
      documentId,
      conversationId || undefined,
      assistantMessageId,
      undefined,
      resolveSelectedModel(modelOptions.value, selectedModel.value),
      history,
    );

    const pending = pendingMessages.get(assistantMessageId);
    if (pending) {
      pendingMessages.set(response.messageId, {
        ...pending,
        sessionId: response.conversationId,
      });

      if (response.messageId !== assistantMessageId) {
        pendingMessages.delete(assistantMessageId);
      }
    }

    currentStreamingMessageId.value = response.messageId;

    if (response.conversationId !== localConversationId) {
      chatStore.promoteSession(localConversationId, response.conversationId);
    } else {
      chatStore.currentConversationId = response.conversationId;
    }

    console.log(
      "[Chat] 消息已发送:",
      response.messageId,
      response.conversationId,
      documentId
        ? "(使用文件)"
        : knowledgeBaseId
          ? "(使用知识库)"
          : "(默认聊天)",
    );
  } catch (error: any) {
    pendingMessages.delete(assistantMessageId);
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
  console.log(
    "[Chat] 当前 conversations:",
    conversations.value.map((c) => ({ id: c.id, title: c.title })),
  );
  console.log("[Chat] 当前 currentConversationId:", currentSessionId.value);

  const { message } = data;
  console.log("[Chat] messageId:", message.messageId);
  console.log("[Chat] content:", message.content?.substring(0, 50));
  console.log("[Chat] isComplete:", message.isComplete);

  const pending = pendingMessages.get(message.messageId);
  console.log("[Chat] 查找 pending message:", pending);

  if (pending) {
    console.log("[Chat] 找到 pending message，开始更新...");
    chatStore.updateMessage(
      pending.sessionId,
      pending.messageIndex,
      message.content || "",
      message.isComplete,
    );
    chatStore.updateStreamingMessage(
      message.messageId,
      message.content || "",
      message.isComplete,
    );
    scrollToBottom();

    if (message.isComplete) {
      console.log("[Chat] 消息完成，清理 pending");
      chatStore.isStreaming = false;
      pendingMessages.delete(message.messageId);
      currentStreamingMessageId.value = null;

      // 刷新会话列表以更新最后消息时间
      chatStore.loadConversations();
      if (currentSessionId.value === data.conversationId) {
        void chatStore.loadConversationDetail(data.conversationId).then(() => {
          scrollToBottom();
        });
      }
    }
  } else {
    console.log("[Chat] 未找到 pending message，处理为新消息");

    chatStore.isStreaming = !message.isComplete;
    chatStore.updateStreamingMessage(
      message.messageId,
      message.content || "",
      message.isComplete,
    );

    scrollToBottom();
  }

  console.log("[Chat] ===== SignalR 消息处理完成 =====");
}

// 初始化 SignalR
async function initializeSignalR() {
  try {
    const userId = userStore.userInfo?.id;
    if (!userId) {
      console.warn("[Chat] 没有 user ID，无法连接 SignalR");
      return;
    }

    await initSignalR();
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
  await chatStore.loadConversations();
  await loadChatModels();
  await loadKnowledgeBases();
  await initializeSignalR();

  const checkInterval = setInterval(() => {
    isSignalRConnected.value = isConnected();
  }, 5000);

  onUnmounted(() => {
    clearInterval(checkInterval);
    stopSignalR();
  });
});
</script>

<style scoped lang="scss">
.chat-container {
  display: flex;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  height: 100%;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.74), rgba(244, 248, 255, 0.9));
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius-lg);
  box-shadow: var(--app-shadow-md);
  backdrop-filter: blur(18px);
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

.chat-main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  background: transparent;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 28px 24px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.82) 0%, rgba(243, 247, 253, 0.88) 100%);

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

  .message-references {
    margin-top: 10px;
    padding: 12px;
    border-radius: 14px;
    background: rgba(248, 250, 252, 0.92);
    border: 1px solid rgba(148, 163, 184, 0.18);
  }

  .references-title {
    margin-bottom: 10px;
    font-size: 12px;
    font-weight: 700;
    color: #475569;
    letter-spacing: 0.02em;
  }

  .reference-card-list {
    display: flex;
    flex-direction: column;
    gap: 10px;
  }

  .reference-card {
    padding: 10px 12px;
    border-radius: 12px;
    background: #ffffff;
    border: 1px solid rgba(148, 163, 184, 0.14);
    box-shadow: 0 4px 14px rgba(15, 23, 42, 0.04);
    cursor: pointer;
    transition:
      transform 0.2s ease,
      box-shadow 0.2s ease,
      border-color 0.2s ease;

    &:hover {
      transform: translateY(-1px);
      border-color: rgba(37, 99, 235, 0.24);
      box-shadow: 0 10px 22px rgba(37, 99, 235, 0.08);
    }
  }

  .reference-card-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 10px;
    margin-bottom: 6px;
  }

  .reference-name {
    font-size: 13px;
    font-weight: 600;
    color: #1e293b;
    word-break: break-word;
  }

  .reference-source {
    flex-shrink: 0;
    padding: 2px 8px;
    border-radius: 999px;
    background: rgba(37, 99, 235, 0.08);
    color: #2563eb;
    font-size: 11px;
    font-weight: 600;
  }

  .reference-snippet {
    font-size: 12px;
    line-height: 1.6;
    color: #475569;
    white-space: pre-wrap;
    word-break: break-word;
  }

  .reference-hit-count {
    margin-top: 8px;
    font-size: 11px;
    color: #475569;
    font-weight: 600;
  }

  .reference-score {
    margin-top: 8px;
    font-size: 11px;
    color: #64748b;
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

.streaming-actions {
  margin-top: 8px;

  .el-button {
    border-radius: 6px;
    font-size: 12px;
    padding: 4px 12px;
    height: auto;

    &:hover {
      transform: translateY(-1px);
      box-shadow: 0 2px 6px rgba(245, 108, 108, 0.3);
    }
  }
}

.input-area {
  border-top: 1px solid var(--app-border);
  padding: 18px 20px calc(18px + var(--safe-bottom));
  background: rgba(255, 255, 255, 0.88);
  backdrop-filter: blur(20px);
}

.composer-shell {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 16px;
  border: 1px solid rgba(37, 99, 235, 0.08);
  border-radius: 22px;
  background: linear-gradient(180deg, rgba(255, 255, 255, 0.96), rgba(241, 246, 255, 0.92));
  box-shadow: 0 12px 32px rgba(15, 23, 42, 0.06);
}

.selected-files {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  animation: fadeIn 0.3s ease;

  .selected-knowledge-base {
    :deep(.el-tag__content) {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
  }
}

.input-box {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.editor-shell {
  position: relative;
}

.toolbar-actions {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 14px;

  .input-tools {
    display: flex;
    align-items: center;
    gap: 12px;
    flex-wrap: wrap;
    min-width: 0;
    flex: 1;
  }

  .model-select,
  .knowledge-select {
    width: 220px;
    max-width: 100%;
  }

  .tool-button {
    min-height: 42px;
    padding: 0 14px;
    border-radius: 12px;
    background: rgba(37, 99, 235, 0.06);
    border: 1px solid rgba(37, 99, 235, 0.08);
  }

  .send-button {
    min-width: 132px;
    min-height: 44px;
    border: none;
    border-radius: 14px;
    background: linear-gradient(135deg, #2563eb 0%, #3b82f6 100%);
    box-shadow: 0 12px 24px rgba(37, 99, 235, 0.24);
  }

  .el-button {
    font-weight: 600;
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
  border-radius: 18px;
  border: 1px solid rgba(148, 163, 184, 0.22);
  background: rgba(255, 255, 255, 0.92);
  transition: all 0.3s ease;
  font-size: 14px;
  padding: 14px 16px;

  &:focus {
    border-color: #409eff;
    box-shadow: 0 0 0 3px rgba(64, 158, 255, 0.1);
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
    padding: 18px 14px;
  }

  .input-area {
    padding: 12px 12px calc(14px + var(--safe-bottom));
  }

  .composer-shell {
    padding: 14px;
    border-radius: 18px;
  }

  .toolbar-actions {
    flex-direction: column;
    align-items: stretch;

    .input-tools {
      width: 100%;
    }

    .model-select,
    .knowledge-select {
      width: 100%;
    }

    .send-button {
      width: 100%;
    }
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

  .selected-files {
    gap: 8px;
  }

  .message-avatar {
    margin: 0 8px;

    .el-avatar {
      width: 32px !important;
      height: 32px !important;
    }
  }

  .tool-button {
    width: 100%;
    justify-content: center;
  }
}
</style>
