# OmniMind

企业级多模态知识库平台。

## 产品定位

OmniMind 当前版本聚焦一个清晰边界：

- 知识库管理
- 成员协作与邀请
- 文件夹与文档管理
- 多模态内容摄取
- RAG 问答
- 文档处理进度实时通知

这不是一个“企业智能操作系统”，也不是已经完成的 Agent 编排平台。当前仓库的目标是把知识库产品做成一个可交付、可维护、接口一致的企业级 v1。

## 当前能力

### 1. 知识库协作

- 支持创建知识库
- 支持 `Private / Internal / Public` 可见性
- 支持成员和邀请管理
- 权限按知识库级别控制

### 2. 多模态摄取

当前版本的主链路分两层：

- 已完成并可直接进入解析与索引：`PDF`、`DOCX`、`PPTX`、`Markdown`、`TXT`
- 已接入统一文档生命周期，但依赖外部能力补全识别内容：
  - 图片：需要 OCR 服务回流
  - 音频：需要 ASR 转写服务回流
  - 视频：当前按“抽音频 -> 转写”路线处理
- 网页链接：可作为文档入口创建，但抓取与正文提取仍需继续补完

### 3. RAG 问答

- 基于知识库文档检索增强回答
- 支持临时文件问答
- 支持会话历史与流式输出

### 4. 实时处理进度

- 文档上传后可通过 SignalR 接收处理进度
- 聊天流式消息也通过 SignalR 推送

## 这轮重构明确不做

以下能力不再作为运行时主链路维护：

- 多租户
- 短信验证码登录
- 租户选择页和 `X-Tenant-Id` 请求头
- Push Device / 设备推送链路
- 对外暴露的 Test API

数据库中旧表、旧字段和旧 migration 可以保留，但不再作为产品能力继续扩展。

## 架构概览

### 前端

- `frontClient`
- Vue 3 + Vite + Element Plus

### API

- `OmniMind.Api`
- 提供认证、知识库、文档、文件夹、邀请、聊天等接口

### 摄取处理

- RabbitMQ 负责异步文档处理调度
- `DocumentProcessor` 负责解析、切片、向量化编排
- 音视频由外部转写服务处理后回流

### 存储

- MinIO：原始文件对象存储
- Qdrant：向量检索
- PostgreSQL：业务数据、会话、文档元数据

### 转写服务

- `转写程序/`
- 当前按外部 ASR 服务集成，不在本轮主站重构范围内

## 接口约定

当前主链路统一约定：

- 登录响应：`token / refreshToken / expiresIn / user`
- 分页响应：`items / totalCount / page / pageSize`
- 文档 `contentType`：统一为 MIME 字符串
- SignalR：只依赖 JWT 身份，不再接受前端自传 `userId`

## 当前限制

以下能力仍然属于后续增强项，不应在对外文档中宣称“已完成”：

- 图片 OCR 的正式识别实现
- 音频与视频转写链路的正式生产化编排
- 视频关键帧视觉理解
- 网页抓取与正文抽取增强
- 混合检索（BM25 + Vector）
- Rerank
- Knowledge Graph
- Agent / Orchestrator
- 长期记忆

## 本地开发

### 后端依赖

- PostgreSQL
- Redis
- RabbitMQ
- MinIO
- Qdrant

### 运行说明

1. 配置 `DB_CONNECTION`、Redis、RabbitMQ、MinIO、Qdrant 等环境变量或配置文件
2. 启动 `OmniMind.Api`
3. 启动 `frontClient`
4. 如需音视频转写，单独部署并启动 `转写程序/`

## 目录说明

```text
OmniMind.Api/                     Web API
OmniMind.Application/             应用服务与摄取逻辑
OmniMind.Infrastructure/          存储、向量库、消息、实时通信
OmniMind.Domain/                  领域实体
OmniMind.Shared/                  Contracts / Enums / Abstractions
frontClient/                      前端
转写程序/                          外部转写服务
```

## 路线图

后续如果继续扩展，优先顺序应是：

1. 图片 OCR 正式化
2. 音视频链路稳定化
3. 混合检索与 rerank
4. 更细粒度文档权限
5. Agent 与编排能力
