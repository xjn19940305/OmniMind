import request from '../utils/request'
import type { Invitation, InvitationCreateRequest, PaginatedResponse } from '../types'

/**
 * 创建邀请
 */
export function createInvitation(data: InvitationCreateRequest) {
  return request<Invitation>({
    url: '/api/Invitation',
    method: 'post',
    data
  })
}

/**
 * 获取知识库邀请列表
 */
export function getInvitations(knowledgeBaseId: string, params?: {
  page?: number
  pageSize?: number
  status?: number
}) {
  return request<PaginatedResponse<Invitation>>({
    url: `/api/Invitation/knowledge-base/${knowledgeBaseId}`,
    method: 'get',
    params
  })
}

/**
 * 获取邀请详情（通过邀请码）
 */
export function getInvitation(code: string) {
  return request<{
    invitation: Invitation
    inviterName: string
    isCurrentUserInvited: boolean
  }>({
    url: `/api/Invitation/code/${code}`,
    method: 'get'
  })
}

/**
 * 响应邀请（接受/拒绝）
 */
export function respondToInvitation(data: {
  code: string
  accept: boolean
  applicationReason?: string
}) {
  return request({
    url: '/api/Invitation/respond',
    method: 'post',
    data
  })
}

/**
 * 审核邀请
 */
export function approveInvitation(invitationId: string, approved: boolean) {
  return request({
    url: `/api/Invitation/${invitationId}/approve`,
    method: 'post',
    data: { approved }
  })
}

/**
 * 取消邀请
 */
export function cancelInvitation(invitationId: string) {
  return request({
    url: `/api/Invitation/${invitationId}`,
    method: 'delete'
  })
}
