<template>
  <div class="knowledge-detail">
    <!-- 头部信息 -->
    <el-page-header @back="goBack" class="header">
      <template #content>
        <div class="header-content">
          <el-icon class="kb-icon"><Folder /></el-icon>
          <span class="kb-name">{{ knowledgeBase?.name }}</span>
          <el-tag :type="getVisibilityType(knowledgeBase?.visibility || 2)" size="small" class="ml-2">
            {{ getVisibilityLabel(knowledgeBase?.visibility || 2) }}
          </el-tag>
        </div>
      </template>
      <template #extra>
        <el-button @click="showSettingsDialog = true">
          <el-icon><Setting /></el-icon> 设置
        </el-button>
      </template>
    </el-page-header>

    <!-- 描述信息 -->
    <el-card v-if="knowledgeBase?.description" class="description-card" shadow="never">
      <p>{{ knowledgeBase.description }}</p>
    </el-card>

    <!-- 主内容区 -->
    <el-row :gutter="20" class="main-content">
      <!-- 左侧：文件夹树 -->
      <el-col :span="6">
        <el-card class="folder-tree-card" shadow="never">
          <template #header>
            <div class="card-header">
              <span>文件夹</span>
              <el-button text @click="folderTreeRef?.refresh()">
                <el-icon><Refresh /></el-icon>
              </el-button>
            </div>
          </template>
          <FolderTree
            ref="folderTreeRef"
            :knowledge-base-id="knowledgeBaseId"
            @select="handleSelectFolder"
            @add-document="handleAddDocument"
          />
        </el-card>
      </el-col>

      <!-- 右侧：文档列表 -->
      <el-col :span="18">
        <el-card class="document-list-card" shadow="never">
          <template #header>
            <div class="card-header">
              <span>
                {{ currentFolder ? currentFolder.name : '全部文档' }}
                <el-tag v-if="currentFolder" size="small" class="ml-2">
                  {{ currentFolder.documentCount || 0 }} 个文档
                </el-tag>
              </span>
              <div class="header-actions">
                <el-button type="primary" size="small" @click="handleAddDocument(currentFolder?.id)">
                  <el-icon><Upload /></el-icon> 上传文档
                </el-button>
              </div>
            </div>
          </template>
          <DocumentList
            ref="documentListRef"
            :knowledge-base-id="knowledgeBaseId"
            :folder-id="selectedFolderId"
            @view="handleViewDocument"
          />
        </el-card>
      </el-col>
    </el-row>

    <!-- 知识库设置对话框 -->
    <el-dialog v-model="showSettingsDialog" title="知识库设置" width="600px">
      <el-form v-if="knowledgeBase" label-width="100px">
        <el-form-item label="知识库ID">
          <el-input :value="knowledgeBase.id" disabled />
        </el-form-item>
        <el-form-item label="名称">
          <el-input :value="knowledgeBase.name" disabled />
        </el-form-item>
        <el-form-item label="可见性">
          <el-tag :type="getVisibilityType(knowledgeBase.visibility)">
            {{ getVisibilityLabel(knowledgeBase.visibility) }}
          </el-tag>
        </el-form-item>
        <el-form-item label="创建时间">
          <span>{{ formatDate(knowledgeBase.createdAt) }}</span>
        </el-form-item>
        <el-form-item label="挂载的工作空间">
          <el-tag
            v-for="ws in knowledgeBase.workspaces"
            :key="ws.id"
            class="mr-2"
          >
            {{ ws.aliasName || ws.name }}
          </el-tag>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showSettingsDialog = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 文档预览对话框（可选） -->
    <el-dialog v-model="showPreviewDialog" title="文档预览" width="80%" fullscreen>
      <div v-if="currentDocument" class="document-preview">
        <h2>{{ currentDocument.title }}</h2>
        <el-descriptions :column="2" border class="mt-4">
          <el-descriptions-item label="状态">
            <el-tag :type="getStatusType(currentDocument.status)">
              {{ getStatusLabel(currentDocument.status) }}
            </el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="文件类型">
            {{ getContentTypeLabel(currentDocument.contentType) }}
          </el-descriptions-item>
          <el-descriptions-item label="创建时间">
            {{ formatDate(currentDocument.createdAt) }}
          </el-descriptions-item>
          <el-descriptions-item label="切片数量">
            {{ currentDocument.chunkCount || 0 }}
          </el-descriptions-item>
        </el-descriptions>
        <el-divider />
        <div class="preview-content">
          <el-empty description="文档预览功能待实现" />
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Folder,
  Setting,
  Refresh,
  Upload
} from '@element-plus/icons-vue'
import { getKnowledgeBase } from '../api/knowledge'
import FolderTree from '../components/FolderTree.vue'
import DocumentList from '../components/DocumentList.vue'
import type { KnowledgeBase, Folder as FolderType, Document } from '../types'

const route = useRoute()
const router = useRouter()

const knowledgeBaseId = computed(() => route.params.id as string)
const knowledgeBase = ref<KnowledgeBase | null>(null)
const selectedFolderId = ref<string | null>(null)
const currentFolder = ref<FolderType | null>(null)
const currentDocument = ref<Document | null>(null)

const folderTreeRef = ref()
const documentListRef = ref()
const showSettingsDialog = ref(false)
const showPreviewDialog = ref(false)

// 加载知识库详情
async function loadKnowledgeBase() {
  try {
    const data = await getKnowledgeBase(knowledgeBaseId.value)
    knowledgeBase.value = data
  } catch (error: any) {
    console.error('Failed to load knowledge base:', error)
  }
}

// 返回上一页
function goBack() {
  router.push('/knowledge')
}

// 选择文件夹
function handleSelectFolder(folder: any) {
  currentFolder.value = folder
  selectedFolderId.value = folder?.id === 'root' ? null : folder?.id || null
}

// 添加文档（在指定文件夹下）
function handleAddDocument(folderId?: string | null) {
  selectedFolderId.value = folderId || null
  // TODO: 打开上传对话框
  console.log('Add document to folder:', folderId)
}

// 查看文档
function handleViewDocument(doc: Document) {
  currentDocument.value = doc
  showPreviewDialog.value = true
}

// 获取可见性类型
function getVisibilityType(visibility: number): 'success' | 'info' | 'warning' | 'danger' {
  const types: Record<number, 'success' | 'info' | 'warning' | 'danger'> = {
    1: 'danger',
    2: 'info',
    3: 'success'
  }
  return types[visibility] || 'info'
}

// 获取可见性标签
function getVisibilityLabel(visibility: number): string {
  const labels: Record<number, string> = {
    1: '私有',
    2: '内部',
    3: '公开'
  }
  return labels[visibility] || '未知'
}

// 获取状态类型
function getStatusType(status: number): 'success' | 'info' | 'warning' | 'danger' {
  switch (status) {
    case 1: return 'info'
    case 2: return 'warning'
    case 3: return 'info'
    case 4: return 'warning'
    case 5: return 'success'
    case 6: return 'danger'
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

// 获取内容类型标签
function getContentTypeLabel(contentType: number): string {
  const labels: Record<number, string> = {
    1: 'PDF',
    2: 'Word',
    3: 'PPT',
    4: 'Markdown',
    5: '网页',
    6: '图片',
    7: '视频',
    8: '音频'
  }
  return labels[contentType] || '未知'
}

// 格式化日期
function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('zh-CN')
}

onMounted(() => {
  loadKnowledgeBase()
})
</script>

<style scoped lang="scss">
.knowledge-detail {
  display: flex;
  flex-direction: column;
  gap: 20px;

  .header {
    background: white;
    padding: 16px;
    border-radius: 8px;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);

    .header-content {
      display: flex;
      align-items: center;
      gap: 12px;

      .kb-icon {
        font-size: 24px;
        color: #409eff;
      }

      .kb-name {
        font-size: 18px;
        font-weight: 500;
      }

      .ml-2 {
        margin-left: 8px;
      }
    }
  }

  .description-card {
    :deep(.el-card__body) {
      padding: 12px 16px;

      p {
        margin: 0;
        color: #606266;
        font-size: 14px;
      }
    }
  }

  .main-content {
    .folder-tree-card,
    .document-list-card {
      height: calc(100vh - 280px);

      :deep(.el-card__body) {
        height: calc(100% - 57px);
        overflow: hidden;
        padding: 16px;
      }
    }

    .card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;

      .header-actions {
        display: flex;
        gap: 8px;
      }
    }

    .mr-2 {
      margin-right: 8px;
    }
  }

  .document-preview {
    h2 {
      margin-bottom: 16px;
    }

    .mt-4 {
      margin-top: 16px;
    }

    .preview-content {
      min-height: 400px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
  }
}
</style>
