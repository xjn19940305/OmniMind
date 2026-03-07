import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import type { UserInfo } from '../types'
import { revokeToken } from '../api/user'

export const useUserStore = defineStore('user', () => {
  const token = ref<string>(localStorage.getItem('token') || '')
  const refreshToken = ref<string>(localStorage.getItem('refreshToken') || '')
  const userInfo = ref<UserInfo | null>(null)
  const isLoggedIn = computed(() => !!token.value)

  function setToken(newToken: string, newRefreshToken?: string) {
    token.value = newToken
    localStorage.setItem('token', newToken)

    if (newRefreshToken) {
      refreshToken.value = newRefreshToken
      localStorage.setItem('refreshToken', newRefreshToken)
    }
  }

  function setUserInfo(info: UserInfo) {
    userInfo.value = info
    localStorage.setItem('userInfo', JSON.stringify(info))
  }

  function clearAuth() {
    token.value = ''
    refreshToken.value = ''
    userInfo.value = null
    localStorage.removeItem('token')
    localStorage.removeItem('refreshToken')
    localStorage.removeItem('userInfo')
  }

  async function logout() {
    try {
      if (refreshToken.value) {
        await revokeToken(refreshToken.value)
      }
    } catch (error) {
      console.error('Revoke token failed:', error)
    } finally {
      clearAuth()
    }
  }

  function loadUserInfo() {
    const saved = localStorage.getItem('userInfo')
    if (!saved) {
      return
    }

    try {
      userInfo.value = JSON.parse(saved)
    } catch (error) {
      console.error('Failed to parse user info:', error)
    }
  }

  loadUserInfo()

  return {
    token,
    refreshToken,
    userInfo,
    isLoggedIn,
    setToken,
    setUserInfo,
    clearAuth,
    logout,
    loadUserInfo
  }
})
