import request from '../utils/request'
import type { Folder, FolderTreeResponse } from '../types'

/**
 * 获取文件夹树
 */
export function getFolderTree(knowledgeBaseId: string) {
  return request<FolderTreeResponse[]>({
    url: `/api/Folder/tree/${knowledgeBaseId}`,
    method: 'get'
  })
}

/**
 * 获取文件夹列表
 */
export function getFolderList(knowledgeBaseId: string, parentFolderId?: string) {
  return request<Folder[]>({
    url: `/api/Folder/list/${knowledgeBaseId}`,
    method: 'get',
    params: parentFolderId !== undefined ? { parentFolderId } : undefined
  })
}

/**
 * 获取文件夹详情
 */
export function getFolder(id: string) {
  return request<Folder>({
    url: `/api/Folder/${id}`,
    method: 'get'
  })
}

/**
 * 创建文件夹
 */
export function createFolder(data: {
  knowledgeBaseId: string
  parentFolderId?: string
  name: string
  description?: string
  sortOrder?: number
}) {
  return request<Folder>({
    url: '/api/Folder',
    method: 'post',
    data
  })
}

/**
 * 更新文件夹
 */
export function updateFolder(
  id: string,
  data: {
    name?: string
    description?: string
    sortOrder?: number
  }
) {
  return request<Folder>({
    url: `/api/Folder/${id}`,
    method: 'put',
    data
  })
}

/**
 * 移动文件夹
 */
export function moveFolder(
  id: string,
  data: {
    parentFolderId?: string
    sortOrder?: number
  }
) {
  return request<Folder>({
    url: `/api/Folder/${id}/move`,
    method: 'patch',
    data
  })
}

/**
 * 删除文件夹
 */
export function deleteFolder(id: string) {
  return request({
    url: `/api/Folder/${id}`,
    method: 'delete'
  })
}
