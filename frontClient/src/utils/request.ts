import axios from "axios";
import type {
  AxiosInstance,
  AxiosRequestConfig,
  AxiosResponse,
  InternalAxiosRequestConfig,
} from "axios";
import { ElMessage } from "element-plus";
import { refreshTokenApi } from "../api/user";
import { useUserStore } from "../stores/user";

// Create axios instance
const service: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || "/api",
  timeout: 60000,
  headers: {
    "Content-Type": "application/json",
  },
});

// Token refresh state management
let isRefreshing = false;
let failedQueue: Array<(token: string | null) => void> = [];

interface ExtendedAxiosRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean;
}

// Process the failed queue
function processQueue(token: string | null) {
  failedQueue.forEach((cb) => cb(token));
  failedQueue = [];
}

// Request interceptor
service.interceptors.request.use(
  (config) => {
    config.headers = config.headers || {};
    // 1) 全局注入 TenantId（登录前也要带）
    // 优先用 store（如果可用），其次 localStorage
    const userStore = useUserStore();
    const tenantId =
      (userStore as any)?.tenant?.id ||
      (userStore as any)?.tenantId ||
      localStorage.getItem("tenantId");

    // 如果单个请求已经显式设置了 X-Tenant-Id，就不覆盖
    if (tenantId && !config.headers["X-Tenant-Id"]) {
      config.headers["X-Tenant-Id"] = String(tenantId);
    }

    // 2) 注入 Authorization
    const token = localStorage.getItem("token");
    if (token && !config.headers.Authorization) {
      config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
  },
  (error) => {
    return Promise.reject(error);
  },
);

// Response interceptor
service.interceptors.response.use(
  (response: AxiosResponse) => {
    // HTTP 状态码 2xx 表示成功
    return response.data;
  },
  async (error) => {
    const originalRequest = error.config as ExtendedAxiosRequestConfig;

    if (error.response) {
      const { status, data } = error.response;

      // 根据不同的 HTTP 状态码显示错误信息
      let message = data?.message || error.response.statusText || "请求失败";

      switch (status) {
        case 401:
          // Token 过期，尝试刷新
          if (!originalRequest._retry) {
            // 如果已经在刷新中，将请求加入队列
            if (isRefreshing) {
              return new Promise((resolve, reject) => {
                failedQueue.push((token) => {
                  if (token) {
                    originalRequest.headers.Authorization = `Bearer ${token}`;
                    resolve(service(originalRequest));
                  } else {
                    reject(error);
                  }
                });
              });
            }

            originalRequest._retry = true;
            isRefreshing = true;

            try {
              const oldToken = localStorage.getItem("token");
              const oldRefreshToken = localStorage.getItem("refreshToken");

              if (oldToken && oldRefreshToken) {
                const response = await refreshTokenApi(
                  oldToken,
                  oldRefreshToken,
                );

                // 更新 localStorage 和 user store
                localStorage.setItem("token", response.token);
                localStorage.setItem("refreshToken", response.refreshToken);

                // 同步更新 user store
                const userStore = useUserStore();
                userStore.token = response.token;
                userStore.refreshToken = response.refreshToken;

                // 更新请求头
                originalRequest.headers.Authorization = `Bearer ${response.token}`;

                // 处理队列中的请求
                processQueue(response.token);

                // 重试当前请求
                return service(originalRequest);
              }
            } catch (refreshError) {
              // 刷新失败，清除所有信息并跳转登录
              processQueue(null);
              const userStore = useUserStore();
              userStore.clearAuth();
              window.location.href = "/login";
              return Promise.reject(refreshError);
            } finally {
              isRefreshing = false;
            }
          } else {
            // 已经重试过一次，仍然失败，跳转登录
            ElMessage.error("登录已过期，请重新登录");
            const userStore = useUserStore();
            userStore.clearAuth();
            window.location.href = "/login";
          }
          break;
        case 403:
          ElMessage.error("拒绝访问");
          break;
        case 404:
          ElMessage.error("请求的资源不存在");
          break;
        case 500:
          ElMessage.error("服务器错误");
          break;
        // 400 等其他错误由组件自己处理，不在这里显示
        default:
          // 不显示通用错误，让组件处理具体业务错误
          break;
      }
    } else if (error.request) {
      ElMessage.error("网络错误，请检查网络连接");
    }

    return Promise.reject(error);
  },
);

// Generic request function - interceptor already returns data
export function request<T = any>(config: AxiosRequestConfig): Promise<T> {
  return service.request(config) as Promise<T>;
}

export default service;
