<template>
  <div class="profile-container">
    <el-card class="profile-card">
      <div class="profile-header">
        <div class="avatar-section">
          <el-avatar :src="userInfo?.avatar" :size="100" @click="handleAvatarClick">
            <el-icon size="50"><User /></el-icon>
          </el-avatar>
          <div class="avatar-tip">点击更换头像</div>
        </div>
        <div class="user-info">
          <h2>{{ userInfo?.nickname || userInfo?.username }}</h2>
          <p>@{{ userInfo?.username }}</p>
        </div>
      </div>

      <el-tabs v-model="activeTab" class="profile-tabs">
        <el-tab-pane label="基本信息" name="basic">
          <el-form
            ref="formRef"
            :model="form"
            :rules="rules"
            label-width="80px"
            class="profile-form"
          >
            <el-form-item label="用户名" prop="username">
              <el-input v-model="form.username" disabled />
            </el-form-item>
            <el-form-item label="昵称" prop="nickname">
              <el-input v-model="form.nickname" placeholder="请输入昵称" />
            </el-form-item>
            <el-form-item label="手机号" prop="phone">
              <el-input v-model="form.phone" placeholder="请输入手机号" />
            </el-form-item>
            <el-form-item label="邮箱" prop="email">
              <el-input v-model="form.email" placeholder="请输入邮箱" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="loading" @click="handleUpdate">
                保存修改
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>

        <el-tab-pane label="修改密码" name="password">
          <el-form
            ref="passwordFormRef"
            :model="passwordForm"
            :rules="passwordRules"
            label-width="80px"
            class="profile-form"
          >
            <el-form-item label="旧密码" prop="oldPassword">
              <el-input
                v-model="passwordForm.oldPassword"
                type="password"
                show-password
                placeholder="请输入旧密码"
              />
            </el-form-item>
            <el-form-item label="新密码" prop="newPassword">
              <el-input
                v-model="passwordForm.newPassword"
                type="password"
                show-password
                placeholder="请输入新密码"
              />
            </el-form-item>
            <el-form-item label="确认密码" prop="confirmPassword">
              <el-input
                v-model="passwordForm.confirmPassword"
                type="password"
                show-password
                placeholder="请再次输入新密码"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="passwordLoading" @click="handleChangePassword">
                修改密码
              </el-button>
            </el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- Avatar Upload Dialog -->
    <el-dialog v-model="avatarDialogVisible" title="更换头像" width="400px">
      <el-upload
        ref="avatarUploadRef"
        :auto-upload="false"
        :show-file-list="false"
        :on-change="handleAvatarChange"
        accept="image/*"
        drag
        class="avatar-uploader"
      >
        <el-icon class="upload-icon"><UploadFilled /></el-icon>
        <div class="upload-text">拖拽图片到此处或点击上传</div>
        <template #tip>
          <div class="upload-tip">支持 JPG、PNG 格式，大小不超过 2MB</div>
        </template>
      </el-upload>

      <div v-if="previewAvatar" class="avatar-preview">
        <el-avatar :src="previewAvatar" :size="120" />
      </div>

      <template #footer>
        <el-button @click="avatarDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="avatarLoading" @click="handleAvatarUpload">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { User, UploadFilled } from '@element-plus/icons-vue'
import { useUserStore } from '../stores/user'
import { updateUserInfo, changePassword, updateAvatar } from '../api/user'

const userStore = useUserStore()

const activeTab = ref('basic')
const loading = ref(false)
const passwordLoading = ref(false)
const avatarLoading = ref(false)
const avatarDialogVisible = ref(false)
const previewAvatar = ref('')
const avatarFile = ref<File | null>(null)

const formRef = ref<FormInstance>()
const passwordFormRef = ref<FormInstance>()
const avatarUploadRef = ref()

const userInfo = userStore.userInfo

const form = reactive({
  username: userInfo?.username || '',
  nickname: userInfo?.nickname || '',
  phone: userInfo?.phone || '',
  email: userInfo?.email || ''
})

const passwordForm = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})

const rules: FormRules = {
  nickname: [
    { max: 50, message: '昵称长度不能超过 50 个字符', trigger: 'blur' }
  ],
  phone: [
    { pattern: /^1[3-9]\d{9}$/, message: '手机号格式不正确', trigger: 'blur' }
  ],
  email: [
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' }
  ]
}

const validateConfirmPassword = (rule: any, value: any, callback: any) => {
  if (value === '') {
    callback(new Error('请再次输入密码'))
  } else if (value !== passwordForm.newPassword) {
    callback(new Error('两次输入密码不一致'))
  } else {
    callback()
  }
}

const passwordRules: FormRules = {
  oldPassword: [
    { required: true, message: '请输入旧密码', trigger: 'blur' }
  ],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, max: 20, message: '密码长度为 6-20 个字符', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, validator: validateConfirmPassword, trigger: 'blur' }
  ]
}

async function handleUpdate() {
  if (!formRef.value) return

  await formRef.value.validate(async (valid) => {
    if (valid) {
      loading.value = true
      try {
        const updated = await updateUserInfo(form)
        userStore.setUserInfo(updated)
        ElMessage.success('保存成功')
      } catch (error) {
        console.error('Update failed:', error)
      } finally {
        loading.value = false
      }
    }
  })
}

async function handleChangePassword() {
  if (!passwordFormRef.value) return

  await passwordFormRef.value.validate(async (valid) => {
    if (valid) {
      passwordLoading.value = true
      try {
        await changePassword(passwordForm.oldPassword, passwordForm.newPassword)
        ElMessage.success('密码修改成功，请重新登录')

        // Reset form
        passwordForm.oldPassword = ''
        passwordForm.newPassword = ''
        passwordForm.confirmPassword = ''
      } catch (error) {
        console.error('Change password failed:', error)
      } finally {
        passwordLoading.value = false
      }
    }
  })
}

function handleAvatarClick() {
  avatarDialogVisible.value = true
  previewAvatar.value = ''
  avatarFile.value = null
}

function handleAvatarChange(file: any) {
  const isImage = file.raw.type.startsWith('image/')
  const isLt2M = file.raw.size / 1024 / 1024 < 2

  if (!isImage) {
    ElMessage.error('只能上传图片文件!')
    return
  }
  if (!isLt2M) {
    ElMessage.error('图片大小不能超过 2MB!')
    return
  }

  avatarFile.value = file.raw
  previewAvatar.value = URL.createObjectURL(file.raw)
}

async function handleAvatarUpload() {
  if (!avatarFile.value) {
    ElMessage.warning('请选择要上传的头像')
    return
  }

  avatarLoading.value = true
  try {
    const { url } = await updateAvatar(avatarFile.value)
    userStore.setUserInfo({ ...userInfo, avatar: url })
    ElMessage.success('头像更新成功')
    avatarDialogVisible.value = false
  } catch (error) {
    console.error('Upload avatar failed:', error)
  } finally {
    avatarLoading.value = false
  }
}

onMounted(() => {
  // Update form with latest user info
  if (userInfo) {
    form.username = userInfo.username
    form.nickname = userInfo.nickname || ''
    form.phone = userInfo.phone || ''
    form.email = userInfo.email || ''
  }
})
</script>

<style scoped lang="scss">
.profile-container {
  max-width: 800px;
  margin: 0 auto;
}

.profile-card {
  border-radius: 8px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.profile-header {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 32px 0;
  text-align: center;
  border-bottom: 1px solid #e4e7ed;
  margin-bottom: 24px;
}

.avatar-section {
  position: relative;
  cursor: pointer;

  .avatar-tip {
    position: absolute;
    bottom: -24px;
    left: 50%;
    transform: translateX(-50%);
    font-size: 12px;
    color: #909399;
    opacity: 0;
    transition: opacity 0.2s;
  }

  &:hover .avatar-tip {
    opacity: 1;
  }
}

.user-info {
  margin-top: 16px;

  h2 {
    margin: 0;
    font-size: 24px;
    font-weight: 500;
  }

  p {
    margin: 4px 0 0;
    color: #909399;
    font-size: 14px;
  }
}

.profile-form {
  max-width: 500px;
  margin: 0 auto;
  padding: 24px 0;
}

.avatar-uploader {
  :deep(.el-upload) {
    width: 100%;
  }

  :deep(.el-upload-dragger) {
    width: 100%;
    padding: 40px;
  }

  .upload-icon {
    font-size: 48px;
    color: #409eff;
  }

  .upload-text {
    margin-top: 16px;
    color: #606266;
  }

  .upload-tip {
    margin-top: 8px;
    font-size: 12px;
    color: #909399;
  }
}

.avatar-preview {
  display: flex;
  justify-content: center;
  margin: 24px 0;
}

@media (max-width: 768px) {
  .profile-container {
    padding: 0;
  }

  .profile-form {
    padding: 16px;
  }

  .profile-header {
    padding: 24px 0;
  }
}
</style>
