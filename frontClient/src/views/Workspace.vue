<template>
  <div class="workspace-container">
    <!-- 头部操作栏 -->
    <div class="header-actions">
      <el-input
        v-model="searchKeyword"
        placeholder="搜索工作空间"
        clearable
        style="width: 300px"
        @input="handleSearch"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
      <el-button type="primary" @click="showCreateDialog = true">
        <el-icon><Plus /></el-icon>
        创建工作空间
      </el-button>
    </div>

    <!-- 工作空间列表 -->
    <el-card class="workspace-list" shadow="never">
      <el-table
        v-loading="loading"
        :data="workspaces"
        stripe
        @row-click="handleRowClick"
      >
        <el-table-column prop="name" label="名称" min-width="200" />
        <el-table-column prop="type" label="类型" width="120">
          <template #default="{ row }">
            <el-tag v-if="row.type === 1" type="success">个人</el-tag>
            <el-tag v-else-if="row.type === 2" type="primary">团队</el-tag>
            <el-tag v-else type="info">共享</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="memberCount" label="成员数" width="100" />
        <el-table-column prop="knowledgeBaseCount" label="知识库数" width="120" />
        <el-table-column prop="createdAt" label="创建时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click.stop="handleViewMembers(row)">
              <el-icon><User /></el-icon>
              成员管理
            </el-button>
            <el-button link type="primary" @click.stop="handleEdit(row)">
              <el-icon><Edit /></el-icon>
              编辑
            </el-button>
            <el-button link type="danger" @click.stop="handleDelete(row)">
              <el-icon><Delete /></el-icon>
              删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <!-- 分页 -->
      <div class="pagination-wrapper">
        <el-pagination
          v-model:current-page="pagination.page"
          v-model:page-size="pagination.pageSize"
          :total="pagination.total"
          :page-sizes="[10, 20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="loadWorkspaces"
          @current-change="loadWorkspaces"
        />
      </div>
    </el-card>

    <!-- 创建/编辑工作空间对话框 -->
    <el-dialog
      v-model="showCreateDialog"
      :title="editingWorkspace ? '编辑工作空间' : '创建工作空间'"
      width="500px"
      @close="handleDialogClose"
    >
      <el-form
        ref="formRef"
        :model="workspaceForm"
        :rules="workspaceRules"
        label-width="80px"
      >
        <el-form-item label="名称" prop="name">
          <el-input
            v-model="workspaceForm.name"
            placeholder="请输入工作空间名称"
            maxlength="50"
            show-word-limit
          />
        </el-form-item>
        <el-form-item label="类型" prop="type">
          <el-select v-model="workspaceForm.type" placeholder="请选择类型" style="width: 100%">
            <el-option :value="1" label="个人" />
            <el-option :value="2" label="团队" />
            <el-option :value="3" label="共享" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showCreateDialog = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          {{ editingWorkspace ? '保存' : '创建' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 成员管理对话框 -->
    <el-dialog
      v-model="showMembersDialog"
      title="成员管理"
      width="700px"
      @close="handleMembersDialogClose"
    >
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
          <el-option :value="2" label="管理员" />
          <el-option :value="3" label="成员" />
          <el-option :value="4" label="查看者" />
        </el-select>
        <el-button type="primary" :loading="addingMember" @click="handleAddMember">
          添加成员
        </el-button>
      </div>

      <el-table v-loading="loadingMembers" :data="members" stripe max-height="400">
        <el-table-column prop="userId" label="用户ID" width="200" />
        <el-table-column prop="role" label="角色" width="150">
          <template #default="{ row }">
            <el-select
              v-model="row.role"
              size="small"
              :disabled="row.role === 1 || row.userId === currentUserId"
              @change="handleUpdateMemberRole(row)"
            >
              <el-option v-if="row.role === 1" :value="1" label="所有者" />
              <el-option :value="2" label="管理员" />
              <el-option :value="3" label="成员" />
              <el-option :value="4" label="查看者" />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column prop="joinedAt" label="加入时间" width="180">
          <template #default="{ row }">
            {{ formatDate(row.joinedAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="100">
          <template #default="{ row }">
            <el-button
              link
              type="danger"
              :disabled="row.role === 1"
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
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import {
  getWorkspaces,
  createWorkspace,
  updateWorkspace,
  deleteWorkspace,
  getWorkspaceMembers,
  addWorkspaceMember,
  updateWorkspaceMember,
  removeWorkspaceMember
} from '../api/workspace'
import type { Workspace, WorkspaceMember, WorkspaceType, WorkspaceRole } from '../types'

const loading = ref(false)
const loadingMembers = ref(false)
const submitting = ref(false)
const addingMember = ref(false)

const searchKeyword = ref('')
const workspaces = ref<Workspace[]>([])
const members = ref<WorkspaceMember[]>([])
const currentWorkspace = ref<Workspace | null>(null)

// 获取当前登录用户ID
const currentUserId = computed(() => {
  return localStorage.getItem('userId') || ''
})

const showCreateDialog = ref(false)
const showMembersDialog = ref(false)
const editingWorkspace = ref<Workspace | null>(null)

const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

const formRef = ref<FormInstance>()

const workspaceForm = reactive({
  name: '',
  type: 1 as WorkspaceType
})

const memberForm = reactive({
  userId: '',
  role: 3 as WorkspaceRole
})

const workspaceRules: FormRules = {
  name: [
    { required: true, message: '请输入工作空间名称', trigger: 'blur' },
    { min: 2, max: 50, message: '长度在 2 到 50 个字符', trigger: 'blur' }
  ],
  type: [{ required: true, message: '请选择工作空间类型', trigger: 'change' }]
}

onMounted(() => {
  loadWorkspaces()
})

async function loadWorkspaces() {
  loading.value = true
  try {
    const { items, totalCount } = await getWorkspaces({
      page: pagination.page,
      pageSize: pagination.pageSize,
      keyword: searchKeyword.value
    })
    workspaces.value = items
    pagination.total = totalCount
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '加载工作空间失败')
  } finally {
    loading.value = false
  }
}

async function loadMembers(workspaceId: string) {
  loadingMembers.value = true
  try {
    members.value = await getWorkspaceMembers(workspaceId)
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '加载成员列表失败')
  } finally {
    loadingMembers.value = false
  }
}

function handleSearch() {
  pagination.page = 1
  loadWorkspaces()
}

function handleRowClick(row: Workspace) {
  // 可以添加点击行的逻辑，比如查看详情
}

function handleEdit(row: Workspace) {
  editingWorkspace.value = row
  workspaceForm.name = row.name
  workspaceForm.type = row.type
  showCreateDialog.value = true
}

async function handleDelete(row: Workspace) {
  try {
    await ElMessageBox.confirm(
      `确定要删除工作空间"${row.name}"吗？此操作不可恢复。`,
      '删除确认',
      {
        type: 'warning',
        confirmButtonText: '确定',
        cancelButtonText: '取消'
      }
    )

    await deleteWorkspace(row.id)
    ElMessage.success('删除成功')
    loadWorkspaces()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '删除失败')
    }
  }
}

function handleViewMembers(row: Workspace) {
  currentWorkspace.value = row
  showMembersDialog.value = true
  loadMembers(row.id)
}

async function handleSubmit() {
  if (!formRef.value) return

  await formRef.value.validate(async (valid) => {
    if (valid) {
      submitting.value = true
      try {
        if (editingWorkspace.value) {
          await updateWorkspace(editingWorkspace.value.id, {
            name: workspaceForm.name,
            type: workspaceForm.type
          })
          ElMessage.success('更新成功')
        } else {
          await createWorkspace({
            name: workspaceForm.name,
            type: workspaceForm.type
          })
          ElMessage.success('创建成功')
        }
        showCreateDialog.value = false
        loadWorkspaces()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '操作失败')
      } finally {
        submitting.value = false
      }
    }
  })
}

function handleDialogClose() {
  editingWorkspace.value = null
  workspaceForm.name = ''
  workspaceForm.type = 1
  formRef.value?.resetFields()
}

async function handleAddMember() {
  if (!memberForm.userId) {
    ElMessage.warning('请输入用户ID')
    return
  }
  if (!currentWorkspace.value) return

  // 前端验证：不允许添加所有者角色
  if (memberForm.role === 1) {
    ElMessage.error('不能添加所有者角色')
    return
  }

  addingMember.value = true
  try {
    await addWorkspaceMember(currentWorkspace.value.id, {
      userId: memberForm.userId,
      role: memberForm.role
    })
    ElMessage.success('添加成功')
    memberForm.userId = ''
    memberForm.role = 3
    loadMembers(currentWorkspace.value.id)
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '添加成员失败')
  } finally {
    addingMember.value = false
  }
}

async function handleUpdateMemberRole(member: WorkspaceMember) {
  if (!currentWorkspace.value) return

  // 前端验证：不允许修改为所有者
  if (member.role === 1) {
    ElMessage.error('不能将成员设置为所有者')
    // 恢复原角色
    loadMembers(currentWorkspace.value.id)
    return
  }

  // 前端验证：不允许修改所有者的角色
  const originalMember = members.value.find(m => m.userId === member.userId)
  if (originalMember && originalMember.role === 1) {
    ElMessage.error('不能修改所有者的角色')
    // 恢复原角色
    loadMembers(currentWorkspace.value.id)
    return
  }

  // 前端验证：不允许修改自己的角色
  if (member.userId === currentUserId.value) {
    ElMessage.error('不能修改自己的角色')
    // 恢复原角色
    loadMembers(currentWorkspace.value.id)
    return
  }

  try {
    await updateWorkspaceMember(currentWorkspace.value.id, member.userId, {
      role: member.role
    })
    ElMessage.success('角色更新成功')
    loadMembers(currentWorkspace.value.id)
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '更新角色失败')
    // 失败时也恢复原角色
    loadMembers(currentWorkspace.value.id)
  }
}

async function handleRemoveMember(member: WorkspaceMember) {
  if (!currentWorkspace.value) return

  try {
    await ElMessageBox.confirm(
      `确定要移除成员"${member.userId}"吗？`,
      '移除确认',
      {
        type: 'warning',
        confirmButtonText: '确定',
        cancelButtonText: '取消'
      }
    )

    await removeWorkspaceMember(currentWorkspace.value.id, member.userId)
    ElMessage.success('移除成功')
    loadMembers(currentWorkspace.value.id)
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '移除成员失败')
    }
  }
}

function handleMembersDialogClose() {
  currentWorkspace.value = null
  members.value = []
  memberForm.userId = ''
  memberForm.role = 3
}

function formatDate(dateString: string) {
  if (!dateString) return '-'
  const date = new Date(dateString)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}
</script>

<style scoped lang="scss">
.workspace-container {
  padding: 20px;
}

.header-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.workspace-list {
  :deep(.el-card__body) {
    padding: 0;
  }
}

.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  padding: 16px;
  border-top: 1px solid #ebeef5;
}

.members-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
}

@media (max-width: 768px) {
  .workspace-container {
    padding: 12px;
  }

  .header-actions {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;

    .el-input {
      width: 100% !important;
    }
  }

  .members-header {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;

    .el-input,
    .el-select {
      width: 100% !important;
      margin-left: 0 !important;
    }
  }
}
</style>
