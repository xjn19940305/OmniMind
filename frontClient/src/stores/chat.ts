import { defineStore } from "pinia";
import { ref, computed } from "vue";
import type {
  ChatMessage,
  ChatSession,
  Conversation,
  ConversationDetail,
} from "../types";
import { parseChatReferences } from "../utils/chatReferences";
import {
  getConversations,
  getConversation,
  deleteConversation as apiDeleteConversation,
} from "../api/chat";

export const useChatStore = defineStore("chat", () => {
  // 本地会话列表，兼容新会话创建阶段的乐观更新
  const sessions = ref<ChatSession[]>([]);
  const currentConversationId = ref<string | null>(null);
  const isStreaming = ref(false);

  // 后端会话列表
  const conversations = ref<Conversation[]>([]);
  const currentConversationDetail = ref<ConversationDetail | null>(null);
  const isLoadingConversations = ref(false);

  const currentSession = computed(() =>
    sessions.value.find((s) => s.id === currentConversationId.value),
  );

  // 兼容旧命名
  const currentSessionId = computed(() => currentConversationId.value);

  const currentConversation = computed(() =>
    currentConversationDetail.value?.id === currentConversationId.value
      ? currentConversationDetail.value
      : null,
  );

  const currentMessages = computed(() => {
    if (
      currentConversationDetail.value &&
      currentConversationDetail.value.id === currentConversationId.value
    ) {
      return currentConversationDetail.value.messages.map((m) => ({
        id: m.id,
        role: m.role as "user" | "assistant" | "system",
        content: m.content,
        timestamp: m.createdAt,
        references: parseChatReferences(m.references),
        status: m.status as any,
        error: m.error,
        completedAt: m.completedAt,
      }));
    }

    return currentSession.value?.messages || [];
  });

  function mergeStreamingContent(
    previousContent: string | undefined,
    incomingContent: string,
    isComplete: boolean,
  ) {
    const prev = previousContent || "";

    if (!incomingContent) {
      return prev;
    }

    return prev + incomingContent;
  }

  async function loadConversations() {
    try {
      isLoadingConversations.value = true;
      const response = await getConversations({ pageSize: 100 });
      conversations.value = response.conversations;
    } catch (error) {
      console.error("加载会话列表失败:", error);
    } finally {
      isLoadingConversations.value = false;
    }
  }

  async function loadConversationDetail(conversationId: string) {
    try {
      const detail = await getConversation(conversationId);
      currentConversationDetail.value = detail;
      currentConversationId.value = conversationId;
      return detail;
    } catch (error) {
      console.error("加载会话详情失败:", error);
      return null;
    }
  }

  async function selectConversation(conversationId: string) {
    currentConversationId.value = conversationId;
    await loadConversationDetail(conversationId);
  }

  function createNewConversation() {
    currentConversationId.value = null;
    currentConversationDetail.value = null;
  }

  async function deleteConversation(conversationId: string) {
    try {
      await apiDeleteConversation(conversationId);
      conversations.value = conversations.value.filter(
        (c) => c.id !== conversationId,
      );

      if (currentConversationId.value === conversationId) {
        currentConversationId.value = null;
        currentConversationDetail.value = null;
      }

      return true;
    } catch (error) {
      console.error("删除会话失败:", error);
      return false;
    }
  }

  function updateStreamingMessage(
    messageId: string,
    content: string,
    isComplete: boolean,
  ) {
    if (
      !currentConversationDetail.value ||
      currentConversationDetail.value.id !== currentConversationId.value
    ) {
      return;
    }

    const message = currentConversationDetail.value.messages.find(
      (m) => m.id === messageId,
    );
    if (message) {
      message.content = mergeStreamingContent(message.content, content, isComplete);
      message.status = isComplete ? "completed" : "streaming";
      message.completedAt = isComplete ? new Date().toISOString() : message.completedAt;
    } else {
      currentConversationDetail.value.messages.push({
        id: messageId,
        role: "assistant",
        content: content || "",
        status: isComplete ? "completed" : "streaming",
        createdAt: new Date().toISOString(),
        completedAt: isComplete ? new Date().toISOString() : undefined,
      });
    }
  }

  function createSession(title?: string) {
    const newSession: ChatSession = {
      id: Date.now().toString(),
      title: title || "新对话",
      messages: [],
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };
    sessions.value.unshift(newSession);
    currentConversationId.value = newSession.id;
    currentConversationDetail.value = null;
    return newSession;
  }

  function ensureSession(sessionId: string, title?: string) {
    let session = sessions.value.find((s) => s.id === sessionId);
    if (!session) {
      session = {
        id: sessionId,
        title: title || "新对话",
        messages: [],
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      };
      sessions.value.unshift(session);
    }

    currentConversationId.value = session.id;
    if (currentConversationDetail.value?.id !== session.id) {
      currentConversationDetail.value = null;
    }
    return session;
  }

  function promoteSession(sessionId: string, targetSessionId: string) {
    if (sessionId === targetSessionId) {
      return sessions.value.find((s) => s.id === targetSessionId) || null;
    }

    const session = sessions.value.find((s) => s.id === sessionId);
    if (!session) {
      return null;
    }

    const existingTarget = sessions.value.find((s) => s.id === targetSessionId);
    if (existingTarget) {
      existingTarget.messages = [...existingTarget.messages, ...session.messages];
      existingTarget.updatedAt = new Date().toISOString();
      sessions.value = sessions.value.filter((s) => s.id !== sessionId);
      if (currentConversationId.value === sessionId) {
        currentConversationId.value = targetSessionId;
      }
      if (currentConversationDetail.value?.id !== targetSessionId) {
        currentConversationDetail.value = null;
      }
      return existingTarget;
    }

    session.id = targetSessionId;
    session.updatedAt = new Date().toISOString();
    if (currentConversationId.value === sessionId) {
      currentConversationId.value = targetSessionId;
    }
    if (currentConversationDetail.value?.id !== targetSessionId) {
      currentConversationDetail.value = null;
    }

    return session;
  }

  function addMessage(conversationId: string, message: ChatMessage) {
    const session = ensureSession(conversationId);
    session.messages.push(message);
    session.updatedAt = new Date().toISOString();

    if (
      currentConversationDetail.value &&
      currentConversationDetail.value.id === conversationId
    ) {
      currentConversationDetail.value.messages.push({
        id: message.id,
        role: message.role,
        content: message.content,
        status: "completed",
        createdAt: message.timestamp,
      });
    }
  }

  function updateMessage(
    sessionId: string,
    messageIndex: number,
    content: string,
    isComplete: boolean = false,
  ) {
    const session = ensureSession(sessionId);
    if (session.messages[messageIndex]) {
      const existingMessage = session.messages[messageIndex];
      session.messages[messageIndex] = {
        ...existingMessage,
        content: mergeStreamingContent(
          existingMessage.content,
          content,
          isComplete,
        ),
        status: isComplete ? "completed" : "streaming",
        completedAt: isComplete
          ? new Date().toISOString()
          : existingMessage.completedAt,
      };
      session.updatedAt = new Date().toISOString();
    }
  }

  function deleteSession(sessionId: string) {
    const index = sessions.value.findIndex((s) => s.id === sessionId);
    if (index > -1) {
      sessions.value.splice(index, 1);
      if (currentConversationId.value === sessionId) {
        currentConversationId.value = sessions.value[0]?.id || null;
      }
    }
  }

  function clearSessions() {
    sessions.value = [];
    currentConversationId.value = null;
  }

  return {
    sessions,
    currentConversationId,
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
    ensureSession,
    promoteSession,
    addMessage,
    updateMessage,
    deleteSession,
    clearSessions,
  };
});
