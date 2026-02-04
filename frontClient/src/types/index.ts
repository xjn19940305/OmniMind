// User types
export interface UserInfo {
  id: string
  username: string
  nickname?: string
  avatar?: string
  phone?: string
  email?: string
  tenantId?: string
  createdAt?: string
  tenant?: {
    id: string
    name: string
    code: string
  }
}

export interface LoginForm {
  username: string
  password: string
}

export interface PhoneLoginForm {
  phone: string
  code: string
  tenantId: string
}

// Chat types
export interface ChatMessage {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  timestamp: string
  files?: Attachment[]
  metadata?: Record<string, any>
}

export interface ChatSession {
  id: string
  title: string
  messages: ChatMessage[]
  createdAt: string
  updatedAt: string
}

export interface Attachment {
  id: string
  name: string
  type: 'image' | 'pdf' | 'word' | 'excel' | 'ppt' | 'markdown' | 'web' | 'video' | 'audio'
  url: string
  size?: number
  thumbnail?: string
  metadata?: Record<string, any>
  status?: DocumentStatus
}

// Enums
export enum Visibility {
  Private = 1,
  Internal = 2,
  Public = 3
}

export enum KnowledgeBaseMemberRole {
  Admin = 1,
  Editor = 2,
  Viewer = 3
}

export enum ContentType {
  Pdf = 1,
  Docx = 2,
  Pptx = 3,
  Markdown = 4,
  Web = 5,
  Image = 6,
  Audio = 7,
  Video = 8
}

export enum SourceType {
  Upload = 1,
  Url = 2,
  Import = 3
}

export enum DocumentStatus {
  Uploaded = 1,
  Parsing = 2,
  Parsed = 3,
  Indexing = 4,
  Indexed = 5,
  Failed = 6
}

// Knowledge Base types
export interface KnowledgeBase {
  id: string
  name: string
  description?: string
  visibility: Visibility
  ownerUserId?: string
  ownerName?: string
  indexProfileId?: string
  createdAt: string
  updatedAt?: string
  memberCount?: number
}

export interface KnowledgeBaseDetail extends KnowledgeBase {
  memberCount: number
  members?: KnowledgeBaseMember[]
}

export interface KnowledgeBaseMember {
  id: string
  knowledgeBaseId: string
  userId: string
  userName?: string
  role: KnowledgeBaseMemberRole
  createdAt: string
}

// Folder types
export interface Folder {
  id: string
  knowledgeBaseId: string
  parentFolderId?: string
  name: string
  path?: string
  description?: string
  sortOrder: number
  createdByUserId: string
  createdAt: string
  updatedAt?: string
  childFolders?: Folder[]
  documents?: Document[]
}

export interface FolderTreeResponse {
  id: string
  parentFolderId?: string
  name: string
  description?: string
  sortOrder: number
  createdAt: string
  documentCount: number
  children?: FolderTreeResponse[]
}

// Document types
export interface Document {
  id: string
  knowledgeBaseId: string
  folderId?: string
  title: string
  contentType: ContentType
  sourceType: SourceType
  sourceUri?: string
  objectKey: string
  fileHash?: string
  language?: string
  status: DocumentStatus
  error?: string
  duration?: number
  transcription?: string
  sessionId?: string
  chunkCount?: number
  createdByUserId: string
  createdAt: string
  updatedAt?: string
}

// API response types
export interface PaginatedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ErrorResponse {
  message: string
}
