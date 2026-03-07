#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
音频提取工具
从视频文件中提取音频，并转换为 FunASR 最佳格式
"""

import os
import subprocess
import tempfile
import logging
from typing import Optional, Tuple
from datetime import datetime

logger = logging.getLogger(__name__)


class AudioExtractor:
    """音频提取器 - 从视频中提取音频并优化格式"""

    # 视频文件扩展名
    VIDEO_EXTENSIONS = {
        '.mp4', '.avi', '.mov', '.mkv', '.flv', '.wmv', '.webm',
        '.m4v', '.mpg', '.mpeg', '.3gp', '.f4v', '.rmvb'
    }

    # 音频文件扩展名
    AUDIO_EXTENSIONS = {
        '.mp3', '.wav', '.flac', '.aac', '.m4a', '.ogg',
        '.wma', '.opus', '.amr', '.ape'
    }

    # 视频文件头签名（Magic Number）
    VIDEO_SIGNATURES = {
        b'\x00\x00\x00\x18ftypmp4': 'mp4',
        b'\x00\x00\x00\x1cftypisom': 'mp4',
        b'\x00\x00\x00\x20ftypisom': 'mp4',
        b'RIFF': 'avi',  # 需要进一步检查 AVI 标识
        b'\x1a\x45\xdf\xa3': 'mkv',
        b'FLV': 'flv',
        b'\x30\x26\xb2\x75': 'wmv',
    }

    # 音频文件头签名
    AUDIO_SIGNATURES = {
        b'ID3': 'mp3',
        b'\xff\xfb': 'mp3',
        b'\xff\xf3': 'mp3',
        b'\xff\xf2': 'mp3',
        b'RIFF': 'wav',  # 需要进一步检查 WAV 标识
        b'fLaC': 'flac',
        b'\xff\xf1': 'aac',
        b'\xff\xf9': 'aac',
        b'OggS': 'ogg',
    }

    def __init__(self, save_dir: Optional[str] = None, organize_by_date: bool = True):
        """
        初始化音频提取器

        Args:
            save_dir: 音频保存目录（None表示不保存）
            organize_by_date: 是否按日期组织文件
        """
        self.temp_dir = tempfile.gettempdir()
        self.save_dir = save_dir
        self.organize_by_date = organize_by_date

        # 如果指定了保存目录，创建目录
        if self.save_dir:
            os.makedirs(self.save_dir, exist_ok=True)
            logger.info(f"✅ 音频保存目录: {self.save_dir}")

        logger.info("✅ 音频提取器初始化完成")

    def save_audio_file(
        self,
        audio_data: bytes,
        file_id: str,
        output_format: str = 'wav'
    ) -> Optional[str]:
        """
        保存音频文件到磁盘

        Args:
            audio_data: 音频数据
            file_id: 文件ID
            output_format: 音频格式

        Returns:
            Optional[str]: 保存的文件路径，失败返回None
        """
        if not self.save_dir:
            return None

        try:
            # 构建保存路径
            if self.organize_by_date:
                # 按日期组织：extracted_audio/2026-01-14/audio_xxx.wav
                date_str = datetime.now().strftime('%Y-%m-%d')
                save_path = os.path.join(self.save_dir, date_str)
                os.makedirs(save_path, exist_ok=True)
            else:
                # 直接保存：extracted_audio/audio_xxx.wav
                save_path = self.save_dir

            # 构建文件名：audio_{file_id}.wav
            filename = f"audio_{file_id}.{output_format}"
            full_path = os.path.join(save_path, filename)

            # 保存文件
            with open(full_path, 'wb') as f:
                f.write(audio_data)

            file_size_mb = len(audio_data) / (1024 * 1024)
            logger.info(f"💾 音频已保存: {full_path} ({file_size_mb:.2f} MB)")

            return full_path

        except Exception as e:
            logger.error(f"❌ 保存音频文件失败: {str(e)}", exc_info=True)
            return None

    @staticmethod
    def detect_file_type_by_content(file_data: bytes) -> str:
        """
        通过文件头（Magic Number）检测文件类型

        Args:
            file_data: 文件二进制数据（至少前32字节）

        Returns:
            str: 'video', 'audio', 或 'unknown'
        """
        if len(file_data) < 12:
            return 'unknown'

        # 特殊处理 MP4/MOV 格式（检查 ftyp）
        if len(file_data) >= 12 and file_data[4:8] == b'ftyp':
            return 'video'

        # 特殊处理 RIFF 格式（AVI 和 WAV 都用 RIFF）
        if file_data.startswith(b'RIFF') and len(file_data) >= 12:
            if b'AVI ' in file_data[8:12]:
                return 'video'
            elif b'WAVE' in file_data[8:12]:
                return 'audio'

        # 检查其他视频签名
        for signature, format_name in AudioExtractor.VIDEO_SIGNATURES.items():
            if signature != b'RIFF' and file_data.startswith(signature):
                return 'video'

        # 检查音频签名
        for signature, format_name in AudioExtractor.AUDIO_SIGNATURES.items():
            if signature != b'RIFF' and file_data.startswith(signature):
                return 'audio'

        return 'unknown'

    @staticmethod
    def is_video_file(filename: str) -> bool:
        """
        判断文件是否为视频文件（基于扩展名）

        Args:
            filename: 文件名

        Returns:
            bool: 是否为视频文件
        """
        ext = os.path.splitext(filename.lower())[1]
        return ext in AudioExtractor.VIDEO_EXTENSIONS

    @staticmethod
    def is_audio_file(filename: str) -> bool:
        """
        判断文件是否为音频文件

        Args:
            filename: 文件名

        Returns:
            bool: 是否为音频文件
        """
        ext = os.path.splitext(filename.lower())[1]
        return ext in AudioExtractor.AUDIO_EXTENSIONS

    def extract_audio_from_video(
        self,
        video_data: bytes,
        output_format: str = 'wav'
    ) -> Tuple[Optional[bytes], Optional[str]]:
        """
        从视频数据中提取音频

        Args:
            video_data: 视频文件的二进制数据
            output_format: 输出音频格式 ('wav', 'mp3', 'flac')

        Returns:
            Tuple[Optional[bytes], Optional[str]]: (音频数据, 错误信息)
        """
        temp_video_path = None
        temp_audio_path = None

        try:
            # 创建临时视频文件
            temp_video_path = os.path.join(self.temp_dir, f"temp_video_{os.getpid()}.tmp")
            with open(temp_video_path, 'wb') as f:
                f.write(video_data)

            logger.info(f"📹 临时视频文件已保存: {temp_video_path} ({len(video_data) / (1024*1024):.2f} MB)")

            # 创建临时音频文件路径
            temp_audio_path = os.path.join(self.temp_dir, f"temp_audio_{os.getpid()}.{output_format}")

            # 使用 ffmpeg 提取音频
            audio_data, error = self._extract_with_ffmpeg(
                temp_video_path,
                temp_audio_path,
                output_format
            )

            return audio_data, error

        except Exception as e:
            error_msg = f"提取音频失败: {str(e)}"
            logger.error(f"❌ {error_msg}", exc_info=True)
            return None, error_msg

        finally:
            # 清理临时文件
            if temp_video_path and os.path.exists(temp_video_path):
                try:
                    os.remove(temp_video_path)
                    logger.debug(f"🧹 已删除临时视频文件: {temp_video_path}")
                except Exception as e:
                    logger.warning(f"⚠️  删除临时视频文件失败: {e}")

            if temp_audio_path and os.path.exists(temp_audio_path):
                try:
                    os.remove(temp_audio_path)
                    logger.debug(f"🧹 已删除临时音频文件: {temp_audio_path}")
                except Exception as e:
                    logger.warning(f"⚠️  删除临时音频文件失败: {e}")

    def _extract_with_ffmpeg(
        self,
        input_path: str,
        output_path: str,
        output_format: str
    ) -> Tuple[Optional[bytes], Optional[str]]:
        """
        使用 ffmpeg 提取音频

        Args:
            input_path: 输入视频文件路径
            output_path: 输出音频文件路径
            output_format: 输出格式

        Returns:
            Tuple[Optional[bytes], Optional[str]]: (音频数据, 错误信息)
        """
        try:
            # 根据输出格式构建 ffmpeg 命令
            if output_format == 'wav':
                # WAV: 16kHz, 单声道, 16-bit PCM (FunASR 最佳格式)
                cmd = [
                    'ffmpeg',
                    '-i', input_path,
                    '-vn',  # 不处理视频
                    '-acodec', 'pcm_s16le',  # 16-bit PCM
                    '-ar', '16000',  # 16kHz 采样率
                    '-ac', '1',  # 单声道
                    '-y',  # 覆盖输出文件
                    output_path
                ]
            elif output_format == 'mp3':
                # MP3: 16kHz, 单声道, 128kbps
                cmd = [
                    'ffmpeg',
                    '-i', input_path,
                    '-vn',
                    '-acodec', 'libmp3lame',
                    '-ar', '16000',
                    '-ac', '1',
                    '-b:a', '128k',
                    '-y',
                    output_path
                ]
            elif output_format == 'flac':
                # FLAC: 16kHz, 单声道, 无损压缩
                cmd = [
                    'ffmpeg',
                    '-i', input_path,
                    '-vn',
                    '-acodec', 'flac',
                    '-ar', '16000',
                    '-ac', '1',
                    '-y',
                    output_path
                ]
            else:
                return None, f"不支持的音频格式: {output_format}"

            logger.info(f"🎵 开始提取音频 (格式: {output_format})...")
            logger.debug(f"   命令: {' '.join(cmd)}")

            # 执行 ffmpeg 命令
            result = subprocess.run(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                timeout=300  # 5分钟超时
            )

            if result.returncode != 0:
                error_msg = result.stderr.decode('utf-8', errors='ignore')
                logger.error(f"❌ ffmpeg 执行失败: {error_msg}")
                return None, f"ffmpeg 执行失败: {error_msg[:200]}"

            # 读取提取的音频文件
            if not os.path.exists(output_path):
                return None, "音频文件未生成"

            with open(output_path, 'rb') as f:
                audio_data = f.read()

            audio_size_mb = len(audio_data) / (1024 * 1024)
            logger.info(f"✅ 音频提取成功: {audio_size_mb:.2f} MB ({output_format})")

            return audio_data, None

        except subprocess.TimeoutExpired:
            error_msg = "ffmpeg 执行超时 (>5分钟)"
            logger.error(f"❌ {error_msg}")
            return None, error_msg

        except Exception as e:
            error_msg = f"ffmpeg 执行异常: {str(e)}"
            logger.error(f"❌ {error_msg}", exc_info=True)
            return None, error_msg

    def convert_audio_format(
        self,
        audio_data: bytes,
        input_format: str,
        output_format: str = 'wav'
    ) -> Tuple[Optional[bytes], Optional[str]]:
        """
        转换音频格式为 FunASR 最佳格式

        Args:
            audio_data: 原始音频数据
            input_format: 输入格式 (如 'mp3', 'aac')
            output_format: 输出格式 (默认 'wav')

        Returns:
            Tuple[Optional[bytes], Optional[str]]: (转换后的音频数据, 错误信息)
        """
        temp_input_path = None
        temp_output_path = None

        try:
            # 创建临时输入文件
            temp_input_path = os.path.join(self.temp_dir, f"temp_input_{os.getpid()}.{input_format}")
            with open(temp_input_path, 'wb') as f:
                f.write(audio_data)

            # 创建临时输出文件路径
            temp_output_path = os.path.join(self.temp_dir, f"temp_output_{os.getpid()}.{output_format}")

            # 使用 ffmpeg 转换
            converted_data, error = self._extract_with_ffmpeg(
                temp_input_path,
                temp_output_path,
                output_format
            )

            return converted_data, error

        except Exception as e:
            error_msg = f"音频格式转换失败: {str(e)}"
            logger.error(f"❌ {error_msg}", exc_info=True)
            return None, error_msg

        finally:
            # 清理临时文件
            for path in [temp_input_path, temp_output_path]:
                if path and os.path.exists(path):
                    try:
                        os.remove(path)
                    except Exception as e:
                        logger.warning(f"⚠️  删除临时文件失败: {e}")
