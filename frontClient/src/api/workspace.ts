import request from '../utils/request'
import type {
  Workspace,
  WorkspaceDetail,
  WorkspaceMember,
  PaginatedResponse,
  WorkspaceType,
  WorkspaceRole
} from '../types'

/**
 * 获取工作空间列表
 */
export function getWorkspaces(params?: {
  page?: number
  pageSize?: number
  keyword?: string
  type?: number
}) {
  return request<PaginatedResponse<Workspace>>({
    url: '/api/Workspace',
    method: 'get',
    params
  })
}

/**
 * 获取工作空间详情
 */
export function getWorkspace(id: string) {
  return request<WorkspaceDetail>({
    url: `/api/Workspace/${id}`,
    method: 'get'
  })
}

/**
 * 创建工作空间
 */
export function createWorkspace(data: {
  name: string
  type?: WorkspaceType
}) {
  return request<Workspace>({
    url: '/api/Workspace',
    method: 'post',
    data
  })
}

/**
 * 更新工作空间
 */
export function updateWorkspace(
  id: string,
  data: {
    name?: string
    type?: WorkspaceType
  }
) {
  return request<Workspace>({
    url: `/api/Workspace/${id}`,
    method: 'put',
    data
  })
}

/**
 * 删除工作空间
 */
export function deleteWorkspace(id: string) {
  return request({
    url: `/api/Workspace/${id}`,
    method: 'delete'
  })
}

/**
 * 获取工作空间成员列表
 */
export function getWorkspaceMembers(workspaceId: string) {
  return request<WorkspaceMember[]>({
    url: `/api/Workspace/${workspaceId}/members`,
    method: 'get'
  })
}

/**
 * 添加工作空间成员
 */
export function addWorkspaceMember(workspaceId: string, data: {
  userId: string
  role?: WorkspaceRole
}) {
  return request<WorkspaceMember>({
    url: `/api/Workspace/${workspaceId}/members`,
    method: 'post',
    data
  })
}

/**
 * 更新工作空间成员角色
 */
export function updateWorkspaceMember(
  workspaceId: string,
  userId: string,
  data: {
    role: WorkspaceRole
  }
) {
  return request<WorkspaceMember>({
    url: `/api/Workspace/${workspaceId}/members/${userId}`,
    method: 'put',
    data
  })
}

/**
 * 移除工作空间成员
 */
export function removeWorkspaceMember(workspaceId: string, userId: string) {
  return request({
    url: `/api/Workspace/${workspaceId}/members/${userId}`,
    method: 'delete'
  })
}
