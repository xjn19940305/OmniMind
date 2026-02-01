<template>
  <div class="layout-container">
    <el-container>
      <!-- Mobile Header -->
      <el-header class="mobile-header mobile-only">
        <div class="header-left">
          <el-button text @click="mobileDrawerVisible = true">
            <el-icon size="24"><Menu /></el-icon>
          </el-button>
          <span class="logo">OmniMind</span>
        </div>
        <el-dropdown @command="handleCommand">
          <div class="user-avatar-wrapper">
            <el-avatar :src="userInfo?.avatar" :size="36">
              <el-icon><User /></el-icon>
            </el-avatar>
          </div>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item command="profile">
                <el-icon><User /></el-icon> 个人中心
              </el-dropdown-item>
              <el-dropdown-item command="logout" divided>
                <el-icon><SwitchButton /></el-icon> 退出登录
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>

      <el-container>
        <!-- Desktop Sidebar -->
        <el-aside class="desktop-sidebar desktop-only" :width="collapsed ? '64px' : '240px'">
          <div class="sidebar-header">
            <template v-if="!collapsed">
              <span class="logo">OmniMind</span>
            </template>
            <el-button text @click="collapsed = !collapsed">
              <el-icon :size="20">
                <Fold v-if="!collapsed" />
                <Expand v-else />
              </el-icon>
            </el-button>
          </div>

          <el-menu
            :default-active="activeMenu"
            :collapse="collapsed"
            router
            class="sidebar-menu"
          >
            <el-menu-item index="/chat">
              <el-icon><ChatDotRound /></el-icon>
              <template #title>AI 对话</template>
            </el-menu-item>
            <el-menu-item index="/knowledge">
              <el-icon><Collection /></el-icon>
              <template #title>知识库</template>
            </el-menu-item>
            <el-menu-item index="/workspace">
              <el-icon><OfficeBuilding /></el-icon>
              <template #title>工作空间</template>
            </el-menu-item>
          </el-menu>
        </el-aside>

        <!-- Mobile Drawer -->
        <el-drawer
          v-model="mobileDrawerVisible"
          direction="ltr"
          size="70%"
          class="mobile-drawer mobile-only"
        >
          <template #header>
            <span class="logo">OmniMind</span>
          </template>
          <el-menu
            :default-active="activeMenu"
            router
            @select="mobileDrawerVisible = false"
          >
            <el-menu-item index="/chat">
              <el-icon><ChatDotRound /></el-icon>
              <template #title>AI 对话</template>
            </el-menu-item>
            <el-menu-item index="/knowledge">
              <el-icon><Collection /></el-icon>
              <template #title>知识库</template>
            </el-menu-item>
            <el-menu-item index="/workspace">
              <el-icon><OfficeBuilding /></el-icon>
              <template #title>工作空间</template>
            </el-menu-item>
            <el-menu-item index="/profile">
              <el-icon><User /></el-icon>
              <template #title>个人中心</template>
            </el-menu-item>
            <el-menu-item @click="handleLogout">
              <el-icon><SwitchButton /></el-icon>
              <template #title>退出登录</template>
            </el-menu-item>
          </el-menu>
        </el-drawer>

        <!-- Main Content -->
        <el-main class="main-content">
          <router-view />
        </el-main>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessageBox, ElMessage } from 'element-plus'
import {
  Menu,
  User,
  SwitchButton,
  Fold,
  Expand,
  ChatDotRound,
  Collection
} from '@element-plus/icons-vue'
import { useUserStore } from '../stores/user'

const router = useRouter()
const route = useRoute()
const userStore = useUserStore()

const collapsed = ref(false)
const mobileDrawerVisible = ref(false)

const userInfo = computed(() => userStore.userInfo)
const activeMenu = computed(() => route.path)

function handleCommand(command: string) {
  if (command === 'profile') {
    router.push('/profile')
  } else if (command === 'logout') {
    handleLogout()
  }
}

async function handleLogout() {
  try {
    await ElMessageBox.confirm('确定要退出登录吗？', '提示', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await userStore.logout()
    ElMessage.success('已退出登录')
    router.push('/login')
  } catch (e) {
    // User cancelled or error occurred
    if (e !== 'cancel') {
      console.error('Logout error:', e)
    }
  }
}
</script>

<style scoped lang="scss">
.layout-container {
  height: 100vh;
}

.el-container {
  height: 100%;
}

// Mobile Header
.mobile-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 16px;
  background: white;
  border-bottom: 1px solid #e4e7ed;

  .header-left {
    display: flex;
    align-items: center;
    gap: 12px;

    .logo {
      font-size: 20px;
      font-weight: bold;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
  }

  .user-avatar-wrapper {
    cursor: pointer;
  }
}

// Desktop Sidebar
.desktop-sidebar {
  background: white;
  border-right: 1px solid #e4e7ed;
  transition: width 0.3s;

  .sidebar-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    height: 60px;
    padding: 0 20px;
    border-bottom: 1px solid #e4e7ed;

    .logo {
      font-size: 22px;
      font-weight: bold;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
  }

  .sidebar-menu {
    border: none;
  }
}

// Main Content
.main-content {
  padding: 20px;
  background: #f5f7fa;
  overflow-y: auto;
}

@media (max-width: 768px) {
  .main-content {
    padding: 16px;
  }
}
</style>
