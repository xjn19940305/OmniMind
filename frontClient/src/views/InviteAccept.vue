<template>
  <div class="invite-page">
    <div class="invite-container">
      <div v-loading="loading" class="invite-card">
        <template v-if="!loading && invitation">
          <div class="invite-header">
            <el-icon class="invite-icon" :size="64"><Collection /></el-icon>
            <h1 class="invite-title">知识库邀请</h1>
          </div>

          <div class="invite-info">
            <div class="info-item">
              <span class="label">知识库名称</span>
              <span class="value">{{ invitation.knowledgeBaseName }}</span>
            </div>
            <div class="info-item">
              <span class="label">邀请人</span>
              <span class="value">{{ inviterName || '未知用户' }}</span>
            </div>
            <div class="info-item">
              <span class="label">角色</span>
              <el-tag :type="getMemberRoleType(invitation.role)" size="small">
                {{ getMemberRoleLabel(invitation.role) }}
              </el-tag>
            </div>
            <div class="info-item">
              <span class="label">需要审核</span>
              <el-tag :type="invitation.requireApproval ? 'warning' : 'success'" size="small">
                {{ invitation.requireApproval ? '是（接受后需管理员审核）' : '否（直接加入）' }}
              </el-tag>
            </div>
            <div v-if="invitation.email" class="info-item">
              <span class="label">邀请邮箱</span>
              <span class="value">{{ invitation.email }}</span>
            </div>
            <div class="info-item">
              <span class="label">有效期至</span>
              <span class="value">{{ formatDate(invitation.expiresAt) }}</span>
            </div>
          </div>

          <div v-if="isCurrentUserInvited" class="already-accepted">
            <el-icon color="#67c23a" :size="24"><SuccessFilled /></el-icon>
            <span>您已接受此邀请</span>
          </div>

          <div v-else-if="!isLoggedIn" class="invite-actions">
            <el-alert type="info" :closable="false" show-icon style="margin-bottom: 20px">
              请先登录以接受邀请
            </el-alert>
            <el-button type="primary" size="large" @click="goToLogin">
              前往登录
            </el-button>
          </div>

          <div v-else class="invite-actions">
            <div v-if="invitation.requireApproval" class="application-reason">
              <div class="reason-label">申请理由 <span class="optional">(选填)</span></div>
              <el-input
                v-model="applicationReason"
                type="textarea"
                :rows="3"
                placeholder="请简单说明您希望加入此知识库的原因"
                maxlength="500"
                show-word-limit
              />
            </div>
            <div class="action-buttons">
              <el-button type="primary" size="large" :loading="responding" @click="handleAccept(true)">
                接受邀请
              </el-button>
              <el-button size="large" :loading="responding" @click="handleAccept(false)">
                拒绝邀请
              </el-button>
            </div>
          </div>
        </template>

        <template v-else-if="!loading && error">
          <div class="error-state">
            <el-icon class="error-icon" :size="64"><CircleCloseFilled /></el-icon>
            <h2 class="error-title">邀请无效</h2>
            <p class="error-message">{{ error }}</p>
            <el-button type="primary" @click="goToKnowledge">前往知识库</el-button>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  Collection,
  SuccessFilled,
  CircleCloseFilled
} from '@element-plus/icons-vue'
import { getInvitation, respondToInvitation } from '../api/invitation'
import { useUserStore } from '../stores/user'
import type { Invitation, KnowledgeBaseMemberRole } from '../types'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const invitationCode = computed(() => route.params.code as string)

const loading = ref(true)
const responding = ref(false)
const invitation = ref<Invitation | null>(null)
const inviterName = ref('')
const isCurrentUserInvited = ref(false)
const applicationReason = ref('')
const error = ref('')

const isLoggedIn = computed(() => !!userStore.userInfo?.id)

async function loadInvitation() {
  loading.value = true
  error.value = ''
  try {
    const result = await getInvitation(invitationCode.value)
    invitation.value = result.invitation
    inviterName.value = result.inviterName || ''
    isCurrentUserInvited.value = result.isCurrentUserInvited
  } catch (err: any) {
    error.value = err.response?.data?.message || '邀请不存在或已过期'
  } finally {
    loading.value = false
  }
}

async function handleAccept(accept: boolean) {
  if (!invitation.value) return

  responding.value = true
  try {
    const result = await respondToInvitation({
      code: invitationCode.value,
      accept,
      applicationReason: accept && invitation.value.requireApproval ? applicationReason.value : undefined
    })

    if (accept) {
      if (result.requiresApproval) {
        ElMessage.success('已接受邀请，等待管理员审核通过')
      } else {
        ElMessage.success('已加入知识库')
      }
      // 跳转到知识库详情页
      router.push(`/knowledge/${invitation.value.knowledgeBaseId}`)
    } else {
      ElMessage.info('已拒绝邀请')
      router.push('/knowledge')
    }
  } catch (err: any) {
    ElMessage.error(err.response?.data?.message || '操作失败')
  } finally {
    responding.value = false
  }
}

function goToLogin() {
  // 保存邀请码到 sessionStorage，登录后可以返回
  sessionStorage.setItem('pendingInviteCode', invitationCode.value)
  router.push(`/login?redirect=/invite/${invitationCode.value}`)
}

function goToKnowledge() {
  router.push('/knowledge')
}

function getMemberRoleType(role: number): 'success' | 'info' | 'warning' | 'danger' {
  const types: Record<number, 'success' | 'info' | 'warning' | 'danger'> = {
    1: 'danger',
    2: 'warning',
    3: 'info'
  }
  return types[role] || 'info'
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
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
}

onMounted(() => {
  loadInvitation()
})
</script>

<style scoped lang="scss">
.invite-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  padding: 20px;
}

.invite-container {
  width: 100%;
  max-width: 500px;
}

.invite-card {
  background: white;
  border-radius: 16px;
  padding: 40px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
  min-height: 400px;
}

.invite-header {
  text-align: center;
  margin-bottom: 32px;

  .invite-icon {
    color: #667eea;
    margin-bottom: 16px;
  }

  .invite-title {
    font-size: 24px;
    font-weight: 600;
    color: #303133;
    margin: 0;
  }
}

.invite-info {
  .info-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 12px 0;
    border-bottom: 1px solid #f5f7fa;

    &:last-child {
      border-bottom: none;
    }

    .label {
      color: #909399;
      font-size: 14px;
    }

    .value {
      color: #303133;
      font-size: 14px;
      font-weight: 500;
    }
  }
}

.already-accepted {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
  padding: 32px 0;
  color: #67c23a;
  font-size: 16px;
  font-weight: 500;
}

.invite-actions {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding-top: 24px;
  border-top: 1px solid #f5f7fa;
  margin-top: 24px;

  .action-buttons {
    display: flex;
    justify-content: center;
    gap: 16px;

    .el-button {
      min-width: 120px;
    }
  }
}

.application-reason {
  width: 100%;
  margin-bottom: 16px;

  .reason-label {
    font-size: 14px;
    color: #606266;
    margin-bottom: 8px;

    .optional {
      color: #909399;
      font-size: 12px;
    }
  }
}

.error-state {
  text-align: center;
  padding: 40px 0;

  .error-icon {
    color: #f56c6c;
    margin-bottom: 16px;
  }

  .error-title {
    font-size: 20px;
    font-weight: 600;
    color: #303133;
    margin: 0 0 12px 0;
  }

  .error-message {
    color: #909399;
    margin-bottom: 24px;
  }
}
</style>
