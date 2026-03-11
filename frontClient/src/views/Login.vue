<template>
  <div class="login-shell">
    <div class="login-container">
    <div class="login-box">
      <div class="login-header">
        <h1>OmniMind</h1>
        <p>企业级多模态知识库平台</p>
      </div>

      <el-form
        ref="loginFormRef"
        :model="loginForm"
        :rules="loginRules"
        size="large"
        class="login-form"
      >
        <el-form-item prop="username">
          <el-input
            v-model="loginForm.username"
            placeholder="用户名 / 邮箱 / 手机号"
            prefix-icon="User"
            @keyup.enter="handleLogin"
          />
        </el-form-item>
        <el-form-item prop="password">
          <el-input
            v-model="loginForm.password"
            type="password"
            show-password
            placeholder="密码"
            prefix-icon="Lock"
            @keyup.enter="handleLogin"
          />
        </el-form-item>
        <el-form-item>
          <el-button
            type="primary"
            :loading="loading"
            class="login-btn"
            @click="handleLogin"
          >
            登录
          </el-button>
        </el-form-item>
      </el-form>
    </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { loginByUsername } from '../api/user'
import { useUserStore } from '../stores/user'

const router = useRouter()
const userStore = useUserStore()

const loading = ref(false)
const loginFormRef = ref<FormInstance>()

const loginForm = reactive({
  username: '',
  password: ''
})

const loginRules: FormRules = {
  username: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

async function handleLogin() {
  if (!loginFormRef.value) {
    return
  }

  await loginFormRef.value.validate(async (valid) => {
    if (!valid) {
      return
    }

    loading.value = true
    try {
      const response = await loginByUsername({
        username: loginForm.username.trim(),
        password: loginForm.password
      })

      userStore.setToken(response.token, response.refreshToken)
      userStore.setUserInfo({
        id: response.user.id,
        username: response.user.userName || response.user.username || '',
        nickname: response.user.nickName || response.user.nickname,
        phone: response.user.phoneNumber || response.user.phone,
        email: response.user.email,
        avatar: response.user.picture || response.user.avatar,
        createdAt: response.user.dateCreated || response.user.createdAt
      })

      router.push('/chat')
    } catch (error: any) {
      ElMessage.error(error.response?.data?.message || '登录失败')
    } finally {
      loading.value = false
    }
  })
}
</script>

<style scoped lang="scss">
.login-shell {
  min-height: 100vh;
  padding: clamp(16px, 4vw, 32px);
  background:
    radial-gradient(circle at top left, rgba(18, 120, 92, 0.28), transparent 35%),
    radial-gradient(circle at bottom right, rgba(210, 138, 66, 0.22), transparent 30%),
    linear-gradient(135deg, #f3efe4 0%, #dbe8e1 100%);
}

.login-container {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100vh - clamp(32px, 8vw, 64px));
}

.login-box {
  width: min(420px, 92vw);
  padding: 40px 36px;
  background: rgba(255, 255, 255, 0.92);
  border: 1px solid rgba(19, 55, 46, 0.08);
  border-radius: 24px;
  box-shadow: 0 24px 60px rgba(28, 47, 41, 0.12);
  backdrop-filter: blur(10px);
}

.login-header {
  margin-bottom: 28px;
  text-align: center;

  h1 {
    margin: 0;
    font-size: 34px;
    font-weight: 700;
    letter-spacing: 0.04em;
    color: #173d33;
  }

  p {
    margin: 10px 0 0;
    color: #5e756c;
    font-size: 14px;
  }
}

.login-form {
  margin-top: 12px;
}

.login-btn {
  width: 100%;
  height: 44px;
}

@media (max-width: 768px) {
  .login-box {
    width: 100%;
    padding: 28px 20px;
    border-radius: 20px;
  }
}
</style>
