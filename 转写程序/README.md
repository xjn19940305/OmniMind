# 转写程序

这个目录现在只保留一条有效主链路：`API 服务 + RabbitMQ 消费者 + OSS 下载/回传 + FunASR 转写`。

## 当前结构

```text
转写程序/
├── README.md
├── api_server.py
├── asr_consumer.py
├── paraformerdome.py
├── oss_downloader.py
├── audio_extractor.py
├── resource_monitor.py
├── logger_config.py
├── requirements.txt
├── config.ini.example
├── config.docker.ini
├── config.ini
├── Dockerfile
├── docker-compose.yml
├── docker-compose.consumer-only.yml
├── deploy.sh
├── deploy_china.sh
├── start_asr_consumer.sh
├── start_asr_consumer.bat
├── scripts/
│   ├── funasr-api.service
│   ├── funasr-consumer.service
│   ├── install_service.sh
│   └── uninstall_service.sh
├── docs/
├── logs/
├── temp_oss_files/
└── speaker_workspace/
```

## 保留文件职责

- `api_server.py`: 对外提供 HTTP 接口。
- `asr_consumer.py`: 消费主站投递的 ASR 请求消息。
- `paraformerdome.py`: 模型加载、识别和说话人区分。
- `oss_downloader.py`: OSS 下载源文件并回传转写结果。
- `audio_extractor.py`: 视频抽音频、格式转换。
- `resource_monitor.py`: 资源占用监控。
- `logger_config.py`: 统一日志配置。
- `scripts/`: systemd 安装和卸载脚本。
- `docs/`: 参数和调优文档。

## 已删掉的旧链路

- 旧消费者：`rabbitmq_consumer.py`、`rabbitmq_consumer_concurrent_v2.py`
- 旧启动包装：`start_consumer.py`、`start_consumer_with_retry.py`、`start_concurrent_consumer.py`
- 数据库日志链路：`transcription_logger.py`、`create_table.py`、`db_config.*`
- 历史模型变体：`paraformerdome_cpu.py`、`paraformerdome_v1.py`
- 一次性测试脚本、历史部署说明、运行日志、缓存文件

## 运行方式

本地启动：

```bash
pip install -r requirements.txt
cp config.ini.example config.ini
python api_server.py
python asr_consumer.py
```

Docker 启动：

```bash
cp config.docker.ini config.ini
cp .env.example .env
./deploy.sh
```
