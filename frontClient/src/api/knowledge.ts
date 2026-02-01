import request from '../utils/request'
import type {
  KnowledgeBase,
  PaginatedResponse,
  KnowledgeBaseWorkspaceLink,
  Visibility
} from '../types'

/**
 * 获取知识库列表
 */
export function getKnowledgeBases(params?: {
  page?: number
  pageSize?: number
  keyword?: string
  visibility?: number
}) {
  return request<PaginatedResponse<KnowledgeBase>>({
    url: '/api/KnowledgeBase',
    method: 'get',
    params
  })
}

/**
 * 获取知识库详情
 */
export function getKnowledgeBase(id: string) {
  return request<KnowledgeBase>({
    url: `/api/KnowledgeBase/${id}`,
    method: 'get'
  })
}

/**
 * 创建知识库
 */
export function createKnowledgeBase(data: {
  name: string
  description?: string
  visibility?: Visibility
  indexProfileId?: number
}) {
  return request<KnowledgeBase>({
    url: '/api/KnowledgeBase',
    method: 'post',
    data
  })
}

/**
 * 更新知识库
 */
export function updateKnowledgeBase(
  id: string,
  data: {
    name?: string
    description?: string
    visibility?: Visibility
    indexProfileId?: number
  }
) {
  return request<KnowledgeBase>({
    url: `/api/KnowledgeBase/${id}`,
    method: 'put',
    data
  })
}

/**
 * 删除知识库
 */
export function deleteKnowledgeBase(id: string) {
  return request({
    url: `/api/KnowledgeBase/${id}`,
    method: 'delete'
  })
}

/**
 * 挂载知识库到工作空间
 */
export function mountKnowledgeBase(
  knowledgeBaseId: string,
  data: {
    workspaceId: string
    aliasName?: string
    sortOrder?: number
  }
) {
  return request<KnowledgeBaseWorkspaceLink>({
    url: `/api/KnowledgeBase/${knowledgeBaseId}/workspaces`,
    method: 'post',
    data
  })
}

/**
 * 从工作空间卸载知识库
 */
export function unmountKnowledgeBase(knowledgeBaseId: string, workspaceId: string) {
  return request({
    url: `/api/KnowledgeBase/${knowledgeBaseId}/workspaces/${workspaceId}`,
    method: 'delete'
  })
}
