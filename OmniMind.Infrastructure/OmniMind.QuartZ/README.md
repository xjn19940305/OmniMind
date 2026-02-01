# Quartz 文档处理任务

## 概述

`DocumentProcessingJob` 是基于Quartz.NET框架实现的文档处理定时任务，利用Quartz的数据库持久化和集群机制，完美解决多节点环境下的任务重复执行问题。

## 为什么使用Quartz而不是BackgroundService？

### Quartz的优势

1. **天然支持集群** - 使用数据库锁机制，多节点环境下只有一个节点执行Job
2. **持久化** - Job状态和执行历史保存在数据库
3. **可视化** - 可以使用Quartz管理界面查看Job执行情况
4. **灵活调度** - 支持Cron表达式，调度更灵活
5. **容错机制** - 节点宕机后其他节点会接管Job

### 与BackgroundService的对比

| 特性 | Quartz | BackgroundService |
|------|--------|-------------------|
| 多节点去重 | ✅ 自动 | ❌ 需要手动实现分布式锁 |
| 持久化 | ✅ 数据库 | ❌ 内存状态 |
| 可视化管理 | ✅ 支持 | ❌ 不支持 |
| 灵活调度 | ✅ Cron表达式 | ⚠️ 依赖Interval |
| 失败重试 | ✅ 内置 | ❌ 需要手动实现 |
| 集群支持 | ✅ 原生 | ❌ 需要额外组件 |

## 架构设计

```
┌─────────────────────────────────────────────────┐
│            Quartz Scheduler Cluster              │
│  (使用MySQL数据库锁实现分布式协调)               │
└─────────────────────────────────────────────────┘
                        │
         ┌──────────────┼──────────────┐
         │              │              │
    ┌────▼────┐   ┌────▼────┐   ┌────▼────┐
    │ Node 1  │   │ Node 2  │   │ Node 3  │
    │ API服务  │   │ API服务  │   │ API服务  │
    └────┬────┘   └────┬────┘   └────┬────┘
         │              │              │
         └──────────────┴──────────────┘
                        │
         只有一个节点会执行DocumentProcessingJob
                        │
         ┌──────────────▼──────────────┐
         │   DocumentProcessingJob     │
         │   - 处理Uploaded状态的文档  │
         │   - 下载MinIO文件           │
         │   - 解析、切片、向量化      │
         └─────────────────────────────┘
```

## 两种工作模式

### 模式1: 批量处理模式（推荐）

**特点**：
- 定时从数据库查询`Status=Uploaded`的文档
- 每次处理一批（如10个）
- 适合文档量不大、处理耗时的场景

**配置示例**：

```csharp
options.AddJob<DocumentProcessingJob>(config =>
{
    config.WithIdentity("DocumentProcessingJob")
    .StoreDurably()
    .UsingJobData("mode", "batch")      // 批量模式
    .UsingJobData("batchSize", 10)      // 每批处理10个文档
    .UsingJobData("timeoutSeconds", 60);
})
.AddTrigger(opt =>
{
    opt.WithIdentity("DocumentProcessingJobTrigger")
    .ForJob("DocumentProcessingJob")
    // 每分钟执行一次
    .WithCronSchedule("0 * * * * ?")
    .StartNow();
});
```

**工作流程**：

```
每分钟触发
    ↓
查询数据库: SELECT TOP 10 * FROM Documents WHERE Status = 'Uploaded'
    ↓
逐个处理文档
    ↓
更新状态: Uploaded → Parsing → Parsed → Indexed
```

### 模式2: 持续监听模式

**特点**：
- 启动后持续监听RabbitMQ队列
- 实时消费消息，延迟更低
- 适合高吞吐、实时性要求高的场景

**配置示例**：

```csharp
options.AddJob<DocumentProcessingJob>(config =>
{
    config.WithIdentity("DocumentProcessingJobContinuous")
    .StoreDurably()
    .UsingJobData("mode", "continuous");  // 持续模式
})
.AddTrigger(opt =>
{
    opt.WithIdentity("DocumentProcessingJobContinuousTrigger")
    .ForJob("DocumentProcessingJobContinuous")
    // 每小时检查一次（容错机制，如果Job停止了会重启）
    .WithCronSchedule("0 0 * * * ?")
    .StartNow();
});
```

**工作流程**：

```
Job启动
    ↓
连接RabbitMQ
    ↓
持续监听队列
    ↓
收到消息 → 立即处理 → ACK
```

## Cron表达式示例

```csharp
// 每分钟执行
"0 * * * * ?"

// 每5分钟执行
"0 */5 * * * ?"

// 每小时执行
"0 0 * * * ?"

// 每天凌晨1点执行
"0 0 1 * * ?"

// 工作时间每10分钟执行（周一到周五，9点到18点）
"0 */10 9-18 ? * MON-FRI"

// 每30秒执行
"0/30 * * * * ?"
```

## 参数配置

### JobDataMap参数

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| mode | string | "batch" | 工作模式：batch（批量）或 continuous（持续） |
| batchSize | int | 10 | 每批处理的文档数量（仅批量模式） |
| timeoutSeconds | int | 60 | 超时时间（秒） |

### 调整处理速度

```csharp
// 方式1: 调整Cron表达式（执行频率）
.WithCronSchedule("0 */10 * * * ?")  // 每10分钟执行一次

// 方式2: 调整批次大小（每次处理数量）
.UsingJobData("batchSize", 50)  // 每次处理50个文档

// 方式3: 组合使用
// 每分钟执行，每次处理20个 = 每分钟最多处理20个文档
```

## 数据库表

Quartz会在MySQL中创建以下表：

```sql
-- 任务详情表
QRTZ_JOB_DETAILS

-- 触发器表
QRTZ_TRIGGERS

-- 简单触发器表
QRTZ_SIMPLE_TRIGGERS

-- Cron触发器表
QRTZ_CRON_TRIGGERS

-- 调度器状态表
QRTZ_SCHEDULER_STATE

-- 锁表（实现分布式锁）
QRTZ_LOCKS
```

## 监控和管理

### 查看Job执行日志

```
[DocumentProcessingJob] DocumentProcessingJob 开始执行
[批量模式] 开始处理文档，批次大小: 10
[批量模式] 找到 5 个待处理文档
[文档处理] 开始处理: DocumentId=xxx, Title=示例文档.pdf
[文档处理] 正在解析文档: xxx
[文档处理] 正在切片文档: xxx
[文档处理] 正在向量化文档: xxx
[文档处理] 处理完成: DocumentId=xxx, 耗时=2500ms
[批量模式] 批次处理完成，成功处理 5 个文档
[DocumentProcessingJob] DocumentProcessingJob 执行完成
```

### 数据库查询

```sql
-- 查看待处理的文档
SELECT Id, Title, Status, CreatedAt
FROM Documents
WHERE Status = 1  -- Uploaded
ORDER BY CreatedAt
LIMIT 20;

-- 查看处理失败的文档
SELECT Id, Title, Error, UpdatedAt
FROM Documents
WHERE Status = 6  -- Failed
ORDER BY UpdatedAt DESC;

-- 统计各状态文档数量
SELECT Status, COUNT(*) as Count
FROM Documents
GROUP BY Status;
```

### Quartz管理界面

可以使用第三方管理界面查看Job状态：
- [Quartz.NET Admin Web](https://github.com/gigi81/quartznet-admin)

## 性能优化

### 1. 并发控制

```csharp
// 在Program.cs中配置线程池大小
options.UseDefaultThreadPool(c =>
{
    c.MaxConcurrency = Environment.ProcessorCount * 2;  // 默认值
});
```

### 2. 禁止并发执行

Job已添加`[DisallowConcurrentExecution]`属性，确保同一个Job不会并发执行。

### 3. 调整批次大小

根据文档处理耗时调整：
- 处理快（<1秒）：batchSize = 50-100
- 处理中等（1-5秒）：batchSize = 10-20
- 处理慢（>5秒）：batchSize = 5-10

### 4. 错误处理

```csharp
// 处理失败时：
// 1. 更新文档状态为Failed
// 2. 记录错误信息到Error字段
// 3. 继续处理下一个文档（不会中断整个批次）

// 可以通过定时任务扫描Failed状态的文档进行重试
```

## 容错机制

### 1. 节点故障自动切换

```
Node 1 执行Job中...
    ↓
Node 1 宕机
    ↓
数据库锁超时释放
    ↓
Node 2 获取锁并接管Job
```

### 2. Job失败重试

```csharp
// 可选：配置重试策略
[RetryFailedJob(times: 3, interval: TimeSpan.FromMinutes(5))]
public class DocumentProcessingJob : IJob
{
    // ...
}
```

### 3. 持续模式容错

```csharp
// 每小时检查一次Job是否在运行
.WithCronSchedule("0 0 * * * ?")
// 如果Job停止了会自动重启
```

## 运维建议

### 1. 监控指标

- Job执行频率
- 每批处理文档数量
- 平均处理耗时
- 失败率
- 队列积压情况

### 2. 告警规则

- 连续失败3次 → 发送告警
- 处理耗时>10秒 → 发送告警
- 队列积压>1000 → 扩容或手动处理

### 3. 手动触发

可以通过Quartz API手动触发Job：

```csharp
// 在Controller中添加管理接口
[HttpPost("admin/jobs/trigger")]
public async Task<IActionResult> TriggerJob()
{
    var scheduler = RequestServices.GetRequiredService<IScheduler>();
    await scheduler.TriggerJob(new JobKey("DocumentProcessingJob"));
    return Ok("Job triggered successfully");
}
```

## 常见问题

### Q: 为什么Job没有执行？

检查项：
1. Quartz调度器是否启动（查看日志）
2. 数据库连接是否正常
3. Job是否正确注册
4. Cron表达式是否正确
5. 查看QRTZ_TRIGGERS表状态

### Q: 多节点会重复执行吗？

不会。Quartz使用数据库行锁（`QRTZ_LOCKS`表）确保同一时刻只有一个节点执行Job。

### Q: 如何调整处理速度？

1. 调整Cron表达式（执行频率）
2. 调整batchSize（批次大小）
3. 部署多个API节点（Quartz会自动协调）

### Q: Job执行时间过长怎么办？

1. 减小batchSize
2. 优化文档处理逻辑
3. 使用持续模式代替批量模式

## 下一步开发

1. **实现文档解析**：集成PDF/DOCX解析库
2. **实现文本切片**：开发智能切片算法
3. **实现向量化**：集成嵌入模型
4. **添加重试机制**：对Failed状态文档重试
5. **性能监控**：集成Prometheus/Grafana
