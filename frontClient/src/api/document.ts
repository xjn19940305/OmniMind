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
  objectKey?: string
  fileHash?: string
  language?: string
  content?: string
}) {
  return request<Document>({
    url: '/api/Document',
    method: 'post',
    data
  })
}

/**
 * 创建笔记
 */
export function createNote(data: {
  knowledgeBaseId: string
  folderId?: string
  title: string
  content: string
}) {
  return request<Document>({
    url: '/api/Document',
    method: 'post',
    data: {
      ...data,
      contentType: 'text/markdown',
      sourceType: 1
    }
  })
}

/**
 * 创建网页链接
 */
export function createWebLink(data: {
  knowledgeBaseId: string
  folderId?: string
  title: string
  url: string
}) {
  return request<Document>({
    url: '/api/Document',
    method: 'post',
    data: {
      knowledgeBaseId: data.knowledgeBaseId,
      folderId: data.folderId,
      title: data.title,
      contentType: 'text/html',
      sourceType: 2,
      sourceUri: data.url,
      content: data.url
    }
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
