import request from '../utils/request'
import type {
  KnowledgeBase,
  KnowledgeBaseDetail,
  KnowledgeBaseMember,
  PaginatedResponse,
  Visibility,
  KnowledgeBaseMemberRole
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
  return request<KnowledgeBaseDetail>({
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

// ==================== 成员管理 API ====================

/**
 * 获取知识库成员列表
 */
export function getKnowledgeBaseMembers(knowledgeBaseId: string) {
  return request<KnowledgeBaseMember[]>({
    url: `/api/KnowledgeBase/${knowledgeBaseId}/members`,
    method: 'get'
  })
}

/**
 * 添加知识库成员
 */
export function addKnowledgeBaseMember(
  knowledgeBaseId: string,
  data: {
    userId: string
    role: KnowledgeBaseMemberRole
  }
) {
  return request<KnowledgeBaseMember>({
    url: `/api/KnowledgeBase/${knowledgeBaseId}/members`,
    method: 'post',
    data
  })
}

/**
 * 更新知识库成员角色
 */
export function updateKnowledgeBaseMember(
  knowledgeBaseId: string,
  userId: string,
  data: {
    role: KnowledgeBaseMemberRole
  }
) {
  return request<KnowledgeBaseMember>({
    url: `/api/KnowledgeBase/${knowledgeBaseId}/members/${userId}`,
    method: 'put',
    data
  })
}

/**
 * 移除知识库成员
 */
export function removeKnowledgeBaseMember(knowledgeBaseId: string, userId: string) {
  return request({
    url: `/api/KnowledgeBase/${knowledgeBaseId}/members/${userId}`,
    method: 'delete'
  })
}
