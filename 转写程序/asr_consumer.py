#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ASR 转录消费者（直接调用模型版本）

功能：
1. 从 asr-request 队列接收消息（包含：userid, objectkey, documentid, taskid）
2. 使用 OSS SDK 下载文件
3. 直接调用 FunASR 模型进行转录
4. 将结果发送到 asr-completed 队列
"""

import pika
import json
import logging
import configparser
import threading
import time
import queue
import os
import tempfile
from typing import Dict, Any, Optional
from datetime import datetime
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

# 导入模型和工具
from paraformerdome import AutoSpeakerRecognitionSystem
from oss_downloader import OSSClient
from audio_extractor import AudioExtractor
from resource_monitor import ResourceMonitor

# 配置日志
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class ASRTranscriptionConsumer:
    """ASR 转录任务消费者（直接调用模型）"""

    def __init__(self, config_file: str = 'config.ini'):
        """初始化消费者"""
        # 读取配置
        self.config = configparser.ConfigParser()
        self.config.read(config_file, encoding='utf-8')

        # RabbitMQ 配置
        self.rabbitmq_config = {
            'host': self.config.get('rabbitmq', 'host'),
            'port': self.config.getint('rabbitmq', 'port'),
            'username': self.config.get('rabbitmq', 'username'),
            'password': self.config.get('rabbitmq', 'password'),
            'virtual_host': self.config.get('rabbitmq', 'virtual_host')
        }

        # 队列配置
        self.request_queue = self.config.get(
            'queues',
            'transcribe_request',
            fallback=self.config.get('queues', 'asr_request', fallback='transcribe-request')
        )
        self.completed_queue = self.config.get(
            'queues',
            'transcribe_completed',
            fallback=self.config.get('queues', 'asr_completed', fallback='transcribe-completed')
        )

        # Exchange 配置（.NET 端使用）
        self.exchange = self.config.get('queues', 'asr_exchange', fallback='asr-exchange')
        self.request_routing_key = self.config.get(
            'queues',
            'transcribe_request_routing_key',
            fallback=self.config.get('queues', 'asr_request_routing_key', fallback='transcribe.request')
        )
        self.completed_routing_key = self.config.get(
            'queues',
            'transcribe_completed_routing_key',
            fallback=self.config.get('queues', 'asr_completed_routing_key', fallback='transcribe.completed')
        )

        # 并发控制配置
        self.max_concurrent = self.config.getint('concurrency', 'max_concurrent_tasks', fallback=10)

        # 资源监控配置
        self.enable_monitoring = self.config.getboolean('resource_limits', 'enable_resource_monitoring', fallback=True)
        cpu_threshold = self.config.getfloat('resource_limits', 'cpu_threshold', fallback=80.0)
        memory_threshold = self.config.getfloat('resource_limits', 'memory_threshold', fallback=85.0)
        self.check_interval = self.config.getint('resource_limits', 'check_interval', fallback=5)
        self.pause_duration = self.config.getint('resource_limits', 'pause_duration', fallback=30)

        # 初始化资源监控器
        self.resource_monitor = ResourceMonitor(cpu_threshold, memory_threshold)

        # 连接和通道
        self.connection = None
        self.channel = None

        # 线程池
        self.executor = ThreadPoolExecutor(max_workers=self.max_concurrent)

        # ACK/NACK 队列（用于线程间通信）
        self.ack_queue = queue.Queue()

        # 统计信息
        self.stats = {
            'total_processed': 0,
            'total_success': 0,
            'total_failed': 0,
            'current_processing': 0,
            'paused_count': 0
        }
        self.stats_lock = threading.Lock()

        # 控制标志
        self.is_paused = False
        self.should_stop = False
        self.consumer_tag = None
        self.last_overload_check = 0
        self.pause_until = 0.0
        self.stats_monitor_started = False

        # ✅ 初始化 FunASR 模型（全局单例，线程共享）
        logger.info("=" * 60)
        logger.info("正在初始化 FunASR 模型...")
        logger.info("这可能需要几分钟时间，请耐心等待...")
        logger.info("=" * 60)
        self.asr_system = AutoSpeakerRecognitionSystem()
        logger.info("=" * 60)
        logger.info("模型初始化完成！")
        logger.info("=" * 60)

        # ✅ 初始化 OSS 客户端
        self.oss_client = OSSClient(config_file)

        # ✅ 初始化音频提取器
        self.audio_extractor = AudioExtractor()

        # 临时文件目录
        self.temp_dir = tempfile.gettempdir()
        self.temp_files = {}  # task_id -> temp_file_path

        logger.info(f"✅ 消费者初始化完成")
        logger.info(f"📊 最大并发数: {self.max_concurrent}")
        logger.info(f"📥 请求队列: {self.request_queue}")
        logger.info(f"📤 完成队列: {self.completed_queue}")
        logger.info(f"📊 资源监控: {'启用' if self.enable_monitoring else '禁用'}")

    def connect(self):
        """连接到 RabbitMQ"""
        credentials = pika.PlainCredentials(
            self.rabbitmq_config['username'],
            self.rabbitmq_config['password']
        )

        parameters = pika.ConnectionParameters(
            host=self.rabbitmq_config['host'],
            port=self.rabbitmq_config['port'],
            virtual_host=self.rabbitmq_config['virtual_host'],
            credentials=credentials,
            heartbeat=600,
            blocked_connection_timeout=7200
        )

        self.connection = pika.BlockingConnection(parameters)
        self.channel = self.connection.channel()

        # ============ 声明队列（直接队列模式，不使用 Exchange）============
        logger.info(f"📋 声明队列: {self.request_queue}, {self.completed_queue}")
        self.channel.queue_declare(queue=self.request_queue, durable=True)
        self.channel.queue_declare(queue=self.completed_queue, durable=True)
        logger.info(f"✅ 队列声明成功")

        # 设置 QoS
        self.channel.basic_qos(prefetch_count=self.max_concurrent)

        logger.info(f"✅ 已连接到 RabbitMQ: {self.rabbitmq_config['host']}")

    def drain_ack_queue(self, limit: int = 100000):
        """清空 ACK 队列（重连保护）"""
        n = 0
        while n < limit:
            try:
                self.ack_queue.get_nowait()
                n += 1
            except queue.Empty:
                break
        if n:
            logger.warning(f"🧹 丢弃 ACK 动作 {n} 条（重连保护）")

    def process_ack_queue(self):
        """处理 ACK/NACK 队列（在主线程中执行）"""
        while not self.should_stop:
            try:
                # 到点恢复消费
                if self.is_paused and self.pause_until and time.time() >= self.pause_until:
                    self.pause_until = 0
                    self.resume_consuming()
                    logger.info("▶️  资源恢复，继续消费")

                # 取 action
                try:
                    action = self.ack_queue.get(timeout=0.1)
                except queue.Empty:
                    action = None

                if action:
                    if action['type'] == 'ack':
                        self.channel.basic_ack(delivery_tag=action['delivery_tag'])
                    elif action['type'] == 'nack':
                        self.channel.basic_nack(delivery_tag=action['delivery_tag'], requeue=False)
                    elif action['type'] == 'publish':
                        # 直接发送到队列（不使用 Exchange）
                        target_queue = action.get('queue', self.completed_queue)
                        self.channel.basic_publish(
                            exchange='',  # 空字符串表示直接发送到队列
                            routing_key=target_queue,
                            body=action['body'],
                            properties=action['properties']
                        )

                # 驱动事件循环
                self.connection.process_data_events(time_limit=0)

            except (pika.exceptions.StreamLostError,
                    pika.exceptions.ConnectionClosedByBroker,
                    pika.exceptions.AMQPConnectionError,
                    pika.exceptions.ChannelWrongStateError) as conn_error:
                logger.error(f"⚠️  RabbitMQ 连接/通道异常: {conn_error}")
                raise
            except Exception as e:
                logger.error(f"处理 ACK 队列失败: {e}", exc_info=True)
                raise

    def on_message(self, ch, method, properties, body):
        """消息处理回调"""
        try:
            if self.should_stop:
                return

            # 检查资源使用情况
            current_time = time.time()
            if self.enable_monitoring and (current_time - self.last_overload_check) > 2:
                self.last_overload_check = current_time
                is_overloaded, reason = self.resource_monitor.is_overloaded()

                if is_overloaded:
                    logger.warning(f"⚠️  系统资源过载: {reason}")
                    logger.warning(f"⏸️  暂停消费 {self.pause_duration} 秒...")

                    with self.stats_lock:
                        self.stats['paused_count'] += 1

                    self.pause_until = current_time + self.pause_duration
                    self.pause_consuming()
                    return

            # 解析消息
            message = json.loads(body.decode('utf-8'))

            # ============ 提取字段（支持 .NET 和 Python 格式）============
            # .NET 格式: DocumentId, ObjectKey, CallbackQueue, ContentType, FileName, Language
            # Python 格式: taskid, userid, objectkey, documentid

            # TaskId - 优先使用 .NET 的 DocumentId，否则使用 taskid/taskId/TaskId，最后生成时间戳
            task_id = (message.get('DocumentId') or
                      message.get('taskid') or
                      message.get('taskId') or
                      message.get('TaskId') or
                      f"task_{int(time.time() * 1000)}")

            # UserId - .NET 端没有此字段，使用空字符串
            user_id = (message.get('userid') or
                      message.get('userId') or
                      message.get('UserId') or
                      '')  # .NET 端不发送 user_id

            # ObjectKey - OSS 文件路径
            object_key = (message.get('ObjectKey') or
                         message.get('objectkey') or
                         message.get('objectKey') or
                         message.get('ObjectKey') or
                         '')

            # DocumentId - 文档 ID
            document_id = (message.get('DocumentId') or
                          message.get('documentid') or
                          message.get('documentId') or
                          message.get('DocumentId') or
                          '')

            # 额外字段（.NET 端发送）
            callback_queue = message.get('CallbackQueue', self.completed_queue)
            content_type = message.get('ContentType', '')
            file_name = message.get('FileName', '')
            language = message.get('Language', 'zh_cn')

            logger.info(f"📥 收到任务: {task_id}")
            logger.info(f"  文档ID: {document_id}")
            logger.info(f"  文件名: {file_name}")
            logger.info(f"  对象Key: {object_key}")
            logger.info(f"  内容类型: {content_type}")
            logger.info(f"  语言: {language}")

            # 增加处理计数
            with self.stats_lock:
                self.stats['current_processing'] += 1
                current = self.stats['current_processing']

            logger.info(f"  当前处理中: {current}/{self.max_concurrent}")

            # 提交到线程池处理
            self.executor.submit(
                self.process_message_wrapper,
                message,
                method.delivery_tag,
                task_id,
                user_id,
                object_key,
                document_id,
                callback_queue,  # 传递回调队列
                language  # 传递语言参数
            )

        except Exception as e:
            logger.error(f"❌ 消息处理失败: {str(e)}", exc_info=True)
            ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)

    def pause_consuming(self):
        """暂停消费"""
        if self.consumer_tag and not self.is_paused:
            try:
                self.channel.basic_cancel(self.consumer_tag)
                self.connection.process_data_events(time_limit=1)
                self.is_paused = True
                logger.info("⏸️  已暂停消费")
            except Exception as e:
                logger.error(f"暂停消费失败: {str(e)}")

    def resume_consuming(self):
        """恢复消费"""
        if self.is_paused:
            try:
                self.consumer_tag = self.channel.basic_consume(
                    queue=self.request_queue,
                    on_message_callback=self.on_message,
                    auto_ack=False
                )
                self.is_paused = False
                logger.info("▶️  已恢复消费")
            except Exception as e:
                logger.error(f"恢复消费失败: {str(e)}")

    def process_message_wrapper(self, message: Dict[str, Any], delivery_tag: int,
                                task_id: str, user_id: str, object_key: str, document_id: str,
                                callback_queue: str = None, language: str = "auto"):
        """消息处理包装器（在线程池中执行）"""
        start_time = time.time()

        try:
            # 处理转录任务
            result = self.process_transcription(task_id, user_id, object_key, document_id,
                                               callback_queue, language)

            # 发送结果到完成队列（使用 CallbackQueue 或默认队列）
            target_queue = callback_queue or self.completed_queue
            self.send_result_to_queue(result, target_queue)

            # 检查是否成功（使用 Status 字段）
            status = result.get('Status')
            if status in ('success', 'Success', 0):
                self.ack_queue.put({'type': 'ack', 'delivery_tag': delivery_tag})

                with self.stats_lock:
                    self.stats['total_processed'] += 1
                    self.stats['total_success'] += 1
                    self.stats['current_processing'] -= 1

                elapsed = time.time() - start_time
                logger.info(f"✅ 任务完成: {task_id} (耗时: {elapsed:.2f}秒)")
            else:
                logger.error(f"❌ 转录失败: {task_id}, 错误: {result.get('ErrorMessage', 'Unknown error')}")
                self.ack_queue.put({'type': 'nack', 'delivery_tag': delivery_tag})

                with self.stats_lock:
                    self.stats['total_processed'] += 1
                    self.stats['total_failed'] += 1
                    self.stats['current_processing'] -= 1

        except Exception as e:
            logger.error(f"❌ 任务处理异常: {task_id}, 错误: {str(e)}", exc_info=True)
            self.ack_queue.put({'type': 'nack', 'delivery_tag': delivery_tag})

            with self.stats_lock:
                self.stats['total_processed'] += 1
                self.stats['total_failed'] += 1
                self.stats['current_processing'] -= 1

        finally:
            # 清理临时文件
            self.cleanup_temp_file(task_id)

    def cleanup_temp_file(self, task_id: str):
        """清理任务相关的临时文件"""
        if task_id in self.temp_files:
            temp_path = self.temp_files[task_id]
            try:
                if os.path.exists(temp_path):
                    os.remove(temp_path)
                    logger.debug(f"已清理临时文件: {temp_path}")
            except Exception as e:
                logger.warning(f"清理临时文件失败: {str(e)}")
            finally:
                del self.temp_files[task_id]

    def process_transcription(self, task_id: str, user_id: str, object_key: str, document_id: str,
                             callback_queue: str = None, language: str = "auto") -> Dict[str, Any]:
        """
        处理转录任务（直接调用模型）

        流程：
        1. 参数验证
        2. 从 OSS 下载文件
        3. 音频格式优化（如需要）
        4. 调用 FunASR 模型
        5. 格式化结果
        """
        start_time = time.time()
        audio_content = None
        error_type = 'Unknown'

        try:
            # ============================================
            # 1️⃣ 参数验证
            # ============================================
            if not object_key:
                error_msg = "缺少必需参数: ObjectKey"
                logger.error(f"❌ [{task_id}] {error_msg}")

                # 处理 DocumentId 类型
                if isinstance(document_id, int):
                    doc_id = document_id
                elif document_id and str(document_id).replace('-', '').replace('+', '').isdigit():
                    doc_id = int(document_id)
                else:
                    doc_id = document_id or 0

                return {
                    'DocumentId': doc_id,
                    'Status': 'failed',
                    'ErrorMessage': error_msg,
                }

            # ============================================
            # 2️⃣ 从 OSS 下载文件
            # ============================================
            logger.info(f"📥 [{task_id}] 从 OSS 下载文件...")
            logger.info(f"  ObjectKey: {object_key}")

            success, file_path, file_content = self.oss_client.download_file(object_key)

            if not success or not file_content:
                error_type = 'OSSDownloadError'
                error_msg = f"从 OSS 下载文件失败: {object_key}"
                logger.error(f"❌ [{task_id}] {error_msg}")

                # 处理 DocumentId 类型
                if isinstance(document_id, int):
                    doc_id = document_id
                elif document_id and str(document_id).replace('-', '').replace('+', '').isdigit():
                    doc_id = int(document_id)
                else:
                    doc_id = document_id or 0

                return {
                    'DocumentId': doc_id,
                    'Status': 'failed',
                    'ErrorMessage': error_msg,
                }

            # 保存临时文件路径用于后续清理
            self.temp_files[task_id] = file_path

            file_size_mb = len(file_content) / (1024 * 1024)
            logger.info(f"✅ [{task_id}] 文件下载成功 ({file_size_mb:.2f} MB)")

            # ============================================
            # 3️⃣ 音频格式优化
            # ============================================
            audio_content = file_content
            audio_file_name = os.path.basename(object_key)

            # 检测文件类型
            file_type = self.audio_extractor.detect_file_type_by_content(file_content)

            if file_type == 'video':
                logger.info(f"🎬 [{task_id}] 文件头检测: 视频文件")
                is_video = True
            elif file_type == 'audio':
                logger.info(f"🎵 [{task_id}] 文件头检测: 音频文件")
                is_video = False
            else:
                logger.warning(f"⚠️  [{task_id}] 文件头检测失败，使用扩展名判断")
                is_video = self.audio_extractor.is_video_file(audio_file_name)

            if is_video:
                # 视频文件：提取音频
                try:
                    logger.info(f"🎬 [{task_id}] 开始从视频中提取音频...")
                    extracted_audio, extract_error = self.audio_extractor.extract_audio_from_video(
                        video_data=file_content,
                        output_format='wav'
                    )

                    if extract_error or not extracted_audio:
                        error_type = 'AudioExtractionError'
                        raise Exception(f"音频提取失败: {extract_error}")

                    audio_content = extracted_audio
                    audio_file_name = os.path.splitext(audio_file_name)[0] + '.wav'
                    logger.info(f"✅ [{task_id}] 音频提取完成")

                    # 释放原始视频内存
                    del file_content

                except Exception as e:
                    logger.error(f"❌ [{task_id}] 音频提取失败: {str(e)}")
                    audio_content = file_content

            else:
                # 音频文件：转换为最佳格式
                try:
                    logger.info(f"🎵 [{task_id}] 开始优化音频格式...")
                    original_format = os.path.splitext(audio_file_name)[1].lstrip('.')

                    converted_audio, convert_error = self.audio_extractor.convert_audio_format(
                        audio_data=file_content,
                        input_format=original_format,
                        output_format='wav'
                    )

                    if convert_error or not converted_audio:
                        logger.warning(f"⚠️  [{task_id}] 音频格式转换失败，使用原始文件")
                        audio_content = file_content
                    else:
                        audio_content = converted_audio
                        audio_file_name = os.path.splitext(audio_file_name)[0] + '.wav'
                        logger.info(f"✅ [{task_id}] 音频格式转换完成")
                        del file_content

                except Exception as e:
                    logger.error(f"❌ [{task_id}] 音频格式转换异常: {str(e)}")
                    audio_content = file_content

            # ============================================
            # 4️⃣ 保存音频文件到临时目录
            # ============================================
            temp_audio_path = os.path.join(self.temp_dir, f"{task_id}_{audio_file_name}")
            with open(temp_audio_path, 'wb') as f:
                f.write(audio_content)

            self.temp_files[task_id] = temp_audio_path

            # ============================================
            # 5️⃣ 调用 FunASR 模型
            # ============================================
            logger.info(f"🎙️  [{task_id}] 开始转录...")

            # 加载用户声纹库（如果提供了 user_id）
            # 注意：这里不使用全局 speaker_db，而是每次创建新的实例来避免线程安全问题
            if user_id:
                try:
                    self.asr_system.speaker_db = {}
                    self.asr_system.load_speaker_db(user_id)
                    logger.info(f"✅ [{task_id}] 已加载用户声纹库: {user_id}")
                except Exception as spk_error:
                    logger.warning(f"⚠️  [{task_id}] 加载声纹库失败，使用自动识别: {spk_error}")
                    self.asr_system.speaker_db = {}
            else:
                # .NET 端不发送 user_id，使用空声纹库（纯自动识别）
                self.asr_system.speaker_db = {}
                logger.info(f"ℹ️  [{task_id}] 未提供 user_id，使用纯自动说话人识别")

            # 执行转录
            result = self.asr_system.diarize_with_auto_registration(
                audio_url=temp_audio_path,
                threshold=0.6,
                hotWord="",
                language=language,  # 使用 .NET 端传递的语言参数
                separationSpeaker=True,
                enable_speaker_verification=False
            )

            if not result.get('success', False):
                error_type = 'ASRModelError'
                error_msg = result.get('message', 'FunASR 模型返回失败')
                raise Exception(error_msg)

            data = result.get('data', {})

            # ============================================
            # 6️⃣ 格式化结果
            # ============================================
            transcription_text = data.get('text', '')
            segments = data.get('segments', [])
            speakers = data.get('speakers', {})

            # 转换 segments 格式（兼容 C# 端）
            formatted_segments = []
            for seg in segments:
                formatted_seg = {
                    'Id': seg.get('segment_id', seg.get('id', 0)),
                    'Speaker': seg.get('speaker_name', seg.get('speaker', 'Unknown')),
                    'SpeakerId': seg.get('real_speaker_id', seg.get('speaker_id', '')),
                    'Text': seg.get('text', ''),
                    'StartTime': seg.get('start_time_ms', seg.get('start', 0)),
                    'EndTime': seg.get('end_time_ms', seg.get('end', 0)),
                    'Duration': seg.get('duration_ms', seg.get('duration', 0))
                }
                formatted_segments.append(formatted_seg)

            # 转换 speakers 格式
            formatted_speakers = []
            for spk_id, spk_info in speakers.items():
                formatted_speakers.append({
                    'Id': spk_id,
                    'Name': spk_info.get('real_name', ''),
                    'Color': spk_info.get('color', ''),
                    'Confidence': spk_info.get('confidence', 0.0),
                    'SegmentCount': spk_info.get('segment_count', 0),
                    'TotalDurationMs': spk_info.get('total_duration_ms', 0)
                })

            processing_time = time.time() - start_time

            logger.info(f"✅ [{task_id}] 转录成功")
            logger.info(f"    文本长度: {len(transcription_text)} 字符")
            logger.info(f"    片段数量: {len(formatted_segments)}")
            logger.info(f"    说话人数: {len(formatted_speakers)}")
            logger.info(f"    处理耗时: {processing_time:.2f} 秒")

            # ============================================
            # 7️⃣ 上传转录结果到 OSS
            # ============================================
            # 构建转录数据
            transcription_data = {
                'Text': transcription_text,
                'Segments': formatted_segments,
                'Speakers': formatted_speakers,
                'SpeakerCount': len(formatted_speakers),
                'SegmentCount': len(formatted_segments),
                'DurationMs': data.get('duration_ms', 0),
                'ProcessingTime': processing_time
            }

            # 上传到 OSS，文件路径为: transcription/userid/documentid.json
            logger.info(f"📤 [{task_id}] 上传转录结果到 OSS...")
            upload_success, result_object_key = self.oss_client.upload_transcription_result(
                source_object_key=object_key,
                transcription_data=transcription_data,
                user_id=user_id or 'default',
                document_id=str(document_id or '0')
            )

            if not upload_success:
                logger.warning(f"⚠️  [{task_id}] 上传转录结果到 OSS 失败")
                result_object_key = None
            else:
                logger.info(f"✅ [{task_id}] 转录结果已上传: {result_object_key}")

            # ============================================
            # 8️⃣ 返回成功结果（匹配 .NET 端的 AsrCompletedMessage 结构）
            # ============================================
            # 处理 DocumentId 类型（.NET 发送的是 long/int）
            try:
                if isinstance(document_id, int):
                    doc_id = document_id
                elif document_id:
                    # 尝试转换为数字
                    doc_id = int(document_id) if str(document_id).replace('-', '').replace('+', '').isdigit() else document_id
                else:
                    doc_id = 0
            except (ValueError, TypeError):
                doc_id = document_id or 0

            response = {
                'DocumentId': doc_id,
                'Status': 0 if upload_success and result_object_key else 1,
                'Provider': 'funasr',
                'DurationMs': data.get('duration_ms', 0),
            }

            if upload_success and result_object_key:
                response['TranscribedTextObjectKey'] = result_object_key
            else:
                response['Error'] = 'Failed to upload transcription result to object storage'

            # 可选字段（安全添加）
            try:
                if transcription_text:
                    response['TextLength'] = len(transcription_text)
                if formatted_speakers:
                    response['SpeakerCount'] = len(formatted_speakers)
                if formatted_segments:
                    response['SegmentCount'] = len(formatted_segments)
                if data and data.get('duration_ms', 0) > 0:
                    response['DurationMs'] = data.get('duration_ms', 0)
            except Exception as e:
                logger.warning(f"⚠️  [{task_id}] 添加可选字段时出错: {str(e)}")

            return response

        except Exception as e:
            processing_time = time.time() - start_time
            error_message = str(e)

            logger.error(f"❌ [{task_id}] 转录失败: {error_message}", exc_info=True)
            logger.error(f"    错误类型: {error_type}")

            # 返回失败结果（匹配 .NET 端的 AsrCompletedMessage 结构）
            # 处理 DocumentId 类型
            if isinstance(document_id, int):
                doc_id = document_id
            elif document_id and str(document_id).replace('-', '').replace('+', '').isdigit():
                doc_id = int(document_id)
            else:
                doc_id = document_id or 0

            response = {
                'DocumentId': doc_id,
                'Status': 1,
                'Provider': 'funasr',
                'Error': f"[{error_type}] {error_message}",
            }

            return response

        finally:
            if audio_content:
                del audio_content

    def send_result_to_queue(self, result: Dict[str, Any], target_queue: str = None):
        """发送结果到 RabbitMQ 完成队列"""
        try:
            result['CompletedAt'] = datetime.now().isoformat()
            message = json.dumps(result, ensure_ascii=False)

            queue_name = target_queue or self.completed_queue

            self.ack_queue.put({
                'type': 'publish',
                'body': message.encode('utf-8'),
                'properties': pika.BasicProperties(
                    delivery_mode=2,
                    content_type='application/json'
                ),
                'queue': queue_name  # 添加目标队列信息
            })
            logger.info(f"📤 结果已加入发送队列 -> {queue_name}")

        except Exception as e:
            logger.error(f"发送结果失败: {str(e)}", exc_info=True)

    def start_consuming(self):
        """开始消费队列"""
        self.consumer_tag = self.channel.basic_consume(
            queue=self.request_queue,
            on_message_callback=self.on_message,
            auto_ack=False
        )
        logger.info(f"🎧 开始监听队列: {self.request_queue}")

    def start_stats_monitor(self):
        """启动统计监控线程"""
        def monitor():
            while not self.should_stop:
                time.sleep(self.check_interval)

                with self.stats_lock:
                    stats = self.stats.copy()

                logger.info(
                    f"📊 统计: "
                    f"总={stats['total_processed']}, "
                    f"成功={stats['total_success']}, "
                    f"失败={stats['total_failed']}, "
                    f"处理中={stats['current_processing']}/{self.max_concurrent}, "
                    f"拒绝={stats['paused_count']}"
                )

                if self.enable_monitoring:
                    self.resource_monitor.log_stats()

        monitor_thread = threading.Thread(target=monitor, daemon=True)
        monitor_thread.start()
        logger.info("📊 统计监控已启动")

    def run(self):
        """运行消费者（支持自动重连）"""
        max_retries = 5
        retry_delay = 5
        retry_count = 0

        while retry_count < max_retries:
            try:
                logger.info(f"🔌 正在连接 RabbitMQ... (尝试 {retry_count + 1}/{max_retries})")

                if retry_count > 0:
                    self.drain_ack_queue()
                    self.is_paused = False
                    self.pause_until = 0.0

                self.connect()

                if not self.stats_monitor_started:
                    self.start_stats_monitor()
                    self.stats_monitor_started = True

                self.start_consuming()

                logger.info("✅ 消费者启动成功")
                logger.info("按 Ctrl+C 停止消费者")

                retry_count = 0
                self.process_ack_queue()

            except KeyboardInterrupt:
                logger.info("\n⏹️  收到停止信号")
                break
            except Exception as e:
                retry_count += 1
                logger.error(f"❌ 消费者异常 (尝试 {retry_count}/{max_retries}): {str(e)}", exc_info=True)

                if retry_count < max_retries:
                    logger.info(f"⏳ {retry_delay} 秒后重试...")
                    time.sleep(retry_delay)
                    try:
                        if self.connection and not self.connection.is_closed:
                            self.connection.close()
                    except:
                        pass
                else:
                    logger.error(f"❌ 已达到最大重试次数 ({max_retries})，停止消费者")
                    break

        self.close()

    def close(self):
        """关闭连接"""
        self.should_stop = True

        logger.info("⏳ 等待所有任务完成...")
        self.executor.shutdown(wait=True)

        try:
            if self.connection and not self.connection.is_closed:
                self.connection.close()
                logger.info("✅ 已关闭 RabbitMQ 连接")
        except Exception as e:
            logger.warning(f"关闭连接时出现警告: {str(e)}")

        with self.stats_lock:
            stats = self.stats.copy()

        logger.info("\n" + "=" * 60)
        logger.info("📊 最终统计:")
        logger.info(f"  总处理: {stats['total_processed']}")
        logger.info(f"  成功: {stats['total_success']}")
        logger.info(f"  失败: {stats['total_failed']}")
        logger.info("=" * 60)


def main():
    """主函数"""
    logger.info("=" * 60)
    logger.info("ASR 转录消费者启动（直接调用模型版本）")
    logger.info("=" * 60)

    consumer = ASRTranscriptionConsumer()
    consumer.run()


if __name__ == '__main__':
    main()
