import request from '../utils/request'
import type { LoginForm, UserInfo } from '../types'

export function loginByUsername(data: LoginForm) {
  return request<{
    token: string
    refreshToken: string
    expiresIn: number
    user: UserInfo
  }>({
    url: '/api/Auth/signIn',
    method: 'post',
    data
  })
}

export function getUserInfo() {
  return request<any>({
    url: '/api/User/profile',
    method: 'get'
  }).then((response) => ({
    id: response.userId,
    username: response.userName || response.nickName,
    nickname: response.nickName,
    avatar: response.picture,
    email: response.email,
    createdAt: response.completedAt
  } as UserInfo))
}

export function updateUserInfo(data: Partial<UserInfo>) {
  return request<any>({
    url: '/api/User/profile/complete',
    method: 'post',
    data: {
      bio: data.nickname
    }
  }).then((response) => ({
    id: response.userId,
    username: response.userName || response.nickName,
    nickname: response.nickName,
    avatar: response.picture,
    createdAt: response.completedAt
  } as UserInfo))
}

export async function updateAvatar(_file: File) {
  throw new Error('Avatar upload is not available in this build')
}

export async function changePassword(_oldPassword: string, _newPassword: string) {
  throw new Error('Password change is not available in this build')
}

export function refreshTokenApi(token: string, refreshToken: string) {
  return request<{
    token: string
    refreshToken: string
    expiresIn: number
  }>({
    url: '/api/Auth/refreshToken',
    method: 'post',
    data: { token, refreshToken }
  })
}

export function revokeToken(refreshToken: string) {
  return request({
    url: '/api/Auth/revokeToken',
    method: 'post',
    data: { refreshToken }
  })
}
