<template>
  <div class="login-container">
    <div class="login-box">
      <div class="login-header">
        <h1>OmniMind</h1>
        <p>多模态 AI 知识库</p>
      </div>

      <el-steps
        :active="currentStep"
        finish-status="success"
        class="login-steps"
      >
        <el-step title="输入手机号" />
        <el-step title="选择租户" />
        <el-step title="登录" />
      </el-steps>

      <!-- Step 1: 输入手机号和验证码 -->
      <div v-show="currentStep === 0" class="step-content">
        <el-form
          ref="phoneFormRef"
          :model="phoneForm"
          :rules="phoneRules"
          size="large"
          class="login-form"
        >
          <el-form-item prop="phone">
            <el-input
              v-model="phoneForm.phone"
              placeholder="请输入手机号"
              prefix-icon="Iphone"
            />
          </el-form-item>
          <el-form-item prop="code">
            <div class="code-input">
              <el-input
                v-model="phoneForm.code"
                placeholder="请输入验证码"
                prefix-icon="Key"
                @keyup.enter="handleVerifyCode"
              />
              <el-button
                :disabled="countdown > 0"
                :loading="sending"
                @click="handleSendCode"
              >
                {{ countdown > 0 ? `${countdown}秒` : "发送验证码" }}
              </el-button>
            </div>
          </el-form-item>
          <el-form-item>
            <el-button
              type="primary"
              :loading="loading"
              class="login-btn"
              @click="handleVerifyCode"
            >
              下一步
            </el-button>
          </el-form-item>
        </el-form>
      </div>

      <!-- Step 2: 选择租户 -->
      <div v-show="currentStep === 1" class="step-content">
        <div class="tenant-list">
          <el-empty
            v-if="availableTenants.length === 0"
            description="暂无可用租户"
          />
          <el-radio-group
            v-else
            v-model="selectedTenantId"
            class="tenant-options"
          >
            <el-radio
              v-for="tenant in availableTenants"
              :key="tenant.id"
              :label="tenant.id"
              border
              class="tenant-option"
            >
              <div class="tenant-info">
                <div class="tenant-name">{{ tenant.name }}</div>
                <div v-if="tenant.description" class="tenant-desc">
                  {{ tenant.description }}
                </div>
                <div class="tenant-code">代码: {{ tenant.code }}</div>
              </div>
            </el-radio>
          </el-radio-group>
        </div>
        <div class="step-actions">
          <el-button @click="currentStep--">上一步</el-button>
          <el-button
            type="primary"
            :disabled="!selectedTenantId"
            :loading="loading"
            @click="handleLogin"
          >
            登录
          </el-button>
        </div>
      </div>

      <!-- Step 3: 登录成功 -->
      <div v-show="currentStep === 2" class="step-content step-success">
        <el-result icon="success" title="登录成功" sub-title="正在跳转..." />
      </div>

      <!-- 账号登录（暂时隐藏） -->
      <div class="account-login-link" style="display: none">
        <el-divider>或</el-divider>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from "vue";
import { useRouter } from "vue-router";
import { ElMessage } from "element-plus";
import type { FormInstance, FormRules } from "element-plus";
import { useUserStore } from "../stores/user";
import {
  loginByPhone,
  sendSmsCode,
  verifyCodeAndGetTenants,
} from "../api/user";

interface Tenant {
  id: string;
  name: string;
  code: string;
  description?: string;
}

const router = useRouter();
const userStore = useUserStore();

const currentStep = ref(0);
const loading = ref(false);
const sending = ref(false);
const countdown = ref(0);

const phoneFormRef = ref<FormInstance>();

const phoneForm = reactive({
  phone: "",
  code: "",
});

const availableTenants = ref<Tenant[]>([]);
const selectedTenantId = ref<string | null>(null);

const phoneRules: FormRules = {
  phone: [
    { required: true, message: "请输入手机号", trigger: "blur" },
    { pattern: /^1[3-9]\d{9}$/, message: "手机号格式不正确", trigger: "blur" },
  ],
  code: [{ required: true, message: "请输入验证码", trigger: "blur" }],
};

async function handleSendCode() {
  if (!phoneFormRef.value) return;

  await phoneFormRef.value.validateField("phone", async (valid) => {
    if (valid === "") {
      sending.value = true;
      try {
        await sendSmsCode(phoneForm.phone);
        ElMessage.success("验证码已发送");

        countdown.value = 60;
        const timer = setInterval(() => {
          countdown.value--;
          if (countdown.value <= 0) {
            clearInterval(timer);
          }
        }, 1000);
      } catch (error) {
        console.error("Send code failed:", error);
      } finally {
        sending.value = false;
      }
    }
  });
}

async function handleVerifyCode() {
  if (!phoneFormRef.value) return;

  await phoneFormRef.value.validate(async (valid) => {
    if (valid) {
      loading.value = true;
      try {
        const { tenants } = await verifyCodeAndGetTenants(
          phoneForm.phone,
          phoneForm.code,
        );
        availableTenants.value = tenants;

        if (tenants.length === 0) {
          ElMessage.warning("暂无可用租户，请联系管理员");
          return;
        }

        // 如果只有一个租户，自动选择
        if (tenants.length === 1) {
          selectedTenantId.value = tenants[0].id;
          await handleLogin();
        } else {
          currentStep.value = 1;
        }
      } catch (error: any) {
        ElMessage.error(error.response?.data?.message || "验证失败");
        console.error("Verify code failed:", error);
      } finally {
        loading.value = false;
      }
    }
  });
}

async function handleLogin() {
  if (!selectedTenantId.value) {
    ElMessage.warning("请选择租户");
    return;
  }

  loading.value = true;
  try {
    const response = await loginByPhone({
      phone: phoneForm.phone,
      code: phoneForm.code,
      tenantId: selectedTenantId.value,
    });

    userStore.setToken(response.token, response.refreshToken);

    const userInfo = {
      id: response.user.id,
      username: response.user.phone || response.user.username || "",
      nickname: response.user.nickname,
      phone: response.user.phoneNumber,
      avatar: response.user.avatar,
      tenantId: selectedTenantId.value,
      createdAt: response.user.dateCreated,
      lastSignDate: response.user.lastSignDate,
    };
    userStore.setUserInfo(userInfo);
    userStore.setTenantId(selectedTenantId.value);
    currentStep.value = 2;

    setTimeout(() => {
      router.push("/chat");
    }, 500);
  } catch (error: any) {
    ElMessage.error(error.response?.data?.message || "登录失败");
    console.error("Login failed:", error);
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped lang="scss">
.login-container {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}

.login-box {
  width: 90%;
  max-width: 500px;
  padding: 40px;
  background: white;
  border-radius: 16px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
}

.login-header {
  text-align: center;
  margin-bottom: 30px;

  h1 {
    margin: 0;
    font-size: 32px;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
  }

  p {
    margin: 8px 0 0;
    color: #909399;
    font-size: 14px;
  }
}

.login-steps {
  margin-bottom: 30px;
}

.step-content {
  min-height: 200px;
}

.login-form {
  margin-top: 24px;
}

.code-input {
  display: flex;
  gap: 12px;

  .el-input {
    flex: 1;
  }
}

.login-btn {
  width: 100%;
  margin-top: 16px;
}

.tenant-list {
  max-height: 300px;
  overflow-y: auto;
  margin: 24px 0;
}

.tenant-options {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.tenant-option {
  width: 100%;
  padding: 16px;
  border-radius: 8px;
  transition: all 0.2s;

  &:hover {
    border-color: #667eea;
    background: #f8f9ff;
  }

  &.is-checked {
    border-color: #667eea;
    background: #f0f4ff;
  }
}

.tenant-info {
  width: 100%;

  .tenant-name {
    font-size: 16px;
    font-weight: 500;
    color: #303133;
    margin-bottom: 4px;
  }

  .tenant-desc {
    font-size: 13px;
    color: #909399;
    margin-bottom: 4px;
  }

  .tenant-code {
    font-size: 12px;
    color: #909399;
  }
}

.step-actions {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin-top: 24px;
  .el-button {
    flex: 1;
  }
}

.step-success {
  padding: 40px 0;
}

@media (max-width: 768px) {
  .login-box {
    padding: 24px;
  }

  .login-header h1 {
    font-size: 28px;
  }
}
</style>
