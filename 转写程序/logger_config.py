"""
日志配置模块
"""
import logging
import os
from logging.handlers import RotatingFileHandler
from datetime import datetime

def setup_logger(name="funasr", log_dir="logs", log_level=logging.INFO):
    """
    配置日志系统
    
    Args:
        name: 日志记录器名称
        log_dir: 日志目录
        log_level: 日志级别
    
    Returns:
        logger: 配置好的日志记录器
    """
    # 创建日志目录
    if not os.path.exists(log_dir):
        os.makedirs(log_dir)
    
    # 创建日志记录器
    logger = logging.getLogger(name)
    logger.setLevel(log_level)
    
    # 避免重复添加处理器
    if logger.handlers:
        return logger
    
    # 日志格式
    formatter = logging.Formatter(
        '%(asctime)s | %(levelname)-8s | %(name)s | %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    
    # 控制台处理器
    console_handler = logging.StreamHandler()
    console_handler.setLevel(log_level)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)
    
    # 文件处理器 - 所有日志
    log_file = os.path.join(log_dir, f"funasr_{datetime.now().strftime('%Y%m%d')}.log")
    file_handler = RotatingFileHandler(
        log_file,
        maxBytes=50*1024*1024,  # 50MB
        backupCount=10,
        encoding='utf-8'
    )
    file_handler.setLevel(log_level)
    file_handler.setFormatter(formatter)
    logger.addHandler(file_handler)
    
    # 错误日志文件处理器
    error_log_file = os.path.join(log_dir, f"funasr_error_{datetime.now().strftime('%Y%m%d')}.log")
    error_handler = RotatingFileHandler(
        error_log_file,
        maxBytes=50*1024*1024,  # 50MB
        backupCount=10,
        encoding='utf-8'
    )
    error_handler.setLevel(logging.ERROR)
    error_handler.setFormatter(formatter)
    logger.addHandler(error_handler)
    
    return logger

# 创建全局日志记录器
logger = setup_logger()

def get_logger(name=None):
    """
    获取日志记录器
    
    Args:
        name: 子日志记录器名称
    
    Returns:
        logger: 日志记录器
    """
    if name:
        return logging.getLogger(f"funasr.{name}")
    return logger

