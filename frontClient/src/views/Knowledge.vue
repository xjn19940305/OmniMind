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
            <el-icon class="kb-icon" size="32">
              <User v-if="kb.visibility === 1" />
              <Collection v-else />
            </el-icon>
            <div class="kb-details">
              <div class="kb-name">
                {{ kb.name }}
                <el-tag v-if="kb.visibility === 1" type="warning" size="small" class="ml-1">个人</el-tag>
                <el-tag v-else type="success" size="small" class="ml-1">共享</el-tag>
              </div>
              <div class="kb-meta">
                <el-tag :type="getVisibilityType(kb.visibility)" size="small">
                  {{ getVisibilityLabel(kb.visibility) }}
                </el-tag>
                <span class="member-count">{{ kb.memberCount || 0 }} 个成员</span>
                <span v-if="kb.ownerName" class="owner-name">拥有者: {{ kb.ownerName }}</span>
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
                <el-dropdown-item command="edit" :disabled="kb.visibility === 1">
                  <el-icon><Edit /></el-icon> 编辑
                </el-dropdown-item>
                <el-dropdown-item command="delete" :disabled="kb.visibility === 1" divided>
                  <el-icon><Delete /></el-icon> 删除
                </el-dropdown-item>
                <el-dropdown-item command="members" :disabled="kb.visibility === 1" divided>
                  <el-icon><User /></el-icon> 成员管理
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
          <div>
            <el-button @click="handleShowMembers(activeKb)">
              <el-icon><User /></el-icon> 成员管理
            </el-button>
            <el-button @click="activeKbId = null">关闭</el-button>
          </div>
        </div>
      </template>

      <el-descriptions :column="2" border>
        <el-descriptions-item label="知识库ID">{{ activeKb.id }}</el-descriptions-item>
        <el-descriptions-item label="可见性">
          {{ getVisibilityLabel(activeKb.visibility) }}
        </el-descriptions-item>
        <el-descriptions-item label="拥有者">
          {{ activeKb.ownerName || '-' }}
        </el-descriptions-item>
        <el-descriptions-item label="创建时间">
          {{ formatDate(activeKb.createdAt) }}
        </el-descriptions-item>
        <el-descriptions-item label="更新时间">
          {{ activeKb.updatedAt ? formatDate(activeKb.updatedAt) : '-' }}
        </el-descriptions-item>
        <el-descriptions-item label="成员数量">
          {{ activeKb.memberCount || 0 }}
        </el-descriptions-item>
        <el-descriptions-item label="描述" :span="2">
          {{ activeKb.description || '-' }}
        </el-descriptions-item>
      </el-descriptions>
    </el-card>

    <el-empty v-else description="请选择或创建知识库" :image-size="200" />

    <!-- Create KB Dialog -->
    <el-dialog v-model="showCreateDialog" title="创建知识库" width="500px">
      <el-alert
        title="知识库类型说明"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 20px"
      >
        <template #default>
          <div style="font-size: 12px">
            <div><strong>私有</strong>：个人知识库，仅自己可见，系统只能有一个</div>
            <div><strong>内部</strong>：团队共享，成员可见</div>
            <div><strong>公开</strong>：所有人可见</div>
          </div>
        </template>
      </el-alert>
      <el-form ref="createFormRef" :model="createForm" :rules="createRules" label-width="100px">
        <el-form-item label="类型" prop="visibility">
          <el-radio-group v-model="createForm.visibility">
            <el-radio :label="1" :disabled="hasPrivateKnowledgeBase">
              私有（个人知识库）
            </el-radio>
            <el-radio :label="2">内部（团队共享）</el-radio>
            <el-radio :label="3">公开（所有人可见）</el-radio>
          </el-radio-group>
          <div v-if="hasPrivateKnowledgeBase && createForm.visibility === 1" class="form-tip">
            您已拥有个人知识库，无法再创建
          </div>
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input
            v-model="createForm.name"
            :placeholder="createForm.visibility === 1 ? '个人知识库' : '请输入知识库名称'"
            maxlength="128"
            show-word-limit
          />
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
            <el-radio :label="2">内部（团队共享）</el-radio>
            <el-radio :label="3">公开（所有人可见）</el-radio>
          </el-radio-group>
          <div class="form-tip">共享知识库不支持修改为私有</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showEditDialog = false">取消</el-button>
        <el-button type="primary" :loading="editLoading" @click="handleEdit">
          保存
        </el-button>
      </template>
    </el-dialog>

    <!-- Members Dialog -->
    <el-dialog v-model="showMembersDialog" :title="`成员管理 - ${currentKb?.name || ''}`" width="700px">
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
            <el-select
              v-model="row.role"
              size="small"
              @change="handleUpdateMemberRole(row)"
            >
              <el-option :value="1" label="管理员" />
              <el-option :value="2" label="编辑" />
              <el-option :value="3" label="查看者" />
            </el-select>
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
  Search,
  User,
  Collection
} from '@element-plus/icons-vue'
import {
  getKnowledgeBases,
  getKnowledgeBase,
  createKnowledgeBase,
  updateKnowledgeBase,
  deleteKnowledgeBase,
  getKnowledgeBaseMembers,
  addKnowledgeBaseMember,
  updateKnowledgeBaseMember,
  removeKnowledgeBaseMember
} from '../api/knowledge'
import type { KnowledgeBase, KnowledgeBaseMember, Visibility, KnowledgeBaseMemberRole } from '../types'

const router = useRouter()

const loading = ref(false)
const createLoading = ref(false)
const editLoading = ref(false)
const loadingMembers = ref(false)
const addingMember = ref(false)

const knowledgeBases = ref<KnowledgeBase[]>([])
const members = ref<KnowledgeBaseMember[]>([])
const activeKbId = ref<string | null>(null)
const currentKb = ref<KnowledgeBase | null>(null)

const searchKeyword = ref('')
const visibilityFilter = ref<number | null>(null)

const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const showCreateDialog = ref(false)
const showEditDialog = ref(false)
const showMembersDialog = ref(false)

const createFormRef = ref<FormInstance>()
const editFormRef = ref<FormInstance>()

const createForm = reactive({
  name: '',
  description: '',
  visibility: 1 as Visibility
})

const editForm = reactive({
  name: '',
  description: '',
  visibility: 1 as Visibility
})

const memberForm = reactive({
  userId: '',
  role: 3 as KnowledgeBaseMemberRole
})

const activeKb = computed(() => {
  return knowledgeBases.value.find(kb => kb.id === activeKbId.value) || null
})

// 是否已有私有知识库
const hasPrivateKnowledgeBase = computed(() => {
  return knowledgeBases.value.some(kb => kb.visibility === 1)
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

async function loadMembers(knowledgeBaseId: string) {
  loadingMembers.value = true
  try {
    members.value = await getKnowledgeBaseMembers(knowledgeBaseId)
  } catch (error) {
    console.error('Failed to load members:', error)
  } finally {
    loadingMembers.value = false
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
  if (!activeKb.value?.memberCount) {
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
      // 检查是否创建私有知识库
      if (createForm.visibility === 1) {
        // 检查是否已存在私有知识库
        const hasPrivateKb = knowledgeBases.value.some(kb => kb.visibility === 1)
        if (hasPrivateKb) {
          ElMessage.warning('个人知识库只能有一个，如需修改请编辑现有的个人知识库')
          return
        }
      }

      createLoading.value = true
      try {
        await createKnowledgeBase({
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
        createForm.visibility = 2 // 默认为内部（共享）
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

async function handleShowMembers(kb: KnowledgeBase) {
  currentKb.value = kb
  showMembersDialog.value = true
  await loadMembers(kb.id)
}

async function handleAddMember() {
  if (!memberForm.userId) {
    ElMessage.warning('请输入用户ID')
    return
  }
  if (!currentKb.value) return

  addingMember.value = true
  try {
    await addKnowledgeBaseMember(currentKb.value.id, {
      userId: memberForm.userId,
      role: memberForm.role
    })
    ElMessage.success('添加成功')
    memberForm.userId = ''
    memberForm.role = 3
    await loadMembers(currentKb.value.id)
    // 刷新列表以更新成员数量
    await loadKnowledgeBases()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '添加成员失败')
  } finally {
    addingMember.value = false
  }
}

async function handleUpdateMemberRole(member: KnowledgeBaseMember) {
  if (!currentKb.value) return

  try {
    await updateKnowledgeBaseMember(currentKb.value.id, member.userId, {
      role: member.role
    })
    ElMessage.success('角色更新成功')
    await loadMembers(currentKb.value.id)
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '更新角色失败')
    // 失败时恢复原角色
    await loadMembers(currentKb.value.id)
  }
}

async function handleRemoveMember(member: KnowledgeBaseMember) {
  if (!currentKb.value) return

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

    await removeKnowledgeBaseMember(currentKb.value.id, member.userId)
    ElMessage.success('移除成功')
    await loadMembers(currentKb.value.id)
    // 刷新列表以更新成员数量
    await loadKnowledgeBases()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '移除成员失败')
    }
  }
}

async function handleKbCommand(command: string, kb: KnowledgeBase) {
  if (command === 'open') {
    // 跳转到知识库详情页
    router.push(`/knowledge/${kb.id}`)
  } else if (command === 'edit') {
    if (kb.visibility === 1) {
      ElMessage.warning('个人知识库不能编辑')
      return
    }
    currentKb.value = kb
    editForm.name = kb.name
    editForm.description = kb.description || ''
    editForm.visibility = kb.visibility
    showEditDialog.value = true
  } else if (command === 'members') {
    if (kb.visibility === 1) {
      ElMessage.warning('个人知识库无需管理成员')
      return
    }
    await handleShowMembers(kb)
  } else if (command === 'delete') {
    if (kb.visibility === 1) {
      ElMessage.warning('个人知识库不能删除')
      return
    }
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

function getMemberRoleLabel(role: number): string {
  const labels: Record<number, string> = {
    1: '管理员',
    2: '编辑',
    3: '查看者'
  }
  return labels[role] || '未知'
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
        flex-wrap: wrap;

        .member-count,
        .owner-name {
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

.members-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
}

.ml-1 {
  margin-left: 4px;
}

.form-tip {
  font-size: 12px;
  color: #f56c6c;
  margin-top: 4px;
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

    & > div:not(:first-child) {
      width: 100%;
      display: flex;
      gap: 8px;
    }
  }
}
</style>
