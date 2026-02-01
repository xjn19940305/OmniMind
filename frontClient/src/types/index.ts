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
}

// Enums
export enum Visibility {
  Private = 1,
  Internal = 2,
  Public = 3
}

export enum WorkspaceType {
  Personal = 1,
  Team = 2,
  Shared = 3
}

export enum WorkspaceRole {
  Owner = 1,
  Admin = 2,
  Member = 3,
  Viewer = 4
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
  indexProfileId?: string
  createdAt: string
  updatedAt?: string
  workspaceCount: number
  workspaces?: WorkspaceRef[]
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

export interface WorkspaceRef {
  id: string
  name: string
  aliasName?: string
  sortOrder: number
}

export interface KnowledgeBaseWorkspaceLink {
  id: string
  knowledgeBaseId: string
  knowledgeBaseName: string
  workspaceId: string
  workspaceName: string
  aliasName?: string
  sortOrder: number
  createdAt: string
}

// Workspace types
export interface Workspace {
  id: string
  name: string
  type: WorkspaceType
  ownerUserId: string
  createdAt: string
  updatedAt?: string
}

export interface WorkspaceDetail {
  id: string
  name: string
  type: WorkspaceType
  ownerUserId: string
  createdAt: string
  updatedAt?: string
  knowledgeBaseCount: number
  memberCount: number
  knowledgeBases?: KnowledgeBaseRef[]
  members?: MemberRef[]
}

export interface KnowledgeBaseRef {
  id: string
  name: string
  aliasName?: string
  sortOrder: number
}

export interface MemberRef {
  userId: string
  role: WorkspaceRole
  joinedAt: string
}

export interface WorkspaceMember {
  id: string
  workspaceId: string
  userId: string
  role: WorkspaceRole
  createdAt: string
}

// Document types (placeholder for future implementation)
export interface Document {
  id: string
  knowledgeBaseId: string
  folderId?: string
  workspaceId: string
  title: string
  contentType: ContentType
  sourceType: SourceType
  sourceUri?: string
  objectKey: string
  fileHash?: string
  language?: string
  status: DocumentStatus
  error?: string
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
