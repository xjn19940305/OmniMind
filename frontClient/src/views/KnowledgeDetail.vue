<template>
  <div class="knowledge-detail">
    <!-- 头部信息 -->
    <el-page-header @back="goBack" class="header">
      <template #content>
        <div class="header-content">
          <el-icon class="kb-icon">
            <User v-if="knowledgeBase?.visibility === 1" />
            <Collection v-else />
          </el-icon>
          <span class="kb-name">{{ knowledgeBase?.name }}</span>
          <el-tag v-if="knowledgeBase?.visibility === 1" type="warning" size="small" class="ml-2">个人知识库</el-tag>
          <el-tag v-else type="success" size="small" class="ml-2">共享知识库</el-tag>
          <el-tag :type="getVisibilityType(knowledgeBase?.visibility || 1)" size="small" class="ml-2">
            {{ getVisibilityLabel(knowledgeBase?.visibility || 1) }}
          </el-tag>
        </div>
      </template>
      <template #extra>
        <el-button @click="showMembersDialog = true">
          <el-icon><User /></el-icon> 成员管理
        </el-button>
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
        <el-form-item label="类型">
          <el-tag v-if="knowledgeBase.visibility === 1" type="warning">个人知识库（私有）</el-tag>
          <el-tag v-else-if="knowledgeBase.visibility === 2" type="success">共享知识库（内部）</el-tag>
          <el-tag v-else type="info">共享知识库（公开）</el-tag>
        </el-form-item>
        <el-form-item label="名称">
          <el-input :value="knowledgeBase.name" disabled />
        </el-form-item>
        <el-form-item label="拥有者">
          <span>{{ knowledgeBase.ownerName || '-' }}</span>
        </el-form-item>
        <el-form-item label="成员数量">
          <span>{{ knowledgeBase.memberCount || 0 }}</span>
        </el-form-item>
        <el-form-item label="创建时间">
          <span>{{ formatDate(knowledgeBase.createdAt) }}</span>
        </el-form-item>
        <el-form-item label="描述">
          <span>{{ knowledgeBase.description || '-' }}</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showSettingsDialog = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 成员管理对话框 -->
    <el-dialog v-model="showMembersDialog" :title="`成员管理 - ${knowledgeBase?.name || ''}`" width="700px">
      <div class="members-header">
        <el-input
          v-model="memberForm.userId"
          placeholder="输入用户ID"
          clearable
          style="width: 300px"
        >
          <template #prefix>
            <el-icon><User /></el-icon>
          </template>
        </el-input>
        <el-select v-model="memberForm.role" placeholder="选择角色" style="width: 150px; margin-left: 10px">
          <el-option :value="1" label="管理员" />
          <el-option :value="2" label="编辑" />
          <el-option :value="3" label="查看者" />
        </el-select>
        <el-button type="primary" :loading="addingMember" @click="handleAddMember">
          添加成员
        </el-button>
      </div>

      <el-table v-loading="loadingMembers" :data="members" stripe max-height="400">
        <el-table-column prop="userId" label="用户ID" width="200" />
        <el-table-column prop="userName" label="用户名" width="150" />
        <el-table-column prop="role" label="角色" width="150">
          <template #default="{ row }">
            <el-tag :type="getMemberRoleType(row.role)" size="small">
              {{ getMemberRoleLabel(row.role) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="createdAt" label="加入时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="100">
          <template #default="{ row }">
            <el-button
              link
              type="danger"
              @click="handleRemoveMember(row)"
            >
              移除
            </el-button>
          </template>
        </el-table-column>
      </el-table>
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
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Folder,
  Setting,
  Refresh,
  Upload,
  User,
  Collection
} from '@element-plus/icons-vue'
import { getKnowledgeBase } from '../api/knowledge'
import {
  getKnowledgeBaseMembers,
  addKnowledgeBaseMember,
  removeKnowledgeBaseMember
} from '../api/knowledge'
import FolderTree from '../components/FolderTree.vue'
import DocumentList from '../components/DocumentList.vue'
import type { KnowledgeBaseDetail, KnowledgeBaseMember, Folder as FolderType, Document, KnowledgeBaseMemberRole } from '../types'

const route = useRoute()
const router = useRouter()

const knowledgeBaseId = computed(() => route.params.id as string)
const knowledgeBase = ref<KnowledgeBaseDetail | null>(null)
const selectedFolderId = ref<string | null>(null)
const currentFolder = ref<FolderType | null>(null)
const currentDocument = ref<Document | null>(null)
const members = ref<KnowledgeBaseMember[]>([])

const folderTreeRef = ref()
const documentListRef = ref()
const showSettingsDialog = ref(false)
const showPreviewDialog = ref(false)
const showMembersDialog = ref(false)
const loadingMembers = ref(false)
const addingMember = ref(false)

const memberForm = reactive({
  userId: '',
  role: 3 as KnowledgeBaseMemberRole
})

// 加载知识库详情
async function loadKnowledgeBase() {
  try {
    const data = await getKnowledgeBase(knowledgeBaseId.value)
    knowledgeBase.value = data
  } catch (error: any) {
    console.error('Failed to load knowledge base:', error)
  }
}

// 加载成员列表
async function loadMembers() {
  loadingMembers.value = true
  try {
    members.value = await getKnowledgeBaseMembers(knowledgeBaseId.value)
  } catch (error: any) {
    console.error('Failed to load members:', error)
  } finally {
    loadingMembers.value = false
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

// 添加成员
async function handleAddMember() {
  if (!memberForm.userId) {
    ElMessage.warning('请输入用户ID')
    return
  }

  addingMember.value = true
  try {
    await addKnowledgeBaseMember(knowledgeBaseId.value, {
      userId: memberForm.userId,
      role: memberForm.role
    })
    ElMessage.success('添加成功')
    memberForm.userId = ''
    memberForm.role = 3
    await loadMembers()
    // 重新加载知识库信息以更新成员数量
    await loadKnowledgeBase()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '添加成员失败')
  } finally {
    addingMember.value = false
  }
}

// 移除成员
async function handleRemoveMember(member: KnowledgeBaseMember) {
  try {
    await ElMessageBox.confirm(
      `确定要移除成员"${member.userName || member.userId}"吗？`,
      '移除确认',
      {
        type: 'warning',
        confirmButtonText: '确定',
        cancelButtonText: '取消'
      }
    )

    await removeKnowledgeBaseMember(knowledgeBaseId.value, member.userId)
    ElMessage.success('移除成功')
    await loadMembers()
    // 重新加载知识库信息以更新成员数量
    await loadKnowledgeBase()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '移除成员失败')
    }
  }
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

// 获取成员角色类型
function getMemberRoleType(role: number): 'success' | 'info' | 'warning' | 'danger' {
  const types: Record<number, 'success' | 'info' | 'warning' | 'danger'> = {
    1: 'danger',
    2: 'warning',
    3: 'info'
  }
  return types[role] || 'info'
}

// 获取成员角色标签
function getMemberRoleLabel(role: number): string {
  const labels: Record<number, string> = {
    1: '管理员',
    2: '编辑',
    3: '查看者'
  }
  return labels[role] || '未知'
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

  .members-header {
    display: flex;
    align-items: center;
    margin-bottom: 16px;
  }
}
</style>
