# 向量化功能配置说明

## 功能概述

本次更新实现了完整的文档向量化功能，使用微软官方的 **Microsoft.Extensions.AI** 抽象接口：

1. **IEmbeddingGenerator<TInput, TEmbedding>** - 微软官方的向量化抽象接口
2. **AlibabaCloudEmbeddingGenerator** - 阿里云 DashScope 向量化实现（生产推荐）
3. **LocalEmbeddingGenerator** - 本地模型向量化实现（占位符，待完善）
4. **DocumentProcessor 集成** - 文档处理流程集成向量化步骤

## 实现细节

### 1. 官方接口 (Microsoft.Extensions.AI)

使用微软官方的 `IEmbeddingGenerator<string, Embedding<float>>` 接口：

```csharp
public interface IEmbeddingGenerator<TInput, TEmbedding> : IDisposable
    where TEmbedding : Embedding
{
    Task<GeneratedEmbeddings<TEmbedding>> GenerateAsync(
        IEnumerable<TInput> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    object? GetService(Type serviceType, object? serviceKey = null);
}
```

**关键类型**:
- `Embedding<float>` - 包含 `ReadOnlyMemory<float> Vector` 属性
- `GeneratedEmbeddings<Embedding<float>>` - 返回的集合类型
- `EmbeddingGeneratorMetadata` - 元数据（模型名称、提供商等）

### 2. 阿里云实现 (AlibabaCloudEmbeddingGenerator)

- **API**: 阿里云 DashScope 文本嵌入 API
- **模型**: 默认使用 `text-embedding-v3` (1024维)
- **批量处理**: 支持最多 25 个文本批量向量化
- **文档**: https://help.aliyun.com/zh/dashscope/developer-reference/text-embedding-api-details

### 3. 本地模型实现 (LocalEmbeddingGenerator)

- 当前为占位符实现，返回零向量
- 可用于未来接入 ONNX Runtime、llama.cpp 等本地模型
- 配置项已预留，包括模型路径、类型、GPU 支持等

### 4. 文档处理流程更新

DocumentProcessor 的新流程:
1. 下载文件
2. 解析文档内容 (IFileParser)
3. 文本切片 (IChunker)
4. **批量向量化** (IEmbeddingGenerator<string, Embedding<float>>) ← 使用官方接口
5. **存储向量到 Qdrant** (IVectorStore)
6. 更新状态为已完成

## 配置说明

### appsettings.json 配置

```json
{
  "AlibabaCloud": {
    "ApiKey": "your-dashscope-api-key",
    "Endpoint": "https://dashscope.aliyuncs.com",
    "Model": "text-embedding-v3",
    "VectorSize": 1024
  }
}
```

### 环境变量配置

```bash
export AlibabaCloud__ApiKey="sk-xxxxxxxxxxxx"
export AlibabaCloud__Model="text-embedding-v3"
```

## 使用方式

### 1. 注册服务 (Program.cs)

已自动注册，代码如下:

```csharp
// Ingestion 服务
builder.Services.AddIngestion();

// 阿里云向量化服务（使用 Microsoft.Extensions.AI 官方接口）
builder.Services.AddAlibabaCloudEmbedding(builder.Configuration);
```

### 2. 切换到本地模型

如需使用本地模型，修改 Program.cs:

```csharp
// 注释掉阿里云服务
// builder.Services.AddAlibabaCloudEmbedding(builder.Configuration);

// 启用本地模型服务
builder.Services.AddLocalEmbedding(builder.Configuration);
```

### 3. 配置本地模型

```json
{
  "LocalEmbedding": {
    "ModelPath": "/path/to/model.onnx",
    "ModelType": "onnx",
    "VectorSize": 768,
    "MaxTokens": 512,
    "UseGpu": false,
    "Threads": 4
  }
}
```

## 支持的阿里云模型

| 模型名称 | 向量维度 | 说明 |
|---------|---------|------|
| text-embedding-v3 | 1024 | 最新版本，推荐使用 |
| text-embedding-v2 | 1536 | 上一版本 |
| text-embedding-v1 | 1536 | 早期版本 |

## 官方接口的优势

使用 Microsoft.Extensions.AI 官方接口的好处：

1. **标准化**: 遵循微软官方标准，与其他 .NET AI 库一致
2. **可扩展性**: 支持管道模式，可以添加缓存、限流、遥测等中间件
3. **元数据支持**: 通过 `GetService` 获取模型信息和元数据
4. **未来兼容性**: 微软会持续维护和更新这个接口
5. **丰富的扩展方法**: 如 `GenerateVectorAsync()` 简化单文本向量化

### 示例：使用官方接口

```csharp
// 获取服务
var generator = serviceProvider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

// 批量向量化
var embeddings = await generator.GenerateAsync(new[] { "text1", "text2" });

// 获取元数据
var metadata = generator.GetService<EmbeddingGeneratorMetadata>();
Console.WriteLine($"Model: {metadata.ModelId}, Provider: {metadata.Provider}");

// 使用扩展方法简化单文本向量化
var vector = await generator.GenerateVectorAsync("single text");
```

## 注意事项

1. **API Key 安全**: 请妥善保管 DashScope API Key，不要提交到代码仓库
2. **速率限制**: 阿里云 API 有调用频率限制，大批量文档处理时注意控制并发
3. **费用**: DashScope 按调用次数计费，请关注账单
4. **向量存储**: 确保已正确配置 Qdrant 服务
5. **租户隔离**: 向量集合使用租户 ID 隔离，格式: `tenant-{tenantId}_documents`
6. **NuGet 包**: 需要 `Microsoft.Extensions.AI` 包（已包含在 OmniMind.Ingestion.csproj）

## 测试建议

1. 先上传一个小文档测试完整流程
2. 检查日志确认向量化成功
3. 使用 Qdrant 客户端验证向量已存储
4. 进行相似度搜索测试

## 下一步优化建议

1. **错误重试**: 添加 API 调用失败的重试机制
2. **缓存**: 使用官方的 `UseDistributedCache` 扩展方法添加缓存
3. **批量优化**: 根据实际性能调整批量大小
4. **监控**: 添加遥测（使用 `UseOpenTelemetry` 扩展方法）
5. **本地模型**: 完善 ONNX Runtime 或其他本地模型实现
6. **中间件**: 添加限流中间件防止超过 API 速率限制

## 参考文档

- [Microsoft.Extensions.AI 官方文档](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [IEmbeddingGenerator 接口文档](https://learn.microsoft.com/en-us/dotnet/ai/iembeddinggenerator)
- [阿里云 DashScope API 文档](https://help.aliyun.com/zh/dashscope/developer-reference/text-embedding-api-details)
