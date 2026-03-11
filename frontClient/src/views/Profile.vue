<template>
  <div class="profile-shell">
    <div class="profile-container">
      <el-card class="profile-card">
      <div class="profile-header">
        <el-avatar :src="userInfo?.avatar" :size="100">
          <el-icon size="50"><User /></el-icon>
        </el-avatar>
        <div class="user-info">
          <h2>{{ userInfo?.nickname || userInfo?.username }}</h2>
          <p>@{{ userInfo?.username }}</p>
        </div>
      </div>

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
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="form.email" placeholder="请输入邮箱" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" @click="handleUpdate">
            保存修改
          </el-button>
        </el-form-item>
      </el-form>
      </el-card>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { User } from '@element-plus/icons-vue'
import { updateUserInfo } from '../api/user'
import { useUserStore } from '../stores/user'

const userStore = useUserStore()
const userInfo = userStore.userInfo

const loading = ref(false)
const formRef = ref<FormInstance>()

const form = reactive({
  username: userInfo?.username || '',
  nickname: userInfo?.nickname || '',
  email: userInfo?.email || ''
})

const rules: FormRules = {
  nickname: [
    { max: 50, message: '昵称长度不能超过 50 个字符', trigger: 'blur' }
  ],
  email: [
    { type: 'email', message: '邮箱格式不正确', trigger: 'blur' }
  ]
}

async function handleUpdate() {
  if (!formRef.value) return

  await formRef.value.validate(async (valid) => {
    if (!valid) return

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
  })
}

onMounted(() => {
  if (!userInfo) return

  form.username = userInfo.username
  form.nickname = userInfo.nickname || ''
  form.email = userInfo.email || ''
})
</script>

<style scoped lang="scss">
.profile-shell {
  min-height: 100%;
  padding: clamp(12px, 3vw, 28px);
  background:
    radial-gradient(circle at top left, rgba(14, 165, 233, 0.08), transparent 22%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.72), rgba(241, 245, 249, 0.9));
  border-radius: 28px;
}

.profile-container {
  max-width: 800px;
  margin: 0 auto;
}

.profile-card {
  border-radius: 24px;
  border: 1px solid rgba(148, 163, 184, 0.16);
  box-shadow: 0 24px 56px rgba(15, 23, 42, 0.08);
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

@media (max-width: 768px) {
  .profile-shell {
    padding: 0;
    background: transparent;
    border-radius: 0;
  }

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
