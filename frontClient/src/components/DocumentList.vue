<template>
  <div class="document-list">
    <div class="toolbar">
      <el-upload
        ref="uploadRef"
        :action="uploadUrl"
        :headers="uploadHeaders"
        :data="uploadData"
        :show-file-list="false"
        :before-upload="beforeUpload"
        accept=".pdf,.docx,.pptx,.xlsx,.txt,.md,.jpg,.jpeg,.png,.gif,.bmp,.mp4,.mp3"
        :auto-upload="false"
        :on-change="handleFileChange"
        drag
        multiple
      >
        <el-button type="primary" :loading="uploading">
          <el-icon><Upload /></el-icon>
          上传文档
        </el-button>
      </el-upload>

      <el-input
        v-model="searchKeyword"
        placeholder="搜索文档"
        clearable
        @input="handleSearch"
        class="search-input"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
    </div>

    <div
      v-loading="loading"
      class="list-container"
      @drop="handleDrop"
      @dragover.prevent
    >
      <div
        v-for="doc in documents"
        :key="doc.id"
        class="document-item"
        draggable="true"
        @dragstart="handleDragStart(doc)"
        @click="handleViewDocument(doc)"
      >
        <div class="doc-icon">
          <el-icon :size="32" :color="getFileIconColor(doc.contentType)">
            <component :is="getFileIcon(doc.contentType)" />
          </el-icon>
        </div>
        <div class="doc-info">
          <div class="doc-title-row">
            <div class="doc-title">{{ doc.title }}</div>
            <span class="file-type-pill">{{ getDocumentTypeLabel(doc.contentType) }}</span>
          </div>
          <div class="doc-meta">
            <el-tag :type="getDocumentStatusType(doc.status)" size="small">
              {{ getDocumentStatusLabel(doc.status) }}
            </el-tag>
            <span class="doc-time">{{ formatDate(doc.createdAt) }}</span>
          </div>
        </div>
        <div class="doc-actions">
          <el-dropdown @command="(cmd) => handleCommand(cmd, doc)">
            <el-button text>
              <el-icon><MoreFilled /></el-icon>
            </el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="move">
                  <el-icon><Sort /></el-icon>
                  移动
                </el-dropdown-item>
                <el-dropdown-item command="delete" divided>
                  <el-icon><Delete /></el-icon>
                  删除
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>

      <el-empty v-if="!loading && documents.length === 0" description="暂无文档" />
    </div>

    <el-pagination
      v-if="totalCount > 0"
      v-model:current-page="page"
      v-model:page-size="pageSize"
      :total="totalCount"
      layout="prev, pager, next"
      class="pagination"
      @current-change="loadDocuments"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { UploadInstance } from 'element-plus'
import {
  DataBoard,
  Delete,
  Document,
  Grid,
  Headset,
  Link,
  Memo,
  MoreFilled,
  Picture,
  Search,
  Sort,
  Tickets,
  Upload,
  VideoCamera
} from '@element-plus/icons-vue'
import { deleteDocument, getDocuments } from '../api/document'
import type { Document as DocType } from '../types'
import { getDocumentStatusLabel, getDocumentStatusType } from '../utils/documentStatus'
import { getDocumentTypeKey, getDocumentTypeLabel } from '../utils/documentType'
import {
  initSignalR,
  isConnected,
  offDocumentProgress,
  onDocumentProgress,
  type DocumentProgress
} from '../utils/signalr'
import { useUserStore } from '../stores/user'

interface Props {
  knowledgeBaseId: string
  folderId?: string | null
}

interface Emits {
  (e: 'view', document: DocType): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const userStore = useUserStore()

const loading = ref(false)
const uploading = ref(false)
const documents = ref<DocType[]>([])
const searchKeyword = ref('')
const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const draggedDocument = ref<DocType | null>(null)
const uploadRef = ref<UploadInstance>()

const uploadUrl = computed(() => `${import.meta.env.VITE_API_BASE_URL || '/api'}/api/Document/upload`)
const uploadHeaders = computed(() => {
  const token = localStorage.getItem('token')
  return token ? { Authorization: `Bearer ${token}` } : {}
})
const uploadData = computed(() => {
  const data: Record<string, string> = {
    knowledgeBaseId: props.knowledgeBaseId
  }

  if (props.folderId) {
    data.folderId = props.folderId
  }

  return data
})

async function loadDocuments() {
  loading.value = true
  try {
    const { items, totalCount: total } = await getDocuments({
      knowledgeBaseId: props.knowledgeBaseId,
      folderId: props.folderId || undefined,
      page: page.value,
      pageSize: pageSize.value,
      keyword: searchKeyword.value || undefined
    })

    documents.value = items
    totalCount.value = total
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '加载文档失败')
  } finally {
    loading.value = false
  }
}

function handleSearch() {
  page.value = 1
  loadDocuments()
}

function beforeUpload(file: File) {
  if (file.size > 100 * 1024 * 1024) {
    ElMessage.error('文件大小不能超过100MB')
    return false
  }

  return true
}

async function handleFileChange(file: any) {
  if (file.status !== 'ready') {
    return
  }

  uploading.value = true
  try {
    const formData = new FormData()
    formData.append('file', file.raw)
    formData.append('knowledgeBaseId', props.knowledgeBaseId)
    if (props.folderId) {
      formData.append('folderId', props.folderId)
    }

    const response = await fetch(uploadUrl.value, {
      method: 'POST',
      headers: uploadHeaders.value,
      body: formData
    })

    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: '上传失败' }))
      throw new Error(error.message || '上传失败')
    }

    ElMessage.success(`${file.name} 上传成功`)
    await loadDocuments()
  } catch (error: any) {
    ElMessage.error(`${file.name} 上传失败: ${error.message}`)
  } finally {
    uploading.value = false
    uploadRef.value?.clearFiles()
  }
}

function handleViewDocument(doc: DocType) {
  emit('view', doc)
}

async function handleCommand(command: string, doc: DocType) {
  if (command === 'move') {
    ElMessage.info('请在目录树中执行移动操作')
    return
  }

  if (command === 'delete') {
    await handleDelete(doc)
  }
}

async function handleDelete(doc: DocType) {
  try {
    await ElMessageBox.confirm(`确定要删除文档 "${doc.title}" 吗？`, '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })

    await deleteDocument(doc.id)
    ElMessage.success('删除成功')
    await loadDocuments()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '删除失败')
    }
  }
}

function handleDragStart(doc: DocType) {
  draggedDocument.value = doc
}

function handleDrop(event: DragEvent) {
  event.preventDefault()
}

function getFileIcon(contentType: string) {
  switch (getDocumentTypeKey(contentType)) {
    case 'pdf':
      return Tickets
    case 'word':
      return Memo
    case 'ppt':
      return DataBoard
    case 'excel':
      return Grid
    case 'web':
      return Link
    case 'image':
      return Picture
    case 'video':
      return VideoCamera
    case 'audio':
      return Headset
    default:
      return Document
  }
}

function getFileIconColor(contentType: string) {
  switch (getDocumentTypeKey(contentType)) {
    case 'pdf':
      return '#d14f28'
    case 'word':
      return '#295497'
    case 'ppt':
      return '#d24726'
    case 'excel':
      return '#1d6f42'
    case 'markdown':
      return '#7c3aed'
    case 'text':
      return '#4b5563'
    case 'web':
      return '#0f766e'
    case 'image':
      return '#67c23a'
    case 'video':
      return '#e6a23c'
    case 'audio':
      return '#909399'
    default:
      return '#409eff'
  }
}

function getStatusType(status: number): 'success' | 'info' | 'warning' | 'danger' {
  switch (status) {
    case 0: return 'info'
    case 1: return 'info'
    case 2: return 'warning'
    case 3: return 'info'
    case 4: return 'warning'
    case 5: return 'success'
    case 6: return 'danger'
    default: return 'info'
  }
}

function getStatusLabel(status: number) {
  const labels: Record<number, string> = {
    0: '待处理',
    1: '已上传',
    2: '解析中',
    3: '已解析',
    4: '向量化中',
    5: '已向量化',
    6: '失败'
  }
  return labels[status] || '未知'
}

function formatDate(dateStr: string) {
  return new Date(dateStr).toLocaleString('zh-CN')
}

function handleDocumentProgress(progress: DocumentProgress) {
  const docIndex = documents.value.findIndex((doc) => doc.id === progress.documentId)
  if (docIndex === -1) {
    return
  }

  const statusMap: Record<string, number> = {
    Pending: 0,
    Uploaded: 1,
    Parsing: 2,
    Parsed: 3,
    Indexing: 4,
    Indexed: 5,
    Failed: 6
  }

  documents.value = documents.value.map((doc, index) =>
    index === docIndex ? { ...doc, status: statusMap[progress.status] || doc.status } : doc
  )
}

async function initializeSignalR() {
  if (!userStore.userInfo?.id) {
    return
  }

  if (!isConnected()) {
    await initSignalR()
  }

  onDocumentProgress(handleDocumentProgress)
}

watch(() => props.folderId, () => {
  page.value = 1
  loadDocuments()
})

onMounted(async () => {
  await initializeSignalR()
  await loadDocuments()
})

onUnmounted(() => {
  offDocumentProgress()
})

defineExpose({
  loadDocuments,
  refresh: loadDocuments
})
</script>

<style scoped lang="scss">
.document-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  overflow-x: hidden;
}

.toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  max-width: 300px;
}

.list-container {
  flex: 1;
  overflow-y: auto;
  min-height: 200px;
}

.document-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: 8px;

  &:hover {
    border-color: #409eff;
    background: #f5f7fa;
  }
}

.doc-info {
  flex: 1;
  min-width: 0;
}

.doc-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
  min-width: 0;
}

.doc-title {
  font-size: 14px;
  font-weight: 500;
  min-width: 0;
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.file-type-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 2px 8px;
  border-radius: 999px;
  background: rgba(15, 23, 42, 0.06);
  color: #475569;
  font-size: 12px;
  line-height: 1.6;
  white-space: nowrap;
  flex-shrink: 0;
}

.doc-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 12px;
  color: #909399;
}

.pagination {
  display: flex;
  justify-content: center;
}
</style>
