<template>
  <div class="folder-tree">
    <el-tree
      ref="treeRef"
      :data="treeData"
      :props="treeProps"
      :expand-on-click-node="false"
      :highlight-current="true"
      node-key="id"
      default-expand-all
      @node-click="handleNodeClick"
      @node-contextmenu="handleNodeContextMenu"
    >
      <template #default="{ node, data }">
        <div class="tree-node">
          <el-icon class="folder-icon"><Folder /></el-icon>
          <span class="node-label">{{ node.label }}</span>
          <span v-if="showDocumentCount" class="document-count">
            ({{ data.documentCount || 0 }})
          </span>
          <div class="node-actions" @click.stop>
            <el-dropdown @command="(cmd) => handleCommand(cmd, data)" trigger="click">
              <el-icon class="more-icon"><MoreFilled /></el-icon>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="addFolder">
                    <el-icon><FolderAdd /></el-icon> 新建文件夹
                  </el-dropdown-item>
                  <el-dropdown-item command="addDocument">
                    <el-icon><DocumentAdd /></el-icon> 上传文档
                  </el-dropdown-item>
                  <el-dropdown-item command="rename" divided>
                    <el-icon><Edit /></el-icon> 重命名
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
      </template>
    </el-tree>

    <!-- 新建/编辑文件夹对话框 -->
    <el-dialog
      v-model="showFolderDialog"
      :title="isEditMode ? '编辑文件夹' : '新建文件夹'"
      width="500px"
    >
      <el-form ref="folderFormRef" :model="folderForm" :rules="folderRules" label-width="100px">
        <el-form-item label="文件夹名称" prop="name">
          <el-input v-model="folderForm.name" placeholder="请输入文件夹名称" maxlength="128" />
        </el-form-item>
        <el-form-item label="描述" prop="description">
          <el-input
            v-model="folderForm.description"
            type="textarea"
            :rows="3"
            placeholder="请输入描述（可选）"
            maxlength="500"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showFolderDialog = false">取消</el-button>
        <el-button type="primary" :loading="folderLoading" @click="handleSaveFolder">
          {{ isEditMode ? '保存' : '创建' }}
        </el-button>
      </template>
    </el-dialog>

    <!-- 移动文件夹对话框 -->
    <el-dialog v-model="showMoveDialog" title="移动文件夹" width="500px">
      <el-tree
        :data="treeDataForMove"
        :props="treeProps"
        node-key="id"
        default-expand-all
        @node-click="handleSelectTargetFolder"
      />
      <template #footer>
        <el-button @click="showMoveDialog = false">取消</el-button>
        <el-button type="primary" :loading="moveLoading" @click="handleConfirmMove">
          移动
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import {
  Folder,
  FolderAdd,
  DocumentAdd,
  Edit,
  Sort,
  Delete,
  MoreFilled
} from '@element-plus/icons-vue'
import {
  getFolderTree,
  createFolder,
  updateFolder,
  moveFolder,
  deleteFolder
} from '../api/folder'

interface Props {
  knowledgeBaseId: string
  showDocumentCount?: boolean
}

interface Emits {
  (e: 'select', folder: any): void
  (e: 'add-document', folderId?: string): void
}

const props = withDefaults(defineProps<Props>(), {
  showDocumentCount: true
})

const emit = defineEmits<Emits>()

const treeRef = ref()
const treeData = ref<any[]>([])
const treeDataForMove = ref<any[]>([])
const treeProps = {
  children: 'children',
  label: 'name'
}

const showFolderDialog = ref(false)
const showMoveDialog = ref(false)
const isEditMode = ref(false)
const folderLoading = ref(false)
const moveLoading = ref(false)

const folderFormRef = ref<FormInstance>()
const folderForm = ref({
  id: '',
  name: '',
  description: '',
  parentFolderId: '',
  sortOrder: 0
})

const currentFolder = ref<any>(null)
const targetFolderId = ref<string | null>(null)

const folderRules: FormRules = {
  name: [
    { required: true, message: '请输入文件夹名称', trigger: 'blur' },
    { max: 128, message: '名称长度不能超过128个字符', trigger: 'blur' }
  ]
}

// 加载文件夹树
async function loadFolderTree() {
  try {
    const data = await getFolderTree(props.knowledgeBaseId)
    // 添加根节点（知识库本身）
    treeData.value = [
      {
        id: 'root',
        name: '根目录',
        documentCount: 0,
        children: data
      }
    ]
    treeDataForMove.value = treeData.value
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '加载文件夹失败')
  }
}

// 节点点击
function handleNodeClick(data: any) {
  currentFolder.value = data
  emit('select', data)
}

// 右键菜单
function handleNodeContextMenu(event: MouseEvent, data: any) {
  event.preventDefault()
  currentFolder.value = data
}

// 命令处理
function handleCommand(command: string, data: any) {
  currentFolder.value = data

  switch (command) {
    case 'addFolder':
      showAddFolderDialog(data.id === 'root' ? null : data.id)
      break
    case 'addDocument':
      emit('add-document', data.id === 'root' ? null : data.id)
      break
    case 'rename':
      showEditFolderDialog(data)
      break
    case 'move':
      showMoveFolderDialog(data)
      break
    case 'delete':
      handleDeleteFolder(data)
      break
  }
}

// 显示新建文件夹对话框
function showAddFolderDialog(parentFolderId: string | null = null) {
  isEditMode.value = false
  folderForm.value = {
    id: '',
    name: '',
    description: '',
    parentFolderId: parentFolderId || '',
    sortOrder: 0
  }
  showFolderDialog.value = true
}

// 显示编辑文件夹对话框
function showEditFolderDialog(folder: any) {
  isEditMode.value = true
  folderForm.value = {
    id: folder.id,
    name: folder.name,
    description: folder.description || '',
    parentFolderId: folder.parentFolderId || '',
    sortOrder: folder.sortOrder || 0
  }
  showFolderDialog.value = true
}

// 保存文件夹
async function handleSaveFolder() {
  if (!folderFormRef.value) return

  await folderFormRef.value.validate(async (valid) => {
    if (valid) {
      folderLoading.value = true
      try {
        if (isEditMode.value) {
          await updateFolder(folderForm.value.id, {
            name: folderForm.value.name,
            description: folderForm.value.description || undefined,
            sortOrder: folderForm.value.sortOrder
          })
          ElMessage.success('文件夹更新成功')
        } else {
          await createFolder({
            knowledgeBaseId: props.knowledgeBaseId,
            parentFolderId: folderForm.value.parentFolderId || undefined,
            name: folderForm.value.name,
            description: folderForm.value.description || undefined,
            sortOrder: folderForm.value.sortOrder
          })
          ElMessage.success('文件夹创建成功')
        }
        showFolderDialog.value = false
        await loadFolderTree()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '操作失败')
      } finally {
        folderLoading.value = false
      }
    }
  })
}

// 显示移动文件夹对话框
function showMoveFolderDialog(folder: any) {
  currentFolder.value = folder
  targetFolderId.value = null
  showMoveDialog.value = true
}

// 选择目标文件夹
function handleSelectTargetFolder(data: any) {
  targetFolderId.value = data.id === 'root' ? null : data.id
}

// 确认移动
async function handleConfirmMove() {
  moveLoading.value = true
  try {
    await moveFolder(currentFolder.value.id, {
      parentFolderId: targetFolderId.value || undefined
    })
    ElMessage.success('文件夹移动成功')
    showMoveDialog.value = false
    await loadFolderTree()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '移动失败')
  } finally {
    moveLoading.value = false
  }
}

// 删除文件夹
async function handleDeleteFolder(folder: any) {
  try {
    await ElMessageBox.confirm(`确定要删除文件夹"${folder.name}"吗？`, '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })

    await deleteFolder(folder.id)
    ElMessage.success('删除成功')
    await loadFolderTree()

    if (currentFolder.value?.id === folder.id) {
      emit('select', null)
    }
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '删除失败')
    }
  }
}

// 暴露方法给父组件
defineExpose({
  loadFolderTree,
  refresh: loadFolderTree
})

// 监听知识库ID变化
watch(() => props.knowledgeBaseId, () => {
  if (props.knowledgeBaseId) {
    loadFolderTree()
  }
}, { immediate: true })

onMounted(() => {
  if (props.knowledgeBaseId) {
    loadFolderTree()
  }
})
</script>

<style scoped lang="scss">
.folder-tree {
  :deep(.el-tree-node__content) {
    height: 36px;
    &:hover {
      .node-actions {
        opacity: 1;
      }
    }
  }

  .tree-node {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 1;
    padding-right: 8px;

    .folder-icon {
      color: #f7ba2a;
      flex-shrink: 0;
    }

    .node-label {
      flex: 1;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .document-count {
      color: #909399;
      font-size: 12px;
    }

    .node-actions {
      opacity: 0;
      transition: opacity 0.2s;
      margin-left: auto;

      .more-icon {
        cursor: pointer;
        font-size: 16px;

        &:hover {
          color: #409eff;
        }
      }
    }
  }
}
</style>
