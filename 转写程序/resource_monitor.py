#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
资源监控模块 - 监控 CPU、内存使用情况
"""

import psutil
import logging
from typing import Dict, Any

logger = logging.getLogger(__name__)


class ResourceMonitor:
    """系统资源监控器"""

    def __init__(self, cpu_threshold: float = 80.0, memory_threshold: float = 85.0):
        """
        初始化资源监控器

        Args:
            cpu_threshold: CPU 使用率阈值（百分比）
            memory_threshold: 内存使用率阈值（百分比）
        """
        self.cpu_threshold = cpu_threshold
        self.memory_threshold = memory_threshold

    def get_system_stats(self) -> Dict[str, Any]:
        """
        获取系统资源统计信息

        Returns:
            包含 CPU、内存、磁盘使用情况的字典
        """
        try:
            # CPU 使用率（1秒采样）
            cpu_percent = psutil.cpu_percent(interval=1)

            # 内存使用情况
            memory = psutil.virtual_memory()
            memory_percent = memory.percent

            # 磁盘使用情况
            disk = psutil.disk_usage('/')
            disk_percent = disk.percent

            return {
                'cpu_percent': cpu_percent,
                'memory_percent': memory_percent,
                'memory_available_gb': memory.available / (1024 ** 3),
                'disk_percent': disk_percent,
                'disk_free_gb': disk.free / (1024 ** 3)
            }
        except Exception as e:
            logger.error(f"获取系统资源信息失败: {str(e)}")
            return {}

    def is_overloaded(self) -> tuple[bool, str]:
        """
        检查系统是否过载

        Returns:
            (是否过载, 原因描述)
        """
        stats = self.get_system_stats()

        if not stats:
            return False, ""

        cpu_percent = stats.get('cpu_percent', 0)
        memory_percent = stats.get('memory_percent', 0)

        # 检查 CPU
        if cpu_percent > self.cpu_threshold:
            reason = f"CPU 使用率过高: {cpu_percent:.1f}% (阈值: {self.cpu_threshold}%)"
            return True, reason

        # 检查内存
        if memory_percent > self.memory_threshold:
            reason = f"内存使用率过高: {memory_percent:.1f}% (阈值: {self.memory_threshold}%)"
            return True, reason

        return False, ""

    def log_stats(self):
        """记录当前系统资源状态"""
        stats = self.get_system_stats()

        if stats:
            logger.info(
                f"📊 系统资源: "
                f"CPU={stats['cpu_percent']:.1f}%, "
                f"内存={stats['memory_percent']:.1f}% "
                f"(可用: {stats['memory_available_gb']:.2f}GB), "
                f"磁盘={stats['disk_percent']:.1f}% "
                f"(剩余: {stats['disk_free_gb']:.2f}GB)"
            )
