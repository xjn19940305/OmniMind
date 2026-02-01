<template>
  <div class="knowledge-container">
    <el-card class="knowledge-card">
      <template #header>
        <div class="card-header">
          <span>知识库管理</span>
          <el-button type="primary" @click="showCreateDialog = true">
            <el-icon><Plus /></el-icon>
            创建知识库
          </el-button>
        </div>
      </template>

      <!-- Search and Filter -->
      <div class="filter-bar">
        <el-input
          v-model="searchKeyword"
          placeholder="搜索知识库名称或描述"
          clearable
          @clear="handleSearch"
          @keyup.enter="handleSearch"
          class="search-input"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        <el-select v-model="visibilityFilter" placeholder="可见性" clearable @change="handleSearch" class="filter-select">
          <el-option label="私有" :value="1" />
          <el-option label="内部" :value="2" />
          <el-option label="公开" :value="3" />
        </el-select>
      </div>

      <!-- Knowledge Base List -->
      <div class="kb-list" v-loading="loading">
        <div
          v-for="kb in knowledgeBases"
          :key="kb.id"
          class="kb-item"
          :class="{ active: kb.id === activeKbId }"
          @click="handleSelectKb(kb.id)"
        >
          <div class="kb-info">
            <el-icon class="kb-icon" size="32"><Folder /></el-icon>
            <div class="kb-details">
              <div class="kb-name">{{ kb.name }}</div>
              <div class="kb-meta">
                <el-tag :type="getVisibilityType(kb.visibility)" size="small">
                  {{ getVisibilityLabel(kb.visibility) }}
                </el-tag>
                <span class="workspace-count">{{ kb.workspaceCount }} 个工作空间</span>
              </div>
              <div v-if="kb.description" class="kb-desc">{{ kb.description }}</div>
            </div>
          </div>
          <el-dropdown @command="(cmd) => handleKbCommand(cmd, kb)">
            <el-button text>
              <el-icon><MoreFilled /></el-icon>
            </el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item command="open">
                  <el-icon><FolderOpened /></el-icon> 打开
                </el-dropdown-item>
                <el-dropdown-item command="edit">
                  <el-icon><Edit /></el-icon> 编辑
                </el-dropdown-item>
                <el-dropdown-item command="mount">
                  <el-icon><Link /></el-icon> 挂载到工作空间
                </el-dropdown-item>
                <el-dropdown-item command="delete" divided>
                  <el-icon><Delete /></el-icon> 删除
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>

      <!-- Pagination -->
      <el-pagination
        v-if="totalCount > 0"
        v-model:current-page="page"
        v-model:page-size="pageSize"
        :total="totalCount"
        layout="prev, pager, next"
        @current-change="loadKnowledgeBases"
        class="pagination"
      />
    </el-card>

    <!-- Knowledge Base Detail -->
    <el-card v-if="activeKb" class="detail-card">
      <template #header>
        <div class="card-header">
          <div>
            <span class="kb-title">{{ activeKb.name }}</span>
            <el-tag :type="getVisibilityType(activeKb.visibility)" size="small" class="ml-2">
              {{ getVisibilityLabel(activeKb.visibility) }}
            </el-tag>
          </div>
          <el-button @click="activeKbId = null">关闭</el-button>
        </div>
      </template>

      <el-descriptions :column="2" border>
        <el-descriptions-item label="知识库ID">{{ activeKb.id }}</el-descriptions-item>
        <el-descriptions-item label="可见性">
          {{ getVisibilityLabel(activeKb.visibility) }}
        </el-descriptions-item>
        <el-descriptions-item label="创建时间">
          {{ formatDate(activeKb.createdAt) }}
        </el-descriptions-item>
        <el-descriptions-item label="更新时间">
          {{ activeKb.updatedAt ? formatDate(activeKb.updatedAt) : '-' }}
        </el-descriptions-item>
        <el-descriptions-item label="工作空间数量" :span="2">
          {{ activeKb.workspaceCount }}
        </el-descriptions-item>
        <el-descriptions-item label="描述" :span="2">
          {{ activeKb.description || '-' }}
        </el-descriptions-item>
      </el-descriptions>

      <!-- Workspaces -->
      <div v-if="activeKb.workspaces && activeKb.workspaces.length > 0" class="workspaces-section">
        <h4>挂载的工作空间</h4>
        <div class="workspace-tags">
          <el-tag
            v-for="ws in activeKb.workspaces"
            :key="ws.id"
            closable
            @close="handleUnmount(ws.id)"
          >
            {{ ws.aliasName || ws.name }}
          </el-tag>
        </div>
      </div>
    </el-card>

    <el-empty v-else description="请选择或创建知识库" :image-size="200" />

    <!-- Create KB Dialog -->
    <el-dialog v-model="showCreateDialog" title="创建知识库" width="500px">
      <el-form ref="createFormRef" :model="createForm" :rules="createRules" label-width="100px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="createForm.name" placeholder="请输入知识库名称" maxlength="128" show-word-limit />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="createForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入描述（可选）"
            maxlength="1000"
            show-word-limit
          />
        </el-form-item>
        <el-form-item label="可见性" prop="visibility">
          <el-radio-group v-model="createForm.visibility">
            <el-radio :label="1">私有</el-radio>
            <el-radio :label="2">内部</el-radio>
            <el-radio :label="3">公开</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateDialog = false">取消</el-button>
        <el-button type="primary" :loading="createLoading" @click="handleCreate">
          创建
        </el-button>
      </template>
    </el-dialog>

    <!-- Edit KB Dialog -->
    <el-dialog v-model="showEditDialog" title="编辑知识库" width="500px">
      <el-form ref="editFormRef" :model="editForm" :rules="editRules" label-width="100px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="editForm.name" placeholder="请输入知识库名称" maxlength="128" show-word-limit />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="editForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入描述（可选）"
            maxlength="1000"
            show-word-limit
          />
        </el-form-item>
        <el-form-item label="可见性" prop="visibility">
          <el-radio-group v-model="editForm.visibility">
            <el-radio :label="1">私有</el-radio>
            <el-radio :label="2">内部</el-radio>
            <el-radio :label="3">公开</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showEditDialog = false">取消</el-button>
        <el-button type="primary" :loading="editLoading" @click="handleEdit">
          保存
        </el-button>
      </template>
    </el-dialog>

    <!-- Mount Dialog -->
    <el-dialog v-model="showMountDialog" title="挂载到工作空间" width="500px">
      <el-form ref="mountFormRef" :model="mountForm" :rules="mountRules" label-width="100px">
        <el-form-item label="工作空间" prop="workspaceId">
          <el-select v-model="mountForm.workspaceId" placeholder="请选择工作空间" style="width: 100%">
            <el-option
              v-for="ws in availableWorkspaces"
              :key="ws.id"
              :label="ws.name"
              :value="ws.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="别名" prop="aliasName">
          <el-input v-model="mountForm.aliasName" placeholder="可选，在此工作空间中显示的别名" />
        </el-form-item>
        <el-form-item label="排序" prop="sortOrder">
          <el-input-number v-model="mountForm.sortOrder" :min="0" :max="9999" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showMountDialog = false">取消</el-button>
        <el-button type="primary" :loading="mountLoading" @click="handleMount">
          挂载
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import {
  Plus,
  Folder,
  FolderOpened,
  MoreFilled,
  Edit,
  Delete,
  Link,
  Search
} from '@element-plus/icons-vue'
import {
  getKnowledgeBases,
  getKnowledgeBase,
  createKnowledgeBase,
  updateKnowledgeBase,
  deleteKnowledgeBase,
  mountKnowledgeBase,
  unmountKnowledgeBase
} from '../api/knowledge'
import { getWorkspaces } from '../api/workspace'
import type { KnowledgeBase, Workspace, Visibility } from '../types'

const router = useRouter()

const loading = ref(false)
const createLoading = ref(false)
const editLoading = ref(false)
const mountLoading = ref(false)

const knowledgeBases = ref<KnowledgeBase[]>([])
const availableWorkspaces = ref<Workspace[]>([])
const activeKbId = ref<string | null>(null)

const searchKeyword = ref('')
const visibilityFilter = ref<number | null>(null)

const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const showMountDialog = ref(false)

const createFormRef = ref<FormInstance>()
const editFormRef = ref<FormInstance>()
const mountFormRef = ref<FormInstance>()

const createForm = reactive({
  name: '',
  description: '',
  visibility: 2 as Visibility
})

const editForm = reactive({
  name: '',
  description: '',
  visibility: 2 as Visibility
})

const mountForm = reactive({
  workspaceId: null as string | null,
  aliasName: '',
  sortOrder: 0
})

const currentKb = ref<KnowledgeBase | null>(null)

const activeKb = computed(() => {
  return knowledgeBases.value.find(kb => kb.id === activeKbId.value) || null
})

const createRules: FormRules = {
  name: [
    { required: true, message: '请输入知识库名称', trigger: 'blur' },
    { max: 128, message: '名称长度不能超过128个字符', trigger: 'blur' }
  ]
}

const editRules: FormRules = {
  name: [
    { required: true, message: '请输入知识库名称', trigger: 'blur' },
    { max: 128, message: '名称长度不能超过128个字符', trigger: 'blur' }
  ]
}

const mountRules: FormRules = {
  workspaceId: [{ required: true, message: '请选择工作空间', trigger: 'change' }]
}

async function loadKnowledgeBases() {
  loading.value = true
  try {
    const { items, total } = await getKnowledgeBases({
      page: page.value,
      pageSize: pageSize.value,
      keyword: searchKeyword.value || undefined,
      visibility: visibilityFilter.value ?? undefined
    })
    knowledgeBases.value = items
    totalCount.value = total
  } catch (error) {
    console.error('Failed to load knowledge bases:', error)
  } finally {
    loading.value = false
  }
}

async function loadWorkspaces() {
  try {
    const { items } = await getWorkspaces({ pageSize: 100 })
    availableWorkspaces.value = items
  } catch (error) {
    console.error('Failed to load workspaces:', error)
  }
}

function handleSearch() {
  page.value = 1
  loadKnowledgeBases()
}

async function handleSelectKb(id: string) {
  if (activeKbId.value === id) {
    activeKbId.value = null
    return
  }

  activeKbId.value = id
  if (!activeKb.value?.workspaces) {
    try {
      const kb = await getKnowledgeBase(id)
      const index = knowledgeBases.value.findIndex(k => k.id === id)
      if (index !== -1) {
        knowledgeBases.value[index] = kb
      }
    } catch (error) {
      console.error('Failed to load knowledge base detail:', error)
    }
  }
}

async function handleCreate() {
  if (!createFormRef.value) return

  await createFormRef.value.validate(async (valid) => {
    if (valid) {
      createLoading.value = true
      try {
        const kb = await createKnowledgeBase({
          name: createForm.name,
          description: createForm.description || undefined,
          visibility: createForm.visibility
        })
        await loadKnowledgeBases()
        ElMessage.success('知识库创建成功')
        showCreateDialog.value = false

        // Reset form
        createForm.name = ''
        createForm.description = ''
        createForm.visibility = 2
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '创建失败')
        console.error('Failed to create knowledge base:', error)
      } finally {
        createLoading.value = false
      }
    }
  })
}

async function handleEdit() {
  if (!editFormRef.value || !currentKb.value) return

  await editFormRef.value.validate(async (valid) => {
    if (valid) {
      editLoading.value = true
      try {
        await updateKnowledgeBase(currentKb.value.id, {
          name: editForm.name,
          description: editForm.description || undefined,
          visibility: editForm.visibility
        })
        // 重新加载列表和详情
        await loadKnowledgeBases()
        if (activeKbId.value === currentKb.value.id) {
          const kb = await getKnowledgeBase(currentKb.value.id)
          const index = knowledgeBases.value.findIndex(k => k.id === currentKb.value!.id)
          if (index !== -1) {
            knowledgeBases.value[index] = kb
          }
        }
        ElMessage.success('知识库更新成功')
        showEditDialog.value = false
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '更新失败')
        console.error('Failed to update knowledge base:', error)
      } finally {
        editLoading.value = false
      }
    }
  })
}

async function handleMount() {
  if (!mountFormRef.value || !currentKb.value) return

  await mountFormRef.value.validate(async (valid) => {
    if (valid) {
      mountLoading.value = true
      try {
        await mountKnowledgeBase(currentKb.value.id, {
          workspaceId: mountForm.workspaceId!,
          aliasName: mountForm.aliasName || undefined,
          sortOrder: mountForm.sortOrder
        })
        await loadKnowledgeBases()
        // Reload detail
        const kb = await getKnowledgeBase(currentKb.value.id)
        const index = knowledgeBases.value.findIndex(k => k.id === currentKb.value!.id)
        if (index !== -1) {
          knowledgeBases.value[index] = kb
        }
        ElMessage.success('挂载成功')
        showMountDialog.value = false
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '挂载失败')
        console.error('Failed to mount knowledge base:', error)
      } finally {
        mountLoading.value = false
      }
    }
  })
}

async function handleUnmount(workspaceId: string) {
  if (!currentKb.value) return

  ElMessageBox.confirm('确定要从此工作空间卸载该知识库吗？', '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  })
    .then(async () => {
      try {
        await unmountKnowledgeBase(currentKb.value!.id, workspaceId)
        await loadKnowledgeBases()
        const kb = await getKnowledgeBase(currentKb.value!.id)
        const index = knowledgeBases.value.findIndex(k => k.id === currentKb.value!.id)
        if (index !== -1) {
          knowledgeBases.value[index] = kb
        }
        ElMessage.success('卸载成功')
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '卸载失败')
        console.error('Failed to unmount knowledge base:', error)
      }
    })
    .catch(() => {})
}

async function handleKbCommand(command: string, kb: KnowledgeBase) {
  if (command === 'open') {
    // 跳转到知识库详情页
    router.push(`/knowledge/${kb.id}`)
  } else if (command === 'edit') {
    currentKb.value = kb
    editForm.name = kb.name
    editForm.description = kb.description || ''
    editForm.visibility = kb.visibility
    showEditDialog.value = true
  } else if (command === 'mount') {
    currentKb.value = kb
    await loadWorkspaces()
    mountForm.workspaceId = null
    mountForm.aliasName = ''
    mountForm.sortOrder = 0
    showMountDialog.value = true
  } else if (command === 'delete') {
    ElMessageBox.confirm(`确定要删除知识库 "${kb.name}" 吗？`, '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
      .then(async () => {
        try {
          await deleteKnowledgeBase(kb.id)
          await loadKnowledgeBases()
          if (activeKbId.value === kb.id) {
            activeKbId.value = null
          }
          ElMessage.success('删除成功')
        } catch (error: any) {
          ElMessage.error(error.response?.data?.message || '删除失败')
          console.error('Failed to delete knowledge base:', error)
        }
      })
      .catch(() => {})
  }
}

function getVisibilityLabel(visibility: Visibility): string {
  const labels: Record<Visibility, string> = {
    1: '私有',
    2: '内部',
    3: '公开'
  }
  return labels[visibility]
}

function getVisibilityType(visibility: Visibility): 'success' | 'info' | 'warning' | 'danger' {
  const types: Record<Visibility, 'success' | 'info' | 'warning' | 'danger'> = {
    1: 'danger',
    2: 'info',
    3: 'success'
  }
  return types[visibility]
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('zh-CN')
}

onMounted(() => {
  loadKnowledgeBases()
})
</script>

<style scoped lang="scss">
.knowledge-container {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.knowledge-card,
.detail-card {
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;

  .kb-title {
    font-size: 16px;
    font-weight: 500;
  }

  .ml-2 {
    margin-left: 8px;
  }
}

.filter-bar {
  display: flex;
  gap: 12px;
  margin-bottom: 20px;

  .search-input {
    flex: 1;
  }

  .filter-select {
    width: 150px;
  }
}

.kb-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
  min-height: 200px;
}

.kb-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;

  &:hover {
    border-color: #409eff;
    box-shadow: 0 2px 8px rgba(64, 158, 255, 0.2);
  }

  &.active {
    border-color: #409eff;
    background: #ecf5ff;
  }

  .kb-info {
    display: flex;
    align-items: center;
    gap: 12px;
    flex: 1;
    min-width: 0;

    .kb-icon {
      color: #409eff;
      flex-shrink: 0;
    }

    .kb-details {
      flex: 1;
      min-width: 0;

      .kb-name {
        font-size: 16px;
        font-weight: 500;
        margin-bottom: 6px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .kb-meta {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 4px;

        .workspace-count {
          font-size: 12px;
          color: #909399;
        }
      }

      .kb-desc {
        font-size: 12px;
        color: #909399;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
    }
  }
}

.workspaces-section {
  margin-top: 20px;

  h4 {
    margin: 0 0 12px 0;
    font-size: 14px;
    color: #606266;
  }

  .workspace-tags {
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
  }
}

.pagination {
  display: flex;
  justify-content: center;
  margin-top: 20px;
}

@media (max-width: 768px) {
  .kb-list {
    grid-template-columns: 1fr;
  }

  .filter-bar {
    flex-direction: column;

    .filter-select {
      width: 100%;
    }
  }

  .card-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }
}
</style>
