<template>
  <div class="document-list">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-upload
        ref="uploadRef"
        :action="uploadUrl"
        :headers="uploadHeaders"
        :data="uploadData"
        :show-file-list="false"
        :before-upload="beforeUpload"
        accept=".pdf,.doc,.docx,.ppt,.pptx,.txt,.md,.jpg,.jpeg,.png,.gif,.bmp,.mp4,.mp3"
        :auto-upload="false"
        :on-change="handleFileChange"
        drag
        multiple
      >
        <el-button type="primary" :loading="uploading">
          <el-icon><Upload /></el-icon> 上传文档
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

    <!-- 文档列表 -->
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
          <div class="doc-title">{{ doc.title }}</div>
          <div class="doc-meta">
            <el-tag :type="getStatusType(doc.status)" size="small">
              {{ getStatusLabel(doc.status) }}
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
                <el-dropdown-item command="download">
                  <el-icon><Download /></el-icon> 下载
                </el-dropdown-item>
                <el-dropdown-item command="move">
                  <el-icon><Sort /></el-icon> 移动
                </el-dropdown-item>
                <el-dropdown-item command="delete" divided>
                  <el-icon><Delete /></el-icon> 删除
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>

      <el-empty v-if="!loading && documents.length === 0" description="暂无文档" />
    </div>

    <!-- 分页 -->
    <el-pagination
      v-if="totalCount > 0"
      v-model:current-page="page"
      v-model:page-size="pageSize"
      :total="totalCount"
      layout="prev, pager, next"
      @current-change="loadDocuments"
      class="pagination"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { UploadInstance } from 'element-plus'
import {
  Upload,
  Search,
  Download,
  Sort,
  Delete,
  MoreFilled,
  Document,
  Picture,
  VideoCamera,
  Headset
} from '@element-plus/icons-vue'
import { getDocuments, moveDocument, deleteDocument } from '../api/document'
import type { Document as DocType } from '../types'

interface Props {
  knowledgeBaseId: string
  folderId?: string | null
}

interface Emits {
  (e: 'view', document: DocType): void
}

const props = defineProps<Props>()
const emit = defineEmits<Emits>()

const loading = ref(false)
const uploading = ref(false)
const documents = ref<DocType[]>([])
const searchKeyword = ref('')
const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const draggedDocument = ref<DocType | null>(null)
const uploadRef = ref<UploadInstance>()

const uploadUrl = computed(() => {
  return `${import.meta.env.VITE_API_BASE_URL || '/api'}/api/Document/upload`
})

const uploadHeaders = computed(() => {
  const token = localStorage.getItem('token')
  const tenantId = localStorage.getItem('tenantId')
  const headers: Record<string, string> = {}
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (tenantId) headers['X-Tenant-Id'] = tenantId
  return headers
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

// 加载文档列表
async function loadDocuments() {
  loading.value = true
  try {
    const { items, total } = await getDocuments({
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

// 搜索
function handleSearch() {
  page.value = 1
  loadDocuments()
}

// 上传前验证
function beforeUpload(file: File) {
  const maxSize = 100 * 1024 * 1024 // 100MB
  if (file.size > maxSize) {
    ElMessage.error('文件大小不能超过 100MB')
    return false
  }
  return true
}

// 文件选择变化
async function handleFileChange(file: any, fileList: any[]) {
  // 只处理当前触发事件的文件（避免重复处理整个文件列表）
  if (file.status !== 'ready') {
    return
  }

  // 验证文件大小
  const maxSize = 100 * 1024 * 1024 // 100MB
  if (file.size > maxSize) {
    ElMessage.error(`${file.name} 文件大小超过 100MB`)
    return
  }

  uploading.value = true

  // 上传当前文件
  await uploadSingleFile(file)

  uploading.value = false

  // 检查是否所有文件都已上传完成，如果是则清空文件列表
  const remainingFiles = uploadRef.value?.uploadFiles || []
  const allUploaded = remainingFiles.every((f: any) => f.status !== 'ready')
  if (allUploaded) {
    uploadRef.value?.clearFiles()
  }
}

// 上传单个文件
async function uploadSingleFile(file: any) {
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
      const error = await response.json()
      throw new Error(error.message || '上传失败')
    }

    ElMessage.success(`${file.name} 上传成功`)
  } catch (error: any) {
    ElMessage.error(`${file.name} 上传失败: ${error.message}`)
  }

  // 刷新文档列表
  await loadDocuments()
}

// 查看文档
function handleViewDocument(doc: DocType) {
  emit('view', doc)
}

// 命令处理
async function handleCommand(command: string, doc: DocType) {
  switch (command) {
    case 'download':
      handleDownload(doc)
      break
    case 'move':
      // 触发移动模式，需要父组件配合
      ElMessage.info('请拖拽文档到目标文件夹')
      break
    case 'delete':
      await handleDelete(doc)
      break
  }
}

// 下载文档
function handleDownload(doc: DocType) {
  // TODO: 实现下载逻辑
  ElMessage.info('下载功能待实现')
}

// 删除文档
async function handleDelete(doc: DocType) {
  try {
    await ElMessageBox.confirm(`确定要删除文档"${doc.title}"吗？`, '提示', {
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

// 拖拽开始
function handleDragStart(doc: DocType) {
  draggedDocument.value = doc
}

// 拖放处理
async function handleDrop(e: DragEvent) {
  e.preventDefault()
  // 这个组件不处理拖放，拖放逻辑在文件夹树组件中处理
}

// 获取文件图标
function getFileIcon(contentType: number) {
  switch (contentType) {
    case 1: return Document // PDF
    case 2: return Document // DOCX
    case 3: return Document // PPTX
    case 4: return Document // Markdown
    case 5: return Document // Web
    case 6: return Picture // Image
    case 7: return VideoCamera // Video
    case 8: return Headset // Audio
    default: return Document
  }
}

// 获取文件图标颜色
function getFileIconColor(contentType: number) {
  switch (contentType) {
    case 1: return '#d14f28' // PDF - 红色
    case 2: return '#295497' // DOCX - 蓝色
    case 3: return '#d24726' // PPTX - 橙色
    case 4: return '#083fa1' // Markdown - 深蓝
    case 6: return '#67c23a' // Image - 绿色
    case 7: return '#e6a23c' // Video - 黄色
    case 8: return '#909399' // Audio - 灰色
    default: return '#409eff'
  }
}

// 获取状态类型
function getStatusType(status: number): 'success' | 'info' | 'warning' | 'danger' {
  switch (status) {
    case 1: return 'info' // Uploaded
    case 2: return 'warning' // Parsing
    case 3: return 'info' // Parsed
    case 4: return 'warning' // Indexing
    case 5: return 'success' // Indexed
    case 6: return 'danger' // Failed
    default: return 'info'
  }
}

// 获取状态标签
function getStatusLabel(status: number): string {
  const labels: Record<number, string> = {
    1: '已上传',
    2: '解析中',
    3: '已解析',
    4: '索引中',
    5: '已完成',
    6: '失败'
  }
  return labels[status] || '未知'
}

// 格式化日期
function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('zh-CN')
}

// 监听 folderId 变化
watch(() => props.folderId, () => {
  page.value = 1
  loadDocuments()
})

// 暴露方法
defineExpose({
  loadDocuments,
  refresh: loadDocuments
})

// 初始加载
loadDocuments()
</script>

<style scoped lang="scss">
.document-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;

  .toolbar {
    display: flex;
    gap: 12px;
    align-items: center;

    .search-input {
      flex: 1;
      max-width: 300px;
    }
  }

  .list-container {
    flex: 1;
    overflow-y: auto;
    min-height: 200px;

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

      .doc-icon {
        flex-shrink: 0;
      }

      .doc-info {
        flex: 1;
        min-width: 0;

        .doc-title {
          font-size: 14px;
          font-weight: 500;
          margin-bottom: 4px;
          white-space: nowrap;
          overflow: hidden;
          text-overflow: ellipsis;
        }

        .doc-meta {
          display: flex;
          align-items: center;
          gap: 8px;
          font-size: 12px;
          color: #909399;

          .doc-time {
            font-size: 12px;
          }
        }
      }

      .doc-actions {
        flex-shrink: 0;
      }
    }
  }

  .pagination {
    display: flex;
    justify-content: center;
  }
}
</style>
