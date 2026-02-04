<template>
  <div class="knowledge-detail">
    <!-- 头部信息 -->
    <div class="header">
      <div class="header-left">
        <el-button text @click="goBack">
          <el-icon><ArrowLeft /></el-icon>
        </el-button>
        <div class="kb-info">
          <el-icon class="kb-icon" :size="24">
            <User v-if="knowledgeBase?.visibility === 1" />
            <Collection v-else />
          </el-icon>
          <span class="kb-name">{{ knowledgeBase?.name }}</span>
          <el-tag v-if="knowledgeBase?.visibility === 1" type="danger" size="small">私有</el-tag>
          <el-tag v-else-if="knowledgeBase?.visibility === 2" type="info" size="small">内部</el-tag>
          <el-tag v-else type="success" size="small">公开</el-tag>
        </div>
      </div>
      <div class="header-actions">
        <el-button @click="showMembersDialog = true">
          <el-icon><User /></el-icon> 成员管理
        </el-button>
        <el-button @click="showSettingsDialog = true">
          <el-icon><Setting /></el-icon> 设置
        </el-button>
      </div>
    </div>

    <!-- 描述信息 -->
    <div v-if="knowledgeBase?.description" class="description">
      {{ knowledgeBase.description }}
    </div>

    <!-- 面包屑导航 -->
    <div class="breadcrumb-bar">
      <el-breadcrumb separator="/">
        <el-breadcrumb-item @click="navigateToRoot">
          <el-icon><HomeFilled /></el-icon> 全部文件
        </el-breadcrumb-item>
        <el-breadcrumb-item
          v-for="(folder, index) in breadcrumbFolders"
          :key="folder.id"
          @click="navigateToFolder(index)"
        >
          {{ folder.name }}
        </el-breadcrumb-item>
      </el-breadcrumb>
    </div>

    <!-- 搜索结果提示 -->
    <div v-if="searchKeyword" class="search-result-bar">
      <span class="search-text">
        <el-icon><Search /></el-icon>
        搜索结果: "{{ searchKeyword }}" (找到 {{ documents.length }} 个文件)
      </span>
      <el-button size="small" text @click="clearSearch">
        <el-icon><Close /></el-icon> 清除搜索
      </el-button>
    </div>

    <!-- 工具栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <el-input
          v-model="searchKeyword"
          placeholder="搜索文件"
          clearable
          @input="handleSearch"
          class="search-input"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
      </div>
      <div class="toolbar-right">
        <el-dropdown ref="newDropdown" trigger="click" @command="handleNewCommand">
          <el-button type="primary">
            <el-icon><Plus /></el-icon> 新建
          </el-button>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="folder">
                <el-icon><Folder /></el-icon> 新建文件夹
              </el-dropdown-item>
              <el-dropdown-item command="note">
                <el-icon><EditPen /></el-icon> 新建笔记
              </el-dropdown-item>
              <el-dropdown-item divided command="upload">
                <el-icon><Upload /></el-icon> 本地文件
              </el-dropdown-item>
              <el-dropdown-item command="camera">
                <el-icon><Camera /></el-icon> 拍照
              </el-dropdown-item>
              <el-dropdown-item command="image">
                <el-icon><Picture /></el-icon> 图片
              </el-dropdown-item>
              <el-dropdown-item command="record">
                <el-icon><Microphone /></el-icon> 录音
              </el-dropdown-item>
              <el-dropdown-item command="wechat">
                <el-icon><ChatDotRound /></el-icon> 微信文件
              </el-dropdown-item>
              <el-dropdown-item command="link" divided>
                <el-icon><Link /></el-icon> 网页链接
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </div>
    </div>

    <!-- 文件列表 -->
    <div v-loading="loading" class="file-list-container">
      <!-- 列表视图 -->
      <div v-if="viewMode === 'list'" class="file-list">
        <!-- 表头 -->
        <div class="file-list-header">
          <div class="col-name">文件名</div>
          <div class="col-size">大小</div>
          <div class="col-date">修改时间</div>
          <div class="col-actions">操作</div>
        </div>

        <!-- 文件夹 -->
        <div
          v-for="folder in folders"
          :key="'folder-' + folder.id"
          class="file-item folder-item"
          :class="{ selected: selectedItems.includes(folder.id) }"
          @click="handleFolderClick(folder)"
          @dblclick="enterFolder(folder)"
          @contextmenu.prevent="handleContextMenu($event, folder, 'folder')"
        >
          <div class="col-name">
            <el-checkbox
              v-model="selectedItems"
              :value="folder.id"
              @click.stop
              @change="handleSelectionChange"
            />
            <el-icon class="file-icon folder-icon"><Folder /></el-icon>
            <span class="file-name">{{ folder.name }}</span>
          </div>
          <div class="col-size">-</div>
          <div class="col-date">{{ formatDate(folder.createdAt) }}</div>
          <div class="col-actions" @click.stop>
            <el-dropdown @command="(cmd) => handleFolderCommand(cmd, folder)">
              <el-button text>
                <el-icon><MoreFilled /></el-icon>
              </el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="open">
                    <el-icon><FolderOpened /></el-icon> 打开
                  </el-dropdown-item>
                  <el-dropdown-item command="rename">
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

        <!-- 文档 -->
        <div
          v-for="doc in documents"
          :key="'doc-' + doc.id"
          class="file-item"
          :class="{ selected: selectedItems.includes(doc.id) }"
          @click="handleDocClick(doc)"
          @contextmenu.prevent="handleContextMenu($event, doc, 'document')"
        >
          <div class="col-name">
            <el-checkbox
              v-model="selectedItems"
              :value="doc.id"
              @click.stop
              @change="handleSelectionChange"
            />
            <el-icon class="file-icon" :color="getFileIconColor(doc.contentType)">
              <component :is="getFileIcon(doc.contentType)" />
            </el-icon>
            <span class="file-name">{{ doc.title }}</span>
            <el-tag :type="getStatusType(doc.status)" size="small" class="status-tag">
              {{ getStatusLabel(doc.status) }}
            </el-tag>
          </div>
          <div class="col-size">{{ formatFileSize(doc.fileSize) }}</div>
          <div class="col-date">{{ formatDate(doc.updatedAt || doc.createdAt) }}</div>
          <div class="col-actions" @click.stop>
            <el-dropdown @command="(cmd) => handleDocCommand(cmd, doc)">
              <el-button text>
                <el-icon><MoreFilled /></el-icon>
              </el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="open">
                    <el-icon><View /></el-icon> 打开
                  </el-dropdown-item>
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

        <el-empty
          v-if="!loading && folders.length === 0 && documents.length === 0"
          description="暂无文件"
        />
      </div>

      <!-- 网格视图 -->
      <div v-else class="file-grid">
        <!-- 文件夹 -->
        <div
          v-for="folder in folders"
          :key="'folder-' + folder.id"
          class="grid-item folder-item"
          :class="{ selected: selectedItems.includes(folder.id) }"
          @click="handleFolderClick(folder)"
          @dblclick="enterFolder(folder)"
        >
          <div class="grid-item-icon">
            <el-icon :size="48"><Folder /></el-icon>
          </div>
          <div class="grid-item-name" :title="folder.name">{{ folder.name }}</div>
        </div>

        <!-- 文档 -->
        <div
          v-for="doc in documents"
          :key="'doc-' + doc.id"
          class="grid-item"
          :class="{ selected: selectedItems.includes(doc.id) }"
          @click="handleDocClick(doc)"
        >
          <div class="grid-item-icon">
            <el-icon :size="48" :color="getFileIconColor(doc.contentType)">
              <component :is="getFileIcon(doc.contentType)" />
            </el-icon>
          </div>
          <div class="grid-item-name" :title="doc.title">{{ doc.title }}</div>
          <el-tag :type="getStatusType(doc.status)" size="small" class="grid-item-status">
            {{ getStatusLabel(doc.status) }}
          </el-tag>
        </div>

        <el-empty
          v-if="!loading && folders.length === 0 && documents.length === 0"
          description="暂无文件"
          :image-size="100"
        />
      </div>
    </div>

    <!-- 批量操作栏 -->
    <div v-if="selectedItems.length > 0" class="batch-bar">
      <span>已选中 {{ selectedItems.length }} 项</span>
      <div class="batch-actions">
        <el-button @click="handleBatchMove">移动</el-button>
        <el-button @click="handleBatchDelete">删除</el-button>
        <el-button @click="selectedItems.splice(0, selectedItems.length)">取消</el-button>
      </div>
    </div>

    <!-- 新建文件夹对话框 -->
    <el-dialog v-model="showFolderDialog" title="新建文件夹" width="500px">
      <el-form ref="folderFormRef" :model="folderForm" :rules="folderRules" label-width="100px">
        <el-form-item label="文件夹名称" prop="name">
          <el-input v-model="folderForm.name" placeholder="请输入文件夹名称" maxlength="128" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showFolderDialog = false">取消</el-button>
        <el-button type="primary" :loading="folderLoading" @click="handleCreateFolder">
          创建
        </el-button>
      </template>
    </el-dialog>

    <!-- 重命名对话框 -->
    <el-dialog v-model="showRenameDialog" title="重命名" width="500px">
      <el-form ref="renameFormRef" :model="renameForm" :rules="renameRules" label-width="100px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="renameForm.name" placeholder="请输入新名称" maxlength="128" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showRenameDialog = false">取消</el-button>
        <el-button type="primary" :loading="renameLoading" @click="handleRename">
          确定
        </el-button>
      </template>
    </el-dialog>

    <!-- 上传对话框 -->
    <el-dialog v-model="showUploadDialog" :title="uploadType === 'image' ? '上传图片' : '上传文件'" width="600px" @close="handleUploadDialogClose">
      <el-upload
        ref="uploadRef"
        v-model:file-list="uploadFileList"
        :action="uploadUrl"
        :headers="uploadHeaders"
        :data="uploadData"
        :show-file-list="true"
        :auto-upload="false"
        :on-change="handleFileChange"
        :accept="uploadType === 'image' ? 'image/*' : '*'"
        drag
        multiple
        class="upload-area"
        limit="10"
      >
        <el-icon class="upload-icon"><UploadFilled /></el-icon>
        <div class="upload-text">
          {{ uploadType === 'image' ? '拖拽图片到此处或点击上传图片' : '拖拽文件到此处或点击上传' }}
        </div>
      </el-upload>
      <template #footer>
        <el-button @click="showUploadDialog = false">取消</el-button>
        <el-button type="primary" :loading="uploading" @click="handleUpload">
          开始上传
        </el-button>
      </template>
    </el-dialog>

    <!-- 网页链接对话框 -->
    <el-dialog v-model="showLinkDialog" title="添加网页链接" width="600px">
      <el-form ref="linkFormRef" :model="linkForm" :rules="linkRules" label-width="100px">
        <el-form-item label="链接地址" prop="url">
          <el-input v-model="linkForm.url" placeholder="请输入网页链接，如 https://example.com" />
        </el-form-item>
        <el-form-item label="标题" prop="title">
          <el-input v-model="linkForm.title" placeholder="请输入标题（可选）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showLinkDialog = false">取消</el-button>
        <el-button type="primary" :loading="linkLoading" @click="handleAddLink">
          添加
        </el-button>
      </template>
    </el-dialog>

    <!-- 新建笔记对话框 -->
    <el-dialog v-model="showNoteDialog" title="新建笔记" width="700px">
      <el-form ref="noteFormRef" :model="noteForm" :rules="noteRules" label-width="80px">
        <el-form-item label="标题" prop="title">
          <el-input v-model="noteForm.title" placeholder="请输入笔记标题" maxlength="128" />
        </el-form-item>
        <el-form-item label="内容" prop="content">
          <el-input
            v-model="noteForm.content"
            type="textarea"
            placeholder="请输入笔记内容（支持 Markdown 格式）"
            :rows="12"
            maxlength="10000"
            show-word-limit
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showNoteDialog = false">取消</el-button>
        <el-button type="primary" :loading="noteLoading" @click="handleCreateNote">
          创建
        </el-button>
      </template>
    </el-dialog>

    <!-- 设置对话框 -->
    <el-dialog v-model="showSettingsDialog" title="知识库设置" width="600px">
      <el-form v-if="knowledgeBase" label-width="100px">
        <el-form-item label="知识库ID">
          <el-input :value="knowledgeBase.id" disabled />
        </el-form-item>
        <el-form-item label="名称">
          <el-input :value="knowledgeBase.name" disabled />
        </el-form-item>
        <el-form-item label="成员数量">
          <span>{{ knowledgeBase.memberCount || 0 }}</span>
        </el-form-item>
        <el-form-item label="创建时间">
          <span>{{ formatDate(knowledgeBase.createdAt) }}</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showSettingsDialog = false">关闭</el-button>
      </template>
    </el-dialog>

    <!-- 成员管理对话框 -->
    <el-dialog v-model="showMembersDialog" :title="`成员管理 - ${knowledgeBase?.name || ''}`" width="900px">
      <el-tabs v-model="membersActiveTab" @tab-change="handleMembersTabChange">
        <!-- 成员列表 -->
        <el-tab-pane label="成员列表" name="members">
          <div class="members-toolbar">
            <el-button type="primary" @click="showInviteDialog = true">
              <el-icon><Link /></el-icon> 生成邀请链接
            </el-button>
          </div>
          <el-table v-loading="loadingMembers" :data="members" stripe max-height="400">
            <el-table-column prop="userId" label="用户ID" width="200" />
            <el-table-column prop="userName" label="用户名" width="150" />
            <el-table-column prop="role" label="角色" width="120">
              <template #default="{ row }">
                <el-tag :type="getMemberRoleType(row.role)" size="small">
                  {{ getMemberRoleLabel(row.role) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="100">
              <template #default="{ row }">
                <el-button link type="danger" @click="handleRemoveMember(row)">移除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>

        <!-- 邀请列表 -->
        <el-tab-pane label="邀请列表" name="invitations">
          <div class="members-toolbar">
            <el-select v-model="invitationStatusFilter" placeholder="筛选状态" style="width: 150px" @change="loadInvitations">
              <el-option :value="undefined" label="全部" />
              <el-option :value="0" label="待处理/待审核" />
              <el-option :value="1" label="已加入" />
              <el-option :value="2" label="已拒绝" />
              <el-option :value="3" label="已过期" />
              <el-option :value="4" label="已取消" />
            </el-select>
          </div>
          <el-table v-loading="loadingInvitations" :data="invitations" stripe max-height="400">
            <el-table-column prop="code" label="邀请码" width="100" />
            <el-table-column label="被邀请人" width="120">
              <template #default="{ row }">
                {{ row.inviteeUser?.nickName || row.inviteeUser?.userName || row.email || '公开' }}
              </template>
            </el-table-column>
            <el-table-column prop="applicationReason" label="申请理由" show-overflow-tooltip>
              <template #default="{ row }">
                {{ row.applicationReason || '-' }}
              </template>
            </el-table-column>
            <el-table-column prop="role" label="角色" width="100">
              <template #default="{ row }">
                <el-tag :type="getMemberRoleType(row.role)" size="small">
                  {{ getMemberRoleLabel(row.role) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="requireApproval" label="需要审核" width="90">
              <template #default="{ row }">
                <el-tag :type="row.requireApproval ? 'warning' : 'success'" size="small">
                  {{ row.requireApproval ? '是' : '否' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="status" label="状态" width="100">
              <template #default="{ row }">
                <el-tag :type="getInvitationStatusType(row.status)" size="small">
                  {{ getInvitationStatusLabel(row.status, row.inviteeUserId) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="expiresAt" label="过期时间" width="160">
              <template #default="{ row }">
                {{ formatInvitationDate(row.expiresAt) }}
              </template>
            </el-table-column>
            <el-table-column label="操作" width="240">
              <template #default="{ row }">
                <el-button link type="primary" @click="handleCopyInviteLink(row)">复制链接</el-button>
                <!-- 待审核：显示通过/拒绝按钮 -->
                <el-button v-if="row.status === 0 && row.inviteeUserId" link type="success" @click="handleApproveInvitation(row, true)">通过</el-button>
                <el-button v-if="row.status === 0 && row.inviteeUserId" link type="danger" @click="handleApproveInvitation(row, false)">拒绝</el-button>
                <!-- 待处理还没人接受：显示取消按钮 -->
                <el-button v-if="row.status === 0 && !row.inviteeUserId" link type="danger" @click="handleCancelInvitation(row)">取消</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-tab-pane>
      </el-tabs>
    </el-dialog>

    <!-- 生成邀请链接对话框 -->
    <el-dialog v-model="showInviteDialog" title="生成邀请链接" width="600px">
      <el-form ref="inviteFormRef" :model="inviteForm" :rules="inviteRules" label-width="100px">
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="inviteForm.email" placeholder="被邀请人邮箱（可选，留空则为公开邀请）" />
        </el-form-item>
        <el-form-item label="角色" prop="role">
          <el-select v-model="inviteForm.role" placeholder="选择角色">
            <el-option :value="1" label="管理员" />
            <el-option :value="2" label="编辑" />
            <el-option :value="3" label="查看者" />
          </el-select>
        </el-form-item>
        <el-form-item label="需要审核">
          <el-switch v-model="inviteForm.requireApproval" />
          <span class="form-tip">开启后用户接受邀请需管理员审核</span>
        </el-form-item>
        <el-form-item label="有效期" prop="expireDays">
          <el-input-number v-model="inviteForm.expireDays" :min="1" :max="30" />
          <span class="form-tip">天</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showInviteDialog = false">取消</el-button>
        <el-button type="primary" :loading="creatingInvitation" @click="handleCreateInvitation">
          生成邀请
        </el-button>
      </template>
    </el-dialog>

    <!-- 邀请链接结果对话框 -->
    <el-dialog v-model="showInviteResultDialog" title="邀请链接已生成" width="600px">
      <div class="invite-result">
        <el-icon class="success-icon" :size="48"><SuccessFilled /></el-icon>
        <p>邀请链接已生成，您可以：</p>
        <el-input
          :model-value="createdInviteLink"
          readonly
          class="invite-link-input"
        >
          <template #append>
            <el-button @click="handleCopyInviteLinkInput">复制</el-button>
          </template>
        </el-input>
        <p class="invite-tip">将此链接发送给被邀请人，他们将可以通过链接加入知识库</p>
      </div>
      <template #footer>
        <el-button type="primary" @click="showInviteResultDialog = false">确定</el-button>
      </template>
    </el-dialog>

    <!-- 移动对话框 -->
    <el-dialog v-model="showMoveDialog" :title="getMoveDialogTitle()" width="600px">
      <div class="move-dialog-content">
        <p class="move-tip">{{ getMoveDescription() }}</p>
        <div v-loading="loadingFolderTree" class="folder-tree">
          <el-tree
            v-if="folderTree.length > 0"
            :data="folderTree"
            :props="{ label: 'name', children: 'children', disabled: isMoveDisabled }"
            node-key="id"
            :default-expand-all="false"
            :highlight-current="true"
            @node-click="handleFolderSelect"
          >
            <template #default="{ node, data }">
              <span class="tree-node">
                <el-icon><Folder /></el-icon>
                <span class="tree-node-label">{{ data.name }}</span>
                <span v-if="data.documentCount > 0" class="tree-node-count">({{ data.documentCount }})</span>
              </span>
            </template>
          </el-tree>
        </div>
        <div class="move-selection">
          <span v-if="selectedTargetFolderId === null" class="selection-text">目标: 根目录</span>
          <span v-else class="selection-text">目标: 已选择文件夹</span>
          <el-button size="small" text @click="selectedTargetFolderId = null">清除选择</el-button>
        </div>
      </div>
      <template #footer>
        <el-button @click="showMoveDialog = false">取消</el-button>
        <el-button type="primary" :loading="moving" @click="handleConfirmMove">
          确定移动
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, reactive, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import type { UploadInstance } from 'element-plus'
import {
  ArrowLeft,
  User,
  Collection,
  Setting,
  Search,
  Plus,
  Folder,
  FolderOpened,
  Edit,
  Delete,
  MoreFilled,
  Upload,
  UploadFilled,
  Camera,
  Picture,
  Microphone,
  ChatDotRound,
  Link,
  EditPen,
  Download,
  Sort,
  View,
  HomeFilled,
  Document,
  VideoCamera,
  Headset,
  SuccessFilled,
  Close
} from '@element-plus/icons-vue'
import { getKnowledgeBase } from '../api/knowledge'
import { getFolderList, createFolder, updateFolder, deleteFolder, moveFolder, getFolderTree } from '../api/folder'
import { getDocuments, deleteDocument, createNote, createWebLink, moveDocument } from '../api/document'
import { addKnowledgeBaseMember, getKnowledgeBaseMembers, removeKnowledgeBaseMember } from '../api/knowledge'
import {
  createInvitation,
  getInvitations,
  cancelInvitation,
  approveInvitation
} from '../api/invitation'
import { useUserStore } from '../stores/user'
import {
  initSignalR,
  onDocumentProgress,
  isConnected,
  type DocumentProgress
} from '../utils/signalr'
import type { KnowledgeBaseDetail, KnowledgeBaseMember, Folder as FolderType, Document as DocType, KnowledgeBaseMemberRole, Invitation, FolderTreeResponse } from '../types'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const knowledgeBaseId = computed(() => route.params.id as string)
const knowledgeBase = ref<KnowledgeBaseDetail | null>(null)

// 文件夹和文档
const folders = ref<FolderType[]>([])
const documents = ref<DocType[]>([])
const currentFolderId = ref<string | null>(null)
const breadcrumbFolders = ref<FolderType[]>([])
const selectedItems = ref<string[]>([])

// UI 状态
const loading = ref(false)
const uploading = ref(false)
const viewMode = ref<'list' | 'grid'>('list')

// 对话框状态
const showFolderDialog = ref(false)
const showRenameDialog = ref(false)
const showUploadDialog = ref(false)
const showNoteDialog = ref(false)
const showLinkDialog = ref(false)
const showSettingsDialog = ref(false)
const showMembersDialog = ref(false)
const folderLoading = ref(false)
const renameLoading = ref(false)
const noteLoading = ref(false)
const linkLoading = ref(false)
const loadingMembers = ref(false)

// 邀请相关状态
const membersActiveTab = ref<'members' | 'invitations'>('members')
const showInviteDialog = ref(false)
const showInviteResultDialog = ref(false)
const loadingInvitations = ref(false)
const creatingInvitation = ref(false)
const invitations = ref<Invitation[]>([])
const invitationStatusFilter = ref<number | undefined>(undefined)
const inviteFormRef = ref<FormInstance>()
const inviteForm = reactive({
  email: '',
  role: 3 as KnowledgeBaseMemberRole,
  requireApproval: false,
  expireDays: 7
})
const inviteRules: FormRules = {
  role: [{ required: true, message: '请选择角色', trigger: 'change' }],
  expireDays: [{ required: true, message: '请选择有效期', trigger: 'blur' }]
}
const createdInviteLink = ref('')

// 移动相关状态
const showMoveDialog = ref(false)
const folderTree = ref<FolderTreeResponse[]>([])
const selectedTargetFolderId = ref<string | null>(null)
const loadingFolderTree = ref(false)
const moving = ref(false)

// 上传类型
const uploadType = ref<'local' | 'image'>('local')
const uploadFileList = ref<any[]>([])

// 表单
const folderFormRef = ref<FormInstance>()
const renameFormRef = ref<FormInstance>()
const noteFormRef = ref<FormInstance>()
const linkFormRef = ref<FormInstance>()
const uploadRef = ref<UploadInstance>()

const folderForm = reactive({ name: '' })
const renameForm = reactive({ name: '' })
const noteForm = reactive({ title: '', content: '' })
const linkForm = reactive({ url: '', title: '' })

const currentRenameItem = ref<{ type: 'folder' | 'document', id: string } | null>(null)
const members = ref<KnowledgeBaseMember[]>([])

const searchKeyword = ref('')

// 表单验证规则
const folderRules: FormRules = {
  name: [{ required: true, message: '请输入文件夹名称', trigger: 'blur' }]
}

const renameRules: FormRules = {
  name: [{ required: true, message: '请输入名称', trigger: 'blur' }]
}

const linkRules: FormRules = {
  url: [
    { required: true, message: '请输入网页链接', trigger: 'blur' },
    { type: 'url', message: '请输入正确的网址', trigger: 'blur' }
  ]
}

const noteRules: FormRules = {
  title: [{ required: true, message: '请输入笔记标题', trigger: 'blur' }],
  content: [{ required: true, message: '请输入笔记内容', trigger: 'blur' }]
}

// 上传相关
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
  const data: Record<string, string> = { knowledgeBaseId: knowledgeBaseId.value }
  if (currentFolderId.value) {
    data.folderId = currentFolderId.value
  }
  return data
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

// 加载文档列表
async function loadDocuments() {
  try {
    // 搜索时：不限制文件夹，搜索整个知识库
    // 非搜索时：只加载当前文件夹的文档
    const { items } = await getDocuments({
      knowledgeBaseId: knowledgeBaseId.value,
      folderId: (searchKeyword.value ? undefined : currentFolderId.value) || undefined,
      page: 1,
      pageSize: 1000,
      keyword: searchKeyword.value || undefined
    })
    documents.value = items
  } catch (error: any) {
    console.error('Failed to load documents:', error)
  }
}

// 加载文件夹列表
async function loadFolders() {
  try {
    // 搜索时不显示文件夹
    if (searchKeyword.value) {
      folders.value = []
      return
    }
    const data = await getFolderList(knowledgeBaseId.value, currentFolderId.value || undefined)
    folders.value = data
  } catch (error: any) {
    console.error('Failed to load folders:', error)
  }
}

// 加载当前视图
async function loadCurrentView() {
  loading.value = true
  try {
    await Promise.all([loadFolders(), loadDocuments()])
  } finally {
    loading.value = false
  }
}

// 返回上一页
function goBack() {
  router.push('/knowledge')
}

// 导航到根目录
function navigateToRoot() {
  currentFolderId.value = null
  breadcrumbFolders.value = []
  loadCurrentView()
}

// 导航到文件夹
function navigateToFolder(index: number) {
  // 保留到指定索引的面包屑
  breadcrumbFolders.value = breadcrumbFolders.value.slice(0, index + 1)
  const targetFolder = breadcrumbFolders.value[index]
  currentFolderId.value = targetFolder.id === 'root' ? null : targetFolder.id
  loadCurrentView()
}

// 进入文件夹
function enterFolder(folder: FolderType) {
  breadcrumbFolders.value.push(folder)
  currentFolderId.value = folder.id
  selectedItems.value = []
  loadCurrentView()
}

// 文件夹点击
function handleFolderClick(folder: FolderType) {
  const idx = selectedItems.value.indexOf(folder.id)
  if (idx > -1) {
    selectedItems.value.splice(idx, 1)
  } else {
    selectedItems.value.push(folder.id)
  }
}

// 文档点击
function handleDocClick(doc: DocType) {
  const idx = selectedItems.value.indexOf(doc.id)
  if (idx > -1) {
    selectedItems.value.splice(idx, 1)
  } else {
    selectedItems.value.push(doc.id)
  }
}

// 选择变化
function handleSelectionChange() {
  // v-model 会自动更新数组
}

// 新建命令处理
function handleNewCommand(command: string) {
  switch (command) {
    case 'folder':
      showFolderDialog.value = true
      break
    case 'note':
      showNoteDialog.value = true
      break
    case 'upload':
      uploadType.value = 'local'
      showUploadDialog.value = true
      break
    case 'camera':
      ElMessage.info('拍照功能待实现')
      break
    case 'image':
      uploadType.value = 'image'
      showUploadDialog.value = true
      break
    case 'record':
      ElMessage.info('录音功能待实现')
      break
    case 'wechat':
      ElMessage.info('微信文件功能待实现')
      break
    case 'link':
      showLinkDialog.value = true
      break
  }
}

// 创建文件夹
async function handleCreateFolder() {
  if (!folderFormRef.value) return
  await folderFormRef.value.validate(async (valid) => {
    if (valid) {
      folderLoading.value = true
      try {
        await createFolder({
          knowledgeBaseId: knowledgeBaseId.value,
          parentFolderId: currentFolderId.value || undefined,
          name: folderForm.name
        })
        ElMessage.success('文件夹创建成功')
        showFolderDialog.value = false
        folderForm.name = ''
        await loadCurrentView()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '创建失败')
      } finally {
        folderLoading.value = false
      }
    }
  })
}

// 重命名
async function handleRename() {
  if (!renameFormRef.value || !currentRenameItem.value) return
  await renameFormRef.value.validate(async (valid) => {
    if (valid) {
      renameLoading.value = true
      try {
        if (currentRenameItem.value.type === 'folder') {
          await updateFolder(currentRenameItem.value.id, { name: renameForm.name })
        }
        ElMessage.success('重命名成功')
        showRenameDialog.value = false
        renameForm.name = ''
        currentRenameItem.value = null
        await loadCurrentView()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '重命名失败')
      } finally {
        renameLoading.value = false
      }
    }
  })
}

// 文件夹命令处理
async function handleFolderCommand(command: string, folder: FolderType) {
  switch (command) {
    case 'open':
      enterFolder(folder)
      break
    case 'rename':
      currentRenameItem.value = { type: 'folder', id: folder.id }
      renameForm.name = folder.name
      showRenameDialog.value = true
      break
    case 'move':
      // 选中当前文件夹后打开移动对话框
      selectedItems.value = [folder.id]
      await openMoveDialog()
      break
    case 'delete':
      await handleDeleteFolder(folder)
      break
  }
}

// 文档命令处理
async function handleDocCommand(command: string, doc: DocType) {
  switch (command) {
    case 'open':
      ElMessage.info('打开文档功能待实现')
      break
    case 'download':
      ElMessage.info('下载功能待实现')
      break
    case 'move':
      // 选中当前文档后打开移动对话框
      selectedItems.value = [doc.id]
      await openMoveDialog()
      break
    case 'delete':
      await handleDeleteDocument(doc)
      break
  }
}

// 删除文件夹
async function handleDeleteFolder(folder: FolderType) {
  try {
    await ElMessageBox.confirm(`确定要删除文件夹"${folder.name}"吗？`, '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteFolder(folder.id)
    ElMessage.success('删除成功')
    // 从选中项中移除
    const index = selectedItems.value.indexOf(folder.id)
    if (index > -1) {
      selectedItems.value.splice(index, 1)
    }
    await loadCurrentView()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '删除失败')
    }
  }
}

// 删除文档
async function handleDeleteDocument(doc: DocType) {
  try {
    await ElMessageBox.confirm(`确定要删除文档"${doc.title}"吗？`, '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteDocument(doc.id)
    ElMessage.success('删除成功')
    // 从选中项中移除
    const index = selectedItems.value.indexOf(doc.id)
    if (index > -1) {
      selectedItems.value.splice(index, 1)
    }
    await loadCurrentView()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '删除失败')
    }
  }
}

// 批量删除
async function handleBatchDelete() {
  try {
    await ElMessageBox.confirm(`确定要删除选中的 ${selectedItems.value.length} 项吗？`, '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })

    let successCount = 0
    let failCount = 0

    for (const id of selectedItems.value) {
      try {
        const folder = folders.value.find(f => f.id === id)
        const doc = documents.value.find(d => d.id === id)
        if (folder) {
          await deleteFolder(folder.id)
        } else if (doc) {
          await deleteDocument(doc.id)
        }
        successCount++
      } catch (err: any) {
        console.error(`删除 ${id} 失败:`, err)
        failCount++
      }
    }

    if (failCount === 0) {
      ElMessage.success(`成功删除 ${successCount} 个项目`)
    } else if (successCount === 0) {
      ElMessage.error(`删除失败，请重试`)
    } else {
      ElMessage.warning(`部分成功: ${successCount} 个成功, ${failCount} 个失败`)
    }

    selectedItems.value.splice(0, selectedItems.value.length)
    await loadCurrentView()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '删除失败')
    }
  }
}

// 批量移动
async function handleBatchMove() {
  if (selectedItems.value.length === 0) return
  await openMoveDialog()
}

// 打开移动对话框
async function openMoveDialog() {
  selectedTargetFolderId.value = null
  showMoveDialog.value = true
  await loadFolderTree()
}

// 加载文件夹树
async function loadFolderTree() {
  loadingFolderTree.value = true
  try {
    const tree = await getFolderTree(knowledgeBaseId.value)
    // 添加根目录选项
    folderTree.value = [{
      id: 'root',
      name: '根目录',
      documentCount: 0,
      children: tree
    }]
  } catch (error: any) {
    console.error('Failed to load folder tree:', error)
    ElMessage.error('加载文件夹树失败')
  } finally {
    loadingFolderTree.value = false
  }
}

// 选择目标文件夹
function handleFolderSelect(data: any) {
  // 批量移动时检查是否有选中项在目标文件夹中
  if (selectedItems.value.length > 0) {
    const selectedIds = new Set(selectedItems.value)
    if (selectedIds.has(data.id)) {
      ElMessage.warning('不能移动到包含选中项的文件夹')
      return
    }
  }
  selectedTargetFolderId.value = data.id === 'root' ? null : data.id
}

// 获取移动对话框标题
function getMoveDialogTitle() {
  const count = selectedItems.value.length
  if (count === 0) return '移动'
  if (count === 1) {
    const folder = folders.value.find(f => f.id === selectedItems.value[0])
    const doc = documents.value.find(d => d.id === selectedItems.value[0])
    const name = folder?.name || doc?.title || '项目'
    return `移动 - ${name}`
  }
  return `批量移动 (${count} 项)`
}

// 获取移动描述
function getMoveDescription() {
  const count = selectedItems.value.length
  if (count === 0) return '请先选择要移动的项目'
  if (count === 1) return '选择目标文件夹：'
  return `已选择 ${count} 个项目，请选择目标文件夹：`
}

// 检查节点是否应该禁用
function isMoveDisabled(data: any) {
  const selectedIds = new Set(selectedItems.value)
  return selectedIds.has(data.id)
}

// 确认移动
async function handleConfirmMove() {
  if (selectedItems.value.length === 0) {
    ElMessage.warning('请先选择要移动的项目')
    return
  }

  const selectedIds = new Set(selectedItems.value)
  // 防止移动到选中的文件夹中
  if (selectedTargetFolderId.value && selectedIds.has(selectedTargetFolderId.value)) {
    ElMessage.warning('不能移动到包含选中项的文件夹')
    return
  }

  moving.value = true
  let successCount = 0
  let failCount = 0

  try {
    // 分别移动文件夹和文档
    for (const id of selectedItems.value) {
      try {
        const folder = folders.value.find(f => f.id === id)
        const doc = documents.value.find(d => d.id === id)

        if (folder) {
          await moveFolder(id, {
            parentFolderId: selectedTargetFolderId.value || undefined
          })
        } else if (doc) {
          await moveDocument(id, {
            folderId: selectedTargetFolderId.value || undefined
          })
        }
        successCount++
      } catch (err: any) {
        console.error(`移动 ${id} 失败:`, err)
        failCount++
      }
    }

    if (failCount === 0) {
      ElMessage.success(`成功移动 ${successCount} 个项目`)
    } else if (successCount === 0) {
      ElMessage.error(`移动失败，请重试`)
    } else {
      ElMessage.warning(`部分成功: ${successCount} 个成功, ${failCount} 个失败`)
    }

    showMoveDialog.value = false
    selectedItems.value.splice(0, selectedItems.value.length)
    await loadCurrentView()
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || '移动失败')
  } finally {
    moving.value = false
  }
}

// 文件选择
function handleFileChange() {
  // 文件列表通过 v-model:file-list 自动维护
}

// 上传对话框关闭时清空文件列表
function handleUploadDialogClose() {
  uploadFileList.value = []
}

// 上传
async function handleUpload() {
  if (!uploadRef.value) {
    ElMessage.error('上传组件未初始化')
    return
  }

  if (!uploadFileList.value || uploadFileList.value.length === 0) {
    ElMessage.warning('请选择要上传的文件')
    return
  }

  uploading.value = true
  try {
    for (const fileItem of uploadFileList.value) {
      const file = fileItem.raw
      if (!file) continue

      const formData = new FormData()
      formData.append('file', file)
      formData.append('knowledgeBaseId', knowledgeBaseId.value)
      if (currentFolderId.value) {
        formData.append('folderId', currentFolderId.value)
      }

      const response = await fetch(uploadUrl.value, {
        method: 'POST',
        headers: {
          ...(uploadHeaders.value['Authorization'] ? { 'Authorization': uploadHeaders.value['Authorization'] } : {}),
          ...(uploadHeaders.value['X-Tenant-Id'] ? { 'X-Tenant-Id': uploadHeaders.value['X-Tenant-Id'] } : {})
        },
        body: formData
      })

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({ message: '上传失败' }))
        throw new Error(errorData.message || '上传失败')
      }
    }
    ElMessage.success('上传成功')
    showUploadDialog.value = false
    uploadFileList.value = []
    await loadCurrentView()
  } catch (error: any) {
    ElMessage.error(error.message || '上传失败')
    console.error('上传失败:', error)
  } finally {
    uploading.value = false
  }
}

// 添加网页链接
async function handleAddLink() {
  if (!linkFormRef.value) return
  await linkFormRef.value.validate(async (valid) => {
    if (valid) {
      linkLoading.value = true
      try {
        await createWebLink({
          knowledgeBaseId: knowledgeBaseId.value,
          folderId: currentFolderId.value || undefined,
          title: linkForm.title || linkForm.url,
          url: linkForm.url
        })
        ElMessage.success('添加成功')
        showLinkDialog.value = false
        linkForm.url = ''
        linkForm.title = ''
        await loadCurrentView()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '添加失败')
      } finally {
        linkLoading.value = false
      }
    }
  })
}

// 创建笔记
async function handleCreateNote() {
  if (!noteFormRef.value) return
  await noteFormRef.value.validate(async (valid) => {
    if (valid) {
      noteLoading.value = true
      try {
        await createNote({
          knowledgeBaseId: knowledgeBaseId.value,
          folderId: currentFolderId.value || undefined,
          title: noteForm.title,
          content: noteForm.content
        })
        ElMessage.success('笔记创建成功')
        showNoteDialog.value = false
        noteForm.title = ''
        noteForm.content = ''
        await loadCurrentView()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '创建失败')
      } finally {
        noteLoading.value = false
      }
    }
  })
}

// 搜索
function handleSearch() {
  if (searchKeyword.value) {
    // 搜索时清空当前文件夹选择，显示所有搜索结果
    currentFolderId.value = null
    breadcrumbFolders.value = []
  }
  loadCurrentView()
}

// 清除搜索
function clearSearch() {
  searchKeyword.value = ''
  currentFolderId.value = null
  breadcrumbFolders.value = []
  loadCurrentView()
}

// 右键菜单
function handleContextMenu(event: MouseEvent, item: FolderType | Document, type: 'folder' | 'document') {
  // 可以在这里实现自定义右键菜单
  event.preventDefault()
}

// 加载成员
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

// 移除成员
async function handleRemoveMember(member: KnowledgeBaseMember) {
  try {
    await ElMessageBox.confirm(`确定要移除成员"${member.userName || member.userId}"吗？`, '提示', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })
    await removeKnowledgeBaseMember(knowledgeBaseId.value, member.userId)
    ElMessage.success('移除成功')
    await loadMembers()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '移除成员失败')
    }
  }
}

// 成员标签切换
function handleMembersTabChange(tabName: string) {
  if (tabName === 'invitations' && invitations.value.length === 0) {
    loadInvitations()
  }
}

// 加载邀请列表
async function loadInvitations() {
  loadingInvitations.value = true
  try {
    const { items } = await getInvitations(knowledgeBaseId.value, {
      status: invitationStatusFilter.value,
      page: 1,
      pageSize: 100
    })
    invitations.value = items
  } catch (error: any) {
    console.error('Failed to load invitations:', error)
    ElMessage.error(error.response?.data?.message || '加载邀请列表失败')
  } finally {
    loadingInvitations.value = false
  }
}

// 创建邀请
async function handleCreateInvitation() {
  if (!inviteFormRef.value) return
  await inviteFormRef.value.validate(async (valid) => {
    if (valid) {
      creatingInvitation.value = true
      try {
        const result = await createInvitation({
          knowledgeBaseId: knowledgeBaseId.value,
          email: inviteForm.email || undefined,
          role: inviteForm.role,
          requireApproval: inviteForm.requireApproval,
          expireDays: inviteForm.expireDays
        })
        createdInviteLink.value = result.inviteLink
        showInviteDialog.value = false
        showInviteResultDialog.value = true
        // 重置表单
        inviteForm.email = ''
        inviteForm.role = 3
        inviteForm.requireApproval = false
        inviteForm.expireDays = 7
        await loadInvitations()
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || '创建邀请失败')
      } finally {
        creatingInvitation.value = false
      }
    }
  })
}

// 复制邀请链接（从列表）
async function handleCopyInviteLink(invitation: Invitation) {
  try {
    await navigator.clipboard.writeText(invitation.inviteLink)
    ElMessage.success('邀请链接已复制到剪贴板')
  } catch (error) {
    ElMessage.error('复制失败，请手动复制')
  }
}

// 复制邀请链接（从结果对话框）
async function handleCopyInviteLinkInput() {
  try {
    await navigator.clipboard.writeText(createdInviteLink.value)
    ElMessage.success('邀请链接已复制到剪贴板')
  } catch (error) {
    ElMessage.error('复制失败，请手动复制')
  }
}

// 取消邀请
async function handleCancelInvitation(invitation: Invitation) {
  try {
    await ElMessageBox.confirm(`确定要取消邀请"${invitation.email || invitation.code}"吗？`, '提示', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })
    await cancelInvitation(invitation.id)
    ElMessage.success('邀请已取消')
    await loadInvitations()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '取消邀请失败')
    }
  }
}

// 审核邀请
async function handleApproveInvitation(invitation: Invitation, approved: boolean) {
  try {
    await ElMessageBox.confirm(
      `确定要${approved ? '通过' : '拒绝'}此邀请吗？`,
      '提示',
      {
        type: 'warning',
        confirmButtonText: '确定',
        cancelButtonText: '取消'
      }
    )
    await approveInvitation(invitation.id, approved)
    ElMessage.success(approved ? '已通过邀请' : '已拒绝邀请')
    await loadInvitations()
    await loadMembers()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error(error.response?.data?.message || '操作失败')
    }
  }
}

// 获取邀请状态类型
function getInvitationStatusType(status: number): 'success' | 'info' | 'warning' | 'danger' {
  const types: Record<number, 'success' | 'info' | 'warning' | 'danger'> = {
    0: 'warning',  // Pending
    1: 'success',  // Accepted
    2: 'danger',   // Rejected
    3: 'info',     // Expired
    4: 'info'      // Canceled
  }
  return types[status] || 'info'
}

// 获取邀请状态标签
function getInvitationStatusLabel(status: number, inviteeUserId?: string): string {
  if (status === 0 && inviteeUserId) {
    return '待审核'
  }
  const labels: Record<number, string> = {
    0: '待处理',
    1: '已加入',
    2: '已拒绝',
    3: '已过期',
    4: '已取消'
  }
  return labels[status] || '未知'
}

// 格式化邀请日期
function formatInvitationDate(dateStr: string): string {
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// 获取文件图标
function getFileIcon(contentType: number) {
  switch (contentType) {
    case 1: return Document
    case 2: return Document
    case 3: return Document
    case 4: return Document
    case 5: return Document
    case 6: return Picture
    case 7: return VideoCamera
    case 8: return Headset
    default: return Document
  }
}

// 获取文件图标颜色
function getFileIconColor(contentType: number) {
  switch (contentType) {
    case 1: return '#d14f28'
    case 2: return '#295497'
    case 3: return '#d24726'
    case 4: return '#083fa1'
    case 6: return '#67c23a'
    case 7: return '#e6a23c'
    case 8: return '#909399'
    default: return '#409eff'
  }
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

// 格式化文件大小
function formatFileSize(bytes: number): string {
  if (!bytes || bytes === 0) return '-'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const k = 1024
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  const size = bytes / Math.pow(k, i)
  return `${size.toFixed(i === 0 ? 0 : 1)} ${units[i]}`
}

// 格式化日期
function formatDate(dateStr: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

// 处理文档进度更新
function handleDocumentProgress(progress: DocumentProgress) {
  console.log('[KnowledgeDetail] 收到文档进度:', progress)

  const docIndex = documents.value.findIndex(d => d.id === progress.documentId)
  if (docIndex === -1) {
    console.warn('[KnowledgeDetail] 找不到文档:', progress.documentId)
    return
  }

  // 更新文档状态
  const statusMap: Record<string, number> = {
    'Uploaded': 1,
    'Parsing': 2,
    'Parsed': 3,
    'Indexing': 4,
    'Indexed': 5,
    'Failed': 6
  }

  const newStatus = statusMap[progress.status] || 1
  console.log(`[KnowledgeDetail] 更新文档状态: ${documents.value[docIndex].title} ${documents.value[docIndex].status} -> ${newStatus}`)

  documents.value = documents.value.map((doc, i) =>
    i === docIndex ? { ...doc, status: newStatus } : doc
  )

  // 显示进度提示
  if (progress.progress && progress.progress > 0) {
    ElMessage.info(`${progress.title}: ${progress.stage} (${progress.progress}%)`)
  }

  // 如果文档已完成
  if (progress.status === 'Indexed') {
    ElMessage.success(`${progress.title} 已完成处理`)
  }
  // 如果文档处理失败
  else if (progress.status === 'Failed') {
    ElMessage.error(`${progress.title} 处理失败：${progress.error || '未知错误'}`)
  }
}

// 初始化 SignalR
async function initializeSignalR() {
  try {
    const userId = userStore.userInfo?.id || userStore.tenantId
    if (!userId) {
      console.warn('[KnowledgeDetail] 没有 user ID，无法连接 SignalR')
      return
    }

    if (!isConnected()) {
      await initSignalR(userId)
    }

    onDocumentProgress(handleDocumentProgress)
    console.log('[KnowledgeDetail] SignalR 文档进度监听已设置')
  } catch (error) {
    console.error('[KnowledgeDetail] SignalR 初始化失败:', error)
  }
}

onMounted(async () => {
  await initializeSignalR()
  loadKnowledgeBase()
  loadCurrentView()
  loadMembers()
})

onUnmounted(() => {
  console.log('[KnowledgeDetail] 组件卸载')
})
</script>

<style scoped lang="scss">
.knowledge-detail {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
  background: #f5f7fa;
  padding: 16px;
  border-radius: 8px;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: white;
  padding: 16px;
  border-radius: 8px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);

  .header-left {
    display: flex;
    align-items: center;
    gap: 12px;
  }

  .kb-info {
    display: flex;
    align-items: center;
    gap: 8px;

    .kb-icon {
      color: #409eff;
    }

    .kb-name {
      font-size: 18px;
      font-weight: 600;
    }
  }

  .header-actions {
    display: flex;
    gap: 8px;
  }
}

.description {
  background: white;
  padding: 12px 16px;
  border-radius: 8px;
  color: #606266;
  font-size: 14px;
}

.breadcrumb-bar {
  background: white;
  padding: 12px 16px;
  border-radius: 8px;

  :deep(.el-breadcrumb__item) {
    cursor: pointer;

    &:hover {
      .el-breadcrumb__inner {
        color: #409eff;
      }
    }
  }
}

.search-result-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #ecf5ff;
  padding: 10px 16px;
  border-radius: 8px;
  border: 1px solid #d9ecff;

  .search-text {
    display: flex;
    align-items: center;
    gap: 8px;
    color: #409eff;
    font-size: 14px;

    .el-icon {
      font-size: 16px;
    }
  }
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: white;
  padding: 12px 16px;
  border-radius: 8px;

  .toolbar-left {
    flex: 1;
  }

  .search-input {
    max-width: 400px;
  }

  .toolbar-right {
    display: flex;
    gap: 8px;
  }
}

.file-list-container {
  flex: 1;
  background: white;
  border-radius: 8px;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.file-list {
  flex: 1;
  overflow-y: auto;

  .file-list-header {
    display: grid;
    grid-template-columns: 1fr 100px 180px 80px;
    gap: 16px;
    padding: 12px 16px;
    background: #f5f7fa;
    border-bottom: 1px solid #e4e7ed;
    font-weight: 500;
    color: #606266;
    font-size: 13px;
  }

  .file-item {
    display: grid;
    grid-template-columns: 1fr 100px 180px 80px;
    gap: 16px;
    padding: 12px 16px;
    border-bottom: 1px solid #f5f7fa;
    cursor: pointer;
    transition: background 0.2s;
    align-items: center;

    &:hover {
      background: #f5f7fa;
    }

    &.selected {
      background: #ecf5ff;
    }

    .col-name {
      display: flex;
      align-items: center;
      gap: 8px;
      overflow: hidden;

      .file-icon {
        flex-shrink: 0;
        font-size: 20px;
      }

      .folder-icon {
        color: #f7ba2a;
      }

      .file-name {
        flex: 1;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .status-tag {
        flex-shrink: 0;
      }
    }

    .col-size,
    .col-date {
      color: #909399;
      font-size: 13px;
    }

    .col-actions {
      display: flex;
      justify-content: flex-end;
    }
  }
}

.file-grid {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(140px, 1fr));
  gap: 16px;

  .grid-item {
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 16px;
    border-radius: 8px;
    cursor: pointer;
    transition: all 0.2s;

    &:hover {
      background: #f5f7fa;
    }

    &.selected {
      background: #ecf5ff;
    }

    .grid-item-icon {
      margin-bottom: 8px;
      color: #409eff;
    }

    .grid-item-name {
      font-size: 13px;
      text-align: center;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      width: 100%;
      color: #303133;
    }

    .grid-item-status {
      margin-top: 4px;
    }
  }

  .folder-item .grid-item-icon {
    color: #f7ba2a;
  }
}

.batch-bar {
  position: fixed;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  background: #409eff;
  color: white;
  padding: 12px 24px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 1000;

  .batch-actions {
    display: flex;
    gap: 8px;

    .el-button {
      background: white;
      color: #409eff;
      border: none;

      &:hover {
        background: #ecf5ff;
      }
    }
  }
}

.upload-area {
  :deep(.el-upload-dragger) {
    padding: 40px;
  }

  .upload-icon {
    font-size: 48px;
    color: #409eff;
    margin-bottom: 16px;
  }

  .upload-text {
    color: #606266;
  }
}

.members-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
}

.members-toolbar {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
  gap: 10px;
}

.form-tip {
  margin-left: 8px;
  color: #909399;
  font-size: 13px;
}

.invite-result {
  text-align: center;
  padding: 20px 0;

  .success-icon {
    color: #67c23a;
    margin-bottom: 16px;
  }

  p {
    margin: 12px 0;
    color: #606266;
  }

  .invite-link-input {
    margin: 16px 0;
  }

  .invite-tip {
    font-size: 13px;
    color: #909399;
  }
}

.move-dialog-content {
  .move-tip {
    margin: 0 0 16px 0;
    font-size: 14px;
    color: #606266;
  }

  .folder-tree {
    min-height: 200px;
    max-height: 400px;
    overflow-y: auto;
    border: 1px solid #dcdfe6;
    border-radius: 4px;
    padding: 10px;
  }

  .tree-node {
    display: flex;
    align-items: center;
    gap: 6px;
    flex: 1;

    .el-icon {
      color: #f7ba2a;
    }

    .tree-node-label {
      flex: 1;
    }

    .tree-node-count {
      color: #909399;
      font-size: 12px;
    }
  }

  .move-selection {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-top: 12px;
    padding: 8px 12px;
    background: #f5f7fa;
    border-radius: 4px;

    .selection-text {
      font-size: 13px;
      color: #606266;
    }
  }
}
</style>
