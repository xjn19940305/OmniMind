export interface UserInfo {
  id: string
  username?: string
  userName?: string
  nickname?: string
  nickName?: string
  avatar?: string
  picture?: string
  phone?: string
  phoneNumber?: string
  email?: string
  createdAt?: string
  dateCreated?: string
  lastSignDate?: string
}

export interface LoginForm {
  username: string
  password: string
}

export interface ChatMessage {
  id?: string
  role: 'user' | 'assistant' | 'system'
  content: string
  timestamp?: string
  files?: Attachment[]
  references?: ChatReference[]
  metadata?: Record<string, any>
  status?: 'pending' | 'streaming' | 'completed' | 'failed'
  error?: string
  completedAt?: string
}

export interface ChatSession {
  id: string
  title: string
  messages: ChatMessage[]
  createdAt: string
  updatedAt: string
}

export interface Conversation {
  id: string
  title: string
  conversationType: 'simple' | 'knowledge_base' | 'document'
  knowledgeBaseId?: string
  documentId?: string
  modelId?: string
  isPinned: boolean
  createdAt: string
  updatedAt?: string
  messageCount?: number
  lastMessage?: string
  lastMessageAt?: string
}

export interface ChatMessageDto {
  id: string
  role: 'user' | 'assistant' | 'system'
  content: string
  status: 'pending' | 'streaming' | 'completed' | 'failed'
  error?: string
  knowledgeBaseId?: string
  documentId?: string
  references?: string
  createdAt: string
  completedAt?: string
}

export interface ChatReference {
  documentId: string
  documentTitle: string
  chunkId: string
  snippet: string
  score?: number
  sourceType: 'knowledge_base' | 'document'
  hitCount: number
  previewUrl: string
}

export interface ConversationDetail extends Conversation {
  messages: ChatMessageDto[]
}

export interface ConversationListResponse {
  conversations: Conversation[]
  totalCount: number
}

export interface Attachment {
  id: string
  name: string
  type: 'image' | 'pdf' | 'word' | 'excel' | 'ppt' | 'markdown' | 'web' | 'video' | 'audio' | 'file'
  url: string
  size?: number
  thumbnail?: string
  metadata?: Record<string, any>
  status?: DocumentStatus
}

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

export enum SourceType {
  Upload = 1,
  Url = 2,
  Import = 3
}

export enum DocumentStatus {
  Pending = 0,
  Uploaded = 1,
  Parsing = 2,
  Parsed = 3,
  Indexing = 4,
  Indexed = 5,
  Failed = 6
}

export enum InvitationStatus {
  Pending = 0,
  Accepted = 1,
  Rejected = 2,
  Expired = 3,
  Canceled = 4
}

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
  documentCount?: number
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
  children: FolderTreeResponse[]
}

export enum FileItemType {
  Folder = 1,
  Document = 2
}

export interface FileItemResponse {
  id: string
  type: FileItemType
  name: string
  description?: string
  contentType?: string
  status?: DocumentStatus
  sourceType?: SourceType
  fileSize?: number
  content?: string
  createdAt: string
  updatedAt?: string
}

export interface Document {
  id: string
  knowledgeBaseId: string
  folderId?: string
  folderName?: string
  title: string
  contentType: string
  sourceType: SourceType
  sourceUri?: string
  objectKey?: string
  fileSize: number
  fileHash?: string
  language?: string
  status: DocumentStatus
  error?: string
  duration?: number
  transcription?: string
  content?: string
  sessionId?: string
  chunkCount?: number
  createdByUserId: string
  createdAt: string
  updatedAt?: string
}

export interface PaginatedResponse<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ErrorResponse {
  message: string
}

export interface Invitation {
  id: string
  knowledgeBaseId: string
  knowledgeBaseName?: string
  code: string
  inviteLink: string
  email?: string
  role: KnowledgeBaseMemberRole
  requireApproval: boolean
  status: InvitationStatus
  expiresAt: string
  createdAt: string
  applicationReason?: string
  inviteeUserId?: string
  inviteeUser?: {
    id: string
    userName?: string
    nickName?: string
    email?: string
  }
}

export interface InvitationCreateRequest {
  knowledgeBaseId: string
  email?: string
  role: KnowledgeBaseMemberRole
  requireApproval: boolean
  expireDays: number
}
