import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { UserInfo } from '../types'
import { revokeToken } from '../api/user'

export const useUserStore = defineStore('user', () => {
  const token = ref<string>(localStorage.getItem('token') || '')
  const refreshToken = ref<string>(localStorage.getItem('refreshToken') || '')
  const userInfo = ref<UserInfo | null>(null)

  // ✅ 新增：当前选择的租户ID（用于请求头 X-Tenant-Id）
  const tenantId = ref<string>(localStorage.getItem('tenantId') || '')
  const isLoggedIn = computed(() => !!token.value)

  function setToken(newToken: string, newRefreshToken?: string) {
    token.value = newToken
    localStorage.setItem('token', newToken)
    if (newRefreshToken) {
      refreshToken.value = newRefreshToken
      localStorage.setItem('refreshToken', newRefreshToken)
    }
  }

  // ✅ 新增：设置租户（登录成功/切换租户时调用）
  function setTenantId(id: string | number) {
    tenantId.value = String(id)
    localStorage.setItem('tenantId', String(id))
  }

  function setUserInfo(info: UserInfo) {
    userInfo.value = info
    localStorage.setItem('userInfo', JSON.stringify(info))
  }

  // 清除本地状态（用于登录失败或刷新token失败）
  function clearAuth() {
    token.value = ''
    refreshToken.value = ''
    userInfo.value = null
    localStorage.removeItem('token')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('userInfo')
    localStorage.removeItem('tenantId') // ✅ 新增
  }

  // 登出（调用后端撤销token）
  async function logout() {
    try {
      if (refreshToken.value) {
        await revokeToken(refreshToken.value)
      }
    } catch (e) {
      console.error('Revoke token failed:', e)
    } finally {
      clearAuth()
    }
  }

  function loadUserInfo() {
    const saved = localStorage.getItem('userInfo')
    if (saved) {
      try {
        userInfo.value = JSON.parse(saved)
      } catch (e) {
        console.error('Failed to parse user info:', e)
      }
    }
  }

  // Initialize
  loadUserInfo()

  return {
    token,
    refreshToken,
    userInfo,
    isLoggedIn,
    setToken,
    setUserInfo,
    logout,
    clearAuth,
    loadUserInfo,
    tenantId,
    setTenantId
  }
})
