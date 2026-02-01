import request from '../utils/request'
import type { Document, PaginatedResponse } from '../types'

/**
 * 获取文档列表
 */
export function getDocuments(params?: {
  knowledgeBaseId?: string
  folderId?: string
  page?: number
  pageSize?: number
  keyword?: string
  status?: number
}) {
  return request<PaginatedResponse<Document>>({
    url: '/api/Document',
    method: 'get',
    params
  })
}

/**
 * 获取文档详情
 */
export function getDocument(id: string) {
  return request<Document>({
    url: `/api/Document/${id}`,
    method: 'get'
  })
}

/**
 * 创建文档
 */
export function createDocument(data: {
  knowledgeBaseId: string
  folderId?: string
  title: string
  contentType: number
  sourceType: number
  sourceUri?: string
  objectKey: string
  fileHash?: string
  language?: string
}) {
  return request<Document>({
    url: '/api/Document',
    method: 'post',
    data
  })
}

/**
 * 移动文档
 */
export function moveDocument(id: string, data: {
  folderId?: string
}) {
  return request<Document>({
    url: `/api/Document/${id}/move`,
    method: 'patch',
    data
  })
}

/**
 * 删除文档
 */
export function deleteDocument(id: string) {
  return request({
    url: `/api/Document/${id}`,
    method: 'delete'
  })
}
