<template>
  <div class="layout-container">
    <el-container>
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
        <el-aside
          class="desktop-sidebar desktop-only"
          :width="collapsed ? '72px' : '248px'"
        >
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
            <el-menu-item index="/profile">
              <el-icon><User /></el-icon>
              <template #title>个人中心</template>
            </el-menu-item>
          </el-menu>
        </el-aside>

        <el-drawer
          v-model="mobileDrawerVisible"
          direction="ltr"
          :size="drawerSize"
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

        <el-main
          class="main-content"
          :class="{ 'main-content-tablet': isTabletOrBelow }"
        >
          <router-view />
        </el-main>
      </el-container>
    </el-container>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { ElMessage, ElMessageBox } from "element-plus";
import {
  ChatDotRound,
  Collection,
  Expand,
  Fold,
  Menu,
  SwitchButton,
  User,
} from "@element-plus/icons-vue";
import { useViewport } from "../composables/useViewport";
import { useUserStore } from "../stores/user";

const router = useRouter();
const route = useRoute();
const userStore = useUserStore();
const { mode, isTabletOrBelow } = useViewport();

const collapsed = ref(false);
const mobileDrawerVisible = ref(false);

const userInfo = computed(() => userStore.userInfo);
const activeMenu = computed(() => route.path);
const drawerSize = computed(() => (mode.value === "mobile" ? "86%" : "70%"));

function handleCommand(command: string) {
  if (command === "profile") {
    router.push("/profile");
    return;
  }

  if (command === "logout") {
    handleLogout();
  }
}

async function handleLogout() {
  try {
    await ElMessageBox.confirm("确定要退出登录吗？", "提示", {
      confirmButtonText: "确定",
      cancelButtonText: "取消",
      type: "warning",
    });
    await userStore.logout();
    ElMessage.success("已退出登录");
    router.push("/login");
  } catch (error) {
    if (error !== "cancel") {
      console.error("Logout error:", error);
    }
  }
}
</script>

<style scoped lang="scss">
@use "../styles/index.scss" as *;

.layout-container {
  min-height: 100vh;
  width: 100%;
  max-width: 100%;
  overflow-x: hidden;
  background: transparent;
}

.el-container {
  min-height: 100vh;
  min-width: 0;
  width: 100%;
  max-width: 100%;
}

.mobile-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 64px;
  padding: 0 var(--page-gutter-mobile);
  background: rgba(255, 255, 255, 0.84);
  border-bottom: 1px solid var(--app-border);
  backdrop-filter: blur(18px);

  .header-left {
    display: flex;
    align-items: center;
    gap: 12px;

    .logo {
      font-size: 20px;
      font-weight: 700;
      letter-spacing: 0.04em;
      background: linear-gradient(135deg, #1d4ed8 0%, #3b82f6 55%, #38bdf8 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
  }

  .user-avatar-wrapper {
    cursor: pointer;
    padding: 4px;
    border-radius: 999px;
    transition: background 0.2s ease;

    &:hover {
      background: rgba(37, 99, 235, 0.08);
    }
  }
}

.desktop-sidebar {
  margin: 14px 0 14px 14px;
  background: rgba(255, 255, 255, 0.78);
  border: 1px solid var(--app-border);
  border-radius: 24px;
  box-shadow: var(--app-shadow-sm);
  backdrop-filter: blur(16px);
  overflow: hidden;
  transition: width 0.3s;

  .sidebar-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    height: 68px;
    padding: 0 20px;
    border-bottom: 1px solid var(--app-border);

    .logo {
      font-size: 22px;
      font-weight: 700;
      letter-spacing: 0.04em;
      background: linear-gradient(135deg, #1d4ed8 0%, #3b82f6 55%, #38bdf8 100%);
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
    }
  }

  .sidebar-menu {
    border: none;
    background: transparent;

    :deep(.el-menu-item) {
      margin: 6px 12px;
      border-radius: 14px;
      color: var(--app-text-secondary);

      &.is-active {
        background: linear-gradient(
          135deg,
          rgba(37, 99, 235, 0.14),
          rgba(56, 189, 248, 0.12)
        );
        color: var(--app-primary-strong);
      }
    }
  }
}

.main-content {
  min-width: 0;
  width: 100%;
  max-width: 100%;
  min-height: 100vh;
  padding: var(--page-gutter);
  background: transparent;
  overflow-x: hidden;
  overflow-y: auto;
}

.main-content-tablet {
  padding: 18px var(--page-gutter-mobile) calc(18px + var(--safe-bottom));
}

@media (max-width: 768px) {
  .main-content {
    min-height: calc(100vh - 64px);
    padding: 14px var(--page-gutter-mobile) calc(18px + var(--safe-bottom));
  }

  .mobile-header {
    position: sticky;
    top: 0;
    z-index: 20;
  }

  .mobile-header :deep(.el-button) {
    min-width: 44px;
    min-height: 44px;
    border-radius: 12px;
  }

  .mobile-drawer {
    :deep(.el-drawer) {
      border-radius: 0 24px 24px 0;
      overflow: hidden;
    }

    :deep(.el-drawer__header) {
      margin-bottom: 0;
      padding: 20px 20px 12px;
      border-bottom: 1px solid var(--app-border);
    }

    :deep(.el-drawer__body) {
      padding: 12px;
      background: linear-gradient(
        180deg,
        rgba(255, 255, 255, 0.96),
        rgba(244, 248, 255, 0.98)
      );
    }

    :deep(.el-menu) {
      border-right: none;
      background: transparent;
    }

    :deep(.el-menu-item) {
      height: 48px;
      line-height: 48px;
      margin-bottom: 6px;
      border-radius: 14px;
    }
  }
}
</style>
