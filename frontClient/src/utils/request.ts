import axios from 'axios'
import type {
  AxiosInstance,
  AxiosRequestConfig,
  AxiosResponse,
  InternalAxiosRequestConfig
} from 'axios'
import { ElMessage } from 'element-plus'
import { refreshTokenApi } from '../api/user'
import { useUserStore } from '../stores/user'

const service: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 60000,
  headers: {
    'Content-Type': 'application/json'
  }
})

let isRefreshing = false
let failedQueue: Array<(token: string | null) => void> = []

interface ExtendedAxiosRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

function processQueue(token: string | null) {
  failedQueue.forEach((cb) => cb(token))
  failedQueue = []
}

service.interceptors.request.use(
  (config) => {
    config.headers = config.headers || {}
    const token = localStorage.getItem('token')
    if (token && !config.headers.Authorization) {
      config.headers.Authorization = `Bearer ${token}`
    }

    return config
  },
  (error) => Promise.reject(error)
)

service.interceptors.response.use(
  (response: AxiosResponse) => response.data,
  async (error) => {
    const originalRequest = error.config as ExtendedAxiosRequestConfig

    if (error.response) {
      const { status, data } = error.response

      switch (status) {
        case 401:
          if (!originalRequest._retry) {
            if (isRefreshing) {
              return new Promise((resolve, reject) => {
                failedQueue.push((token) => {
                  if (token) {
                    originalRequest.headers.Authorization = `Bearer ${token}`
                    resolve(service(originalRequest))
                  } else {
                    reject(error)
                  }
                })
              })
            }

            originalRequest._retry = true
            isRefreshing = true

            try {
              const oldToken = localStorage.getItem('token')
              const oldRefreshToken = localStorage.getItem('refreshToken')
              if (!oldToken || !oldRefreshToken) {
                throw error
              }

              const response = await refreshTokenApi(oldToken, oldRefreshToken)
              localStorage.setItem('token', response.token)
              localStorage.setItem('refreshToken', response.refreshToken)

              const userStore = useUserStore()
              userStore.token = response.token
              userStore.refreshToken = response.refreshToken

              originalRequest.headers.Authorization = `Bearer ${response.token}`
              processQueue(response.token)
              return service(originalRequest)
            } catch (refreshError) {
              processQueue(null)
              const userStore = useUserStore()
              userStore.clearAuth()
              window.location.href = '/login'
              return Promise.reject(refreshError)
            } finally {
              isRefreshing = false
            }
          }

          ElMessage.error('登录已过期，请重新登录')
          useUserStore().clearAuth()
          window.location.href = '/login'
          break
        case 403:
          ElMessage.error(data?.message || '拒绝访问')
          break
        case 404:
          ElMessage.error('请求的资源不存在')
          break
        case 500:
          ElMessage.error('服务器错误')
          break
      }
    } else if (error.request) {
      ElMessage.error('网络错误，请检查网络连接')
    }

    return Promise.reject(error)
  }
)

export function request<T = any>(config: AxiosRequestConfig): Promise<T> {
  return service.request(config) as Promise<T>
}

export default service
