import request from '../utils/request'
import type { LoginForm, PhoneLoginForm, UserInfo } from '../types'

// 账号密码登录（如果后端有实现）
export function loginByUsername(data: LoginForm) {
  return request<{
    token: string
    refreshToken: string
    expiresIn: number
    user: UserInfo
  }>({
    url: '/api/auth/signin',
    method: 'post',
    data
  })
}

// 验证验证码并获取租户列表
export function verifyCodeAndGetTenants(phone: string, code: string) {
  return request<{
    tenants: Array<{
      id: number
      name: string
      code: string
      description?: string
    }>
  }>({
    url: '/api/Auth/verifyCode',
    method: 'post',
    data: { phoneNumber: phone, verificationCode: code }
  })
}

// 手机号登录（选择租户后）
export function loginByPhone(data: PhoneLoginForm) {
  return request<{
    token: string
    refreshToken: string
    expiresIn: number
    user: UserInfo
    tenant: {
      id: number
      name: string
      code: string
    }
  }>({
    url: '/api/Auth/phoneSignIn',
    method: 'post',
    headers: {
      'X-Tenant-Id': String(data.tenantId) // ✅ 关键：后端未登录也能解析 tenant
    },
    data: {
      phoneNumber: data.phone,
      verificationCode: data.code,
      tenantId: data.tenantId,
      rememberMe: false
    }
  })
}

// 获取租户列表
export function getTenants() {
  return request<Array<{
    id: number
    name: string
    code: string
    description?: string
    createdAt: string
  }>>({
    url: '/api/Auth/tenants',
    method: 'get'
  })
}

// 发送验证码
export function sendSmsCode(phone: string) {
  return request({
    url: '/api/auth/sendVerificationCode',
    method: 'post',
    data: { phoneNumber: phone }
  })
}

// 获取用户信息
export function getUserInfo() {
  return request<UserInfo>({
    url: '/api/user/info', // 根据实际后端接口调整
    method: 'get'
  })
}

// 更新用户信息
export function updateUserInfo(data: Partial<UserInfo>) {
  return request<UserInfo>({
    url: '/api/user/info',
    method: 'put',
    data
  })
}

// 更新头像
export function updateAvatar(file: File) {
  const formData = new FormData()
  formData.append('file', file)

  return request<{ url: string }>({
    url: '/api/user/avatar',
    method: 'post',
    data: formData,
    headers: {
      'Content-Type': 'multipart/form-data'
    }
  })
}

// 修改密码
export function changePassword(oldPassword: string, newPassword: string) {
  return request({
    url: '/api/user/password',
    method: 'put',
    data: { oldPassword, newPassword }
  })
}

// 刷新Token
export function refreshTokenApi(token: string, refreshToken: string) {
  return request<{
    token: string
    refreshToken: string
    expiresIn: number
  }>({
    url: '/api/auth/refreshToken',
    method: 'post',
    data: { token, refreshToken }
  })
}

// 撤销Token（登出）
export function revokeToken(refreshToken: string) {
  return request({
    url: '/api/auth/revokeToken',
    method: 'post',
    data: { refreshToken }
  })
}
