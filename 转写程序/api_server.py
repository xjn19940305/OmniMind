from fastapi import FastAPI, File, UploadFile, HTTPException, Form, APIRouter
from fastapi.responses import JSONResponse
from fastapi.middleware.cors import CORSMiddleware
from typing import List, Optional, Dict, Any
import uvicorn
import os
import shutil
import tempfile
from datetime import datetime
from pydantic import BaseModel
from concurrent.futures import ThreadPoolExecutor
import uuid
import threading
import asyncio
from functools import partial

from paraformerdome import AutoSpeakerRecognitionSystem
from logger_config import get_logger

# 初始化日志
logger = get_logger("api")

# 创建 FastAPI 应用
app = FastAPI(
    title="语音识别与说话人区分 API",
    description="基于 FunASR 的语音识别和说话人识别服务",
    version="1.0.0",
    docs_url="/funasr/docs",
    redoc_url="/funasr/redoc",
    openapi_url="/funasr/openapi.json"
)

# 添加 CORS 中间件
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # 生产环境请设置具体的域名
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 创建路由器，添加 /funasr 前缀
router = APIRouter(prefix="/funasr")

# ============ 并发安全的模型管理 ============
# 全局共享的模型实例（只读，线程安全）
recognition_system: Optional[AutoSpeakerRecognitionSystem] = None

# 线程本地存储：每个线程独立的用户声纹数据
thread_local = threading.local()

def get_thread_recognition_system():
    """获取当前线程的识别系统副本"""
    if not hasattr(thread_local, 'recognition_system'):
        # 为当前线程创建独立的识别系统实例
        if recognition_system is None:
            raise RuntimeError("全局模型未初始化")
        # 浅拷贝：共享模型权重，但独立的 speaker_db
        thread_local.recognition_system = recognition_system
    return thread_local.recognition_system

# 配置
TEMP_AUDIO_DIR = "temp_audio"
os.makedirs(TEMP_AUDIO_DIR, exist_ok=True)

# ============ 异步处理配置 ============
# 创建线程池用于处理 CPU 密集型的语音识别任务
# max_workers 建议设置为 CPU 核心数的 1.5-2 倍
# 40核心服务器：60（保守）到 80（激进），建议从 60 开始
import configparser
config = configparser.ConfigParser()
config.read('config.ini', encoding='utf-8')
max_workers = config.getint('concurrency', 'max_concurrent_tasks', fallback=60)
executor = ThreadPoolExecutor(max_workers=max_workers, thread_name_prefix="asr_worker_")

# 存储异步任务的结果
# 格式: {task_id: {"status": "processing|completed|failed", "future": Future, "result": ..., "error": ...}}
task_results: Dict[str, Any] = {}


# 请求和响应模型
class RegisterSpeakerRequest(BaseModel):
    speaker_id: str
    speaker_name: str
    audio_paths: List[str]


class SpeakerInfo(BaseModel):
    speaker_id: str
    speaker_name: str
    color: str
    registration_time: str
    audio_count: int
    is_auto_registered: bool


class RecognitionResponse(BaseModel):
    successful: bool
    message: str
    data: Optional[dict] = None


@app.on_event("startup")
async def startup_event():
    """应用启动时初始化模型"""
    global recognition_system
    try:
        logger.info("=" * 60)
        logger.info("正在启动语音识别服务...")
        logger.info(f"📊 线程池配置: max_workers={max_workers}")
        logger.info("正在加载模型，这可能需要几分钟时间...")
        logger.info("=" * 60)
        recognition_system = AutoSpeakerRecognitionSystem()
        logger.info("=" * 60)
        logger.info("模型加载完成，服务已就绪！")
        logger.info(f"📊 并发处理能力: {max_workers} 个任务")
        logger.info("服务地址: http://localhost:8000")
        logger.info("API 文档: http://localhost:8000/funasr/docs")
        logger.info("=" * 60)
    except Exception as e:
        logger.error(f"错误: 模型加载失败: {e}")
        import traceback
        traceback.print_exc()
        raise e


@app.on_event("shutdown")
async def shutdown_event():
    """应用关闭时清理资源"""
    logger.info("正在关闭服务...")
    # 清理临时文件
    if os.path.exists(TEMP_AUDIO_DIR):
        shutil.rmtree(TEMP_AUDIO_DIR)
    logger.info("服务已关闭")


@router.get("/")
async def root():
    """根路径，返回服务信息"""
    return {
        "service": "语音识别与说话人区分 API",
        "version": "1.0.0",
        "status": "running",
        "endpoints": {
            "health": "/funasr/health",
            "recognize": "/funasr/api/recognize",
            "recognize_url": "/funasr/api/recognize-url",
            "speakers": "/funasr/api/speakers",
            "register_speaker": "/funasr/api/register-speaker",
            "docs": "/funasr/docs",
            "redoc": "/funasr/redoc"
        }
    }


# ============ 线程池执行的转录函数 ============
def process_transcription_in_thread(
    temp_file_path: str,
    user_id: str,
    threshold: float,
    hotWord: str,
    language: str,
    separationSpeaker: bool,
    filename: str
) -> dict:
    """
    在线程池中执行的转录函数（线程安全）
    每个线程有独立的 speaker_db，不会互相干扰
    """
    try:
        # separationSpeaker 已经是布尔值，直接使用
        
        # 获取当前线程的识别系统实例
        thread_recognition_system = get_thread_recognition_system()

        # 如果不需要说话人区分，直接返回 ASR 结果
        if not separationSpeaker:
            logger.info(f"[线程 {threading.current_thread().name}] 不进行说话人区分")
            asr_result = thread_recognition_system.asr_model_asr_only.generate(
                input=temp_file_path,
                cache={},
                language=language,
            )
            return {
                "successful": True,
                "task": filename,
                "text": asr_result.get("text", ""),
                "duration_ms": asr_result.get("duration_ms", 0),
                "duration": round(asr_result.get("duration_ms", 0) / 1000, 2),
            }

        # 加载当前用户的声纹数据（线程独立）
        thread_recognition_system.speaker_db = {}
        thread_recognition_system.load_speaker_db(user_id)
        logger.info(f"[线程 {threading.current_thread().name}] 已加载用户 {user_id} 的声纹数据")

        # 进行识别
        result = thread_recognition_system.diarize_with_auto_registration(
            temp_file_path,
            threshold=threshold,
            hotWord=hotWord,
            language=language,
            separationSpeaker=separationSpeaker,
            enable_speaker_verification=False
        )

        if not result.get("success", False):
            error_message = result.get("message", "识别失败")
            error_data = result.get("data", {})
            logger.warning(f"识别失败: {error_message}")
            return {
                "successful": False,
                "message": error_message,
                "data": error_data,
                "error_code": error_data.get("error_code", "UNKNOWN_ERROR")
            }

        data = result.get("data", {})
        
        # 构建返回结果
        response_data = {
            "successful": True,
            "task": filename,
            "text": data.get("text", ""),  # ✅ 直接获取 text
            "segments": data.get("segments", []),
            "duration_ms": data.get("duration_ms", 0),
            "duration": round(data.get("duration_ms", 0) / 1000, 2),
            "speakers": data.get("speakers", {})
        }
        
        # 记录识别结果（包含 segments）
        logger.info(f"[线程 {threading.current_thread().name}] 识别完成: {filename}")
        logger.info(f"  说话人数: {len(response_data['speakers'])}")
        logger.info(f"  片段数: {len(response_data['segments'])}")
        logger.info(f"  文本长度: {len(response_data['text'])} 字符")
        
        # 记录每个 segment 的详细信息
        for i, seg in enumerate(response_data['segments'][:5], 1):  # 只记录前5个
            speaker_name = seg.get('speaker_name', seg.get('speaker', '未知'))
            text = seg.get('text', '')[:50]  # 只显示前50个字符
            start = seg.get('start_time_ms', seg.get('start', 0))
            end = seg.get('end_time_ms', seg.get('end', 0))
            logger.info(f"  Segment {i}: [{speaker_name}] {start}-{end}ms | {text}...")
        
        if len(response_data['segments']) > 5:
            logger.info(f"  ... 还有 {len(response_data['segments']) - 5} 个片段")
        
        # 单独输出完整的 segments 数据结构
        import json
        logger.info(f"[完整Segments数据] {json.dumps(response_data['segments'], ensure_ascii=False)}")
        
        return response_data

    except IndexError as e:
        # FunASR VAD 模型的索引错误，通常是音频太短或格式问题
        error_msg = "音频文件处理失败，可能是文件太短、格式异常或采样率不支持。建议：1) 确保音频时长 > 1秒；2) 使用标准格式(wav/mp3)；3) 采样率为 16000Hz"
        logger.error(f"[线程 {threading.current_thread().name}] VAD 索引错误: {str(e)}")
        logger.error(f"音频文件: {temp_file_path}")
        return {
            "successful": False,
            "message": error_msg,
            "data": None,
            "error_type": "VAD_INDEX_ERROR"
        }
    except Exception as e:
        logger.error(f"[线程 {threading.current_thread().name}] 转录失败: {str(e)}", exc_info=True)
        return {
            "successful": False,
            "message": f"转录失败: {str(e)}",
            "data": None
        }


@router.get("/health")
async def health_check():
    """健康检查接口"""
    if recognition_system is None:
        raise HTTPException(status_code=503, detail="模型未加载")
    
    return {
        "status": "healthy",
        "model_loaded": True,
        "timestamp": datetime.now().isoformat(),
        "speakers_count": len(recognition_system.speaker_db)
    }


@router.post("/audio/transcriptions")
async def recognize_audio(
    file: UploadFile = File(...),
    user_id: str = Form(""),
    threshold: float = Form(0.6),
    hotWord: str = Form(""),
    language: str = Form("auto"),
    separationSpeaker: str = Form("true"),  # ✅ 改为字符串接收
    auto_register: str = Form("true"),      # ✅ 改为字符串接收
):
    """
    识别上传的音频文件
    
    参数:
    - file: 音频文件 (支持 wav, mp3 等格式)
    - threshold: 声纹识别阈值 (0-1 之间，默认 0.6)
    - auto_register: 是否自动注册未知说话人 (默认 True)
    
    返回:
    - 包含语音识别结果和说话人区分信息
    """
    
    # 转换字符串参数为布尔值
    separation_speaker_bool = separationSpeaker.lower() in ('true', '1', 'yes')
    auto_register_bool = auto_register.lower() in ('true', '1', 'yes')
    
    logger.info(f"用户ID: {user_id}")
    logger.info(f"hotWord: {hotWord}")
    logger.info(f"separationSpeaker: {separationSpeaker} -> {separation_speaker_bool}")
    logger.info(f"auto_register: {auto_register} -> {auto_register_bool}")
    
    if recognition_system is None:
        raise HTTPException(status_code=503, detail="模型未加载")
    
    # 保存上传的文件
    temp_file_path = None
    try:
        
        logger.info(f"文件名称: {file.filename}")
        # 创建临时文件
        suffix = os.path.splitext(file.filename)[1]
        temp_file = tempfile.NamedTemporaryFile(
            delete=False, 
            suffix=suffix, 
            dir=TEMP_AUDIO_DIR
        )
        temp_file_path = temp_file.name
        
        # 保存上传的文件
        with temp_file as f:
            content = await file.read()
            f.write(content)
        
        logger.info(f"已保存临时文件: {temp_file_path}")

        # ✅ 使用线程池执行转录任务（并发安全）
        loop = asyncio.get_event_loop()
        result = await loop.run_in_executor(
            executor,
            process_transcription_in_thread,
            temp_file_path,
            user_id,
            threshold,
            hotWord,
            language,
            separation_speaker_bool,
            file.filename
        )

        return result

    except Exception as e:
        logger.error(f"错误: 识别失败: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"识别失败: {str(e)}")
    
    finally:
        # 清理临时文件
        if temp_file_path and os.path.exists(temp_file_path):
            try:
                os.remove(temp_file_path)
                logger.debug(f"已删除临时文件: {temp_file_path}")
            except Exception as e:
                logger.warning(f"警告: 删除临时文件失败: {e}")


@router.post("/audio/transcriptions-url")
async def recognize_audio_url(
    audio_url: str = Form(...),
    threshold: float = Form(0.6)
):
    """
    识别指定 URL 或本地路径的音频文件
    
    参数:
    - audio_url: 音频文件的 URL 或本地路径
    - threshold: 声纹识别阈值 (0-1 之间，默认 0.6)
    
    返回:
    - 包含语音识别结果和说话人区分信息
    """

    if recognition_system is None:
        raise HTTPException(status_code=503, detail="模型未加载")
    
    try:
        recognition_system.test()
        # print(f"开始识别音频: {audio_url}")
        
        # # 检查本地文件是否存在
        # if not audio_url.startswith("http") and not os.path.exists(audio_url):
        #     raise HTTPException(status_code=404, detail=f"音频文件不存在: {audio_url}")
        
        # # 进行识别
        # result = recognition_system.diarize_with_auto_registration(
        #     audio_url, 
        #     threshold=threshold,
        #     enable_speaker_verification=False  # 禁用声纹识别，仅使用FunASR的说话人ID
        # )
        
        # # 检查识别是否成功
        # if not result.get("success", False):
        #     return RecognitionResponse(
        #         success=False,
        #         message=result.get("message", "识别失败"),
        #         data=None
        #     )
        
        # # 获取识别数据
        # data = result.get("data", {})
        
        # # 构建返回结果
        # response_data = {
        #     "audio_url": audio_url,
        #     "threshold": threshold,
        #     "original_text": data.get("original_text", ""),
        #     "duration_ms": data.get("duration_ms", 0),
        #     "duration_seconds": round(data.get("duration_ms", 0) / 1000, 2),
        #     "speaker_count": data.get("speaker_count", 0),
        #     "speakers": data.get("speakers", {}),
        #     "segments": data.get("segments", []),
        #     "new_speakers_registered": data.get("new_speakers_registered", 0),
        #     "total_speakers_in_db": len(recognition_system.speaker_db)
        # }
        
        # print(f"识别完成: {response_data['speaker_count']} 个说话人, {len(response_data['segments'])} 个片段")
        
        # return RecognitionResponse(
        #     success=True,
        #     message="识别成功",
        #     data=response_data
        # )
        
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"错误: 识别失败: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"识别失败: {str(e)}")


@router.get("/api/speakers")
async def list_speakers():
    """
    获取所有已注册的说话人列表
    
    返回:
    - 所有说话人的信息列表
    """
    if recognition_system is None:
        raise HTTPException(status_code=503, detail="模型未加载")
    
    try:
        speakers = []
        for spk_id, spk_data in recognition_system.speaker_db.items():
            speakers.append({
                "speaker_id": spk_id,
                "speaker_name": spk_data["name"],
                "color": spk_data["color"],
                "registration_time": spk_data["registration_time"],
                "audio_count": spk_data["audio_count"],
                "is_auto_registered": spk_data.get("is_auto_registered", False),
                "original_spk_id": spk_data.get("original_spk_id", ""),
                "detection_confidence": spk_data.get("detection_confidence", 0.0)
            })
        
        return {
            "success": True,
            "message": "获取成功",
            "data": {
                "total_count": len(speakers),
                "manual_count": len([s for s in speakers if not s["is_auto_registered"]]),
                "auto_count": len([s for s in speakers if s["is_auto_registered"]]),
                "speakers": speakers
            }
        }
    except Exception as e:
        logger.error(f"错误: 获取说话人列表失败: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"获取失败: {str(e)}")


@router.post("/api/register-speaker")
async def register_speaker(
    user_id: str = Form(...),
    speaker_id: str = Form(...),
    speaker_name: str = Form(...),
    files: List[UploadFile] = File(...)
):
    """
    为指定用户批量注册说话人的声纹
    
    参数:
    - user_id: 注册者ID
    - speaker_id: 说话人ID (唯一标识)
    - speaker_name: 说话人姓名
    - files: 说话人的音频样本文件 (至少1个)
    
    返回:
    - 每个 speaker_id 的注册结果
    """
    if recognition_system is None:
        return RecognitionResponse(
            successful=False,
            message="模型未加载",
            data=None
        )
    
    if not files:
        return RecognitionResponse(
            successful=False,
            message="至少需要上传一个音频文件",
            data=None
        )
    
    user_dir = os.path.join(recognition_system.workspace_dir, "users", user_id)
    os.makedirs(user_dir, exist_ok=True)
    

    # 按 speaker_id 收集临时文件路径
    speaker_files = {}

    saved_temp_paths = []  # 便于出错时清理
    try:
        for upload in files:
            # orig_name = upload.filename or ""
            # if "__" not in orig_name:
            #     raise HTTPException(status_code=400, detail=f"文件名格式错误: {orig_name}。应为 <speaker_id>__<name>.wav")
            # spk_id, _rest = orig_name.split("__", 1)
            spk_dir = os.path.join(user_dir, speaker_id)
            os.makedirs(spk_dir, exist_ok=True)

            suffix = os.path.splitext(upload.filename)[1] or ".wav"
            temp_file = tempfile.NamedTemporaryFile(delete=False, suffix=suffix, dir=spk_dir)
            with temp_file as f:
                content = await upload.read()
                f.write(content)
            saved_temp_paths.append(temp_file.name)
            speaker_files.setdefault(speaker_id, []).append(temp_file.name)
            logger.info(f"已保存样本: user={user_id} speaker={speaker_id} file={temp_file.name}")

        results = {}

        # 对每个说话人调用 register_speaker
        for speaker_id, paths in speaker_files.items():
            # 在系统中使用带用户前缀的 speaker_id 避免冲突
            reg_id = f"{user_id}__{speaker_id}"
            try:
                ok = recognition_system.register_speaker(paths, user_id=user_id, speaker_id=reg_id, speaker_name=speaker_name)
                results[speaker_id] = {"registered_id": reg_id, "success": bool(ok), "audio_count": len(paths)}
            except Exception as e:
                logger.error(f"错误: 注册失败: {e}", exc_info=True)
                return RecognitionResponse(
                    successful=False,
                    message=f"注册说话人失败:  user={user_id}, {str(e)}",
                    data=None
                )
        return RecognitionResponse(
            successful=True,
            message="",
            data=None
        )

    except Exception as e:
        logger.error(f"错误: 注册失败: {e}", exc_info=True)
        return RecognitionResponse(
            successful=False,
            message=str(e),
            data=None
        )
    finally:
        # 不在此删除用户存储的样本；仅在临时保存失败时尝试清理已写入的临时文件
        # （注册成功的文件保留在 workspace 中）
        # 清理临时文件
        for temp_file in saved_temp_paths:
            if os.path.exists(temp_file):
                try:
                    os.remove(temp_file)
                except Exception as e:
                    logger.warning(f"警告: 删除临时文件失败: {e}")



# ============ 异步识别接口 ============

@router.post("/audio/transcriptions-async")
async def recognize_audio_async(
    file: UploadFile = File(...),
    user_id: str = Form(""),
    threshold: float = Form(0.6),
    hotWord: str = Form(""),
    language: str = Form("auto"),
    separationSpeaker: bool = Form(True),
    auto_register: bool = Form(True),
):
    """
    异步识别上传的音频文件（立即返回任务 ID）

    优点：
    - 立即返回响应，不阻塞其他请求
    - 使用线程池在后台处理 CPU 密集型操作
    - 客户端可以轮询查询结果

    参数:
    - file: 音频文件 (支持 wav, mp3 等格式)
    - user_id: 用户 ID
    - threshold: 声纹识别阈值 (0-1 之间，默认 0.6)
    - hotWord: 热词
    - language: 语言
    - separationSpeaker: 是否进行说话人区分
    - auto_register: 是否自动注册未知说话人

    返回:
    - task_id: 任务 ID，用于查询结果
    - status: 任务状态 (processing)
    """

    if recognition_system is None:
        raise HTTPException(status_code=503, detail="模型未加载")

    temp_file_path = None
    try:
        suffix = os.path.splitext(file.filename)[1]
        temp_file = tempfile.NamedTemporaryFile(
            delete=False,
            suffix=suffix,
            dir=TEMP_AUDIO_DIR
        )
        temp_file_path = temp_file.name

        with temp_file as f:
            content = await file.read()
            f.write(content)

        logger.info(f"已保存临时文件: {temp_file_path}")

        # 定义识别任务函数
        def recognition_task():
            try:
                if not separationSpeaker:
                    logger.info("不进行说话人区分，直接返回ASR结果")
                    asr_result = recognition_system.asr_model_asr_only.generate(
                        input=temp_file_path,
                        cache={},
                        language=language,
                    )
                    return {
                        "successful": True,
                        "task": file.filename,
                        "text": asr_result.get("text", ""),
                        "duration_ms": asr_result.get("duration_ms", 0),
                        "duration": round(asr_result.get("duration_ms", 0) / 1000, 2),
                    }

                # 加载当前用户的所有声纹
                recognition_system.speaker_db = {}
                recognition_system.load_speaker_db(user_id)

                # 进行识别
                logger.info(f"开始识别音频: {file.filename}")
                result = recognition_system.diarize_with_auto_registration(
                    temp_file_path,
                    threshold=threshold,
                    hotWord=hotWord,
                    language=language,
                    separationSpeaker=separationSpeaker,
                    enable_speaker_verification=False
                )

                return {
                    "successful": True,
                    "task": file.filename,
                    "result": result
                }
            finally:
                # 清理临时文件
                if temp_file_path and os.path.exists(temp_file_path):
                    try:
                        os.remove(temp_file_path)
                        logger.debug(f"已删除临时文件: {temp_file_path}")
                    except Exception as e:
                        logger.warning(f"警告: 删除临时文件失败: {e}")

        # 生成任务 ID
        task_id = str(uuid.uuid4())

        # 提交到线程池
        future = executor.submit(recognition_task)
        task_results[task_id] = {
            "status": "processing",
            "future": future,
            "filename": file.filename,
            "created_at": datetime.now().isoformat()
        }

        logger.info(f"任务已提交: {task_id}")

        return {
            "successful": True,
            "task_id": task_id,
            "status": "processing",
            "message": "任务已提交到后台处理，请使用 task_id 查询结果"
        }

    except Exception as e:
        logger.error(f"错误: {str(e)}", exc_info=True)
        if temp_file_path and os.path.exists(temp_file_path):
            try:
                os.remove(temp_file_path)
            except Exception as e:
                logger.warning(f"警告: 删除临时文件失败: {e}")
        raise HTTPException(status_code=500, detail=f"提交任务失败: {str(e)}")


@router.get("/audio/transcriptions-status/{task_id}")
async def get_transcription_status(task_id: str):
    """
    查询异步识别任务的状态

    参数:
    - task_id: 任务 ID

    返回:
    - status: 任务状态 (processing/completed/failed)
    - result: 识别结果（仅当 status=completed 时）
    - error: 错误信息（仅当 status=failed 时）
    """

    if task_id not in task_results:
        raise HTTPException(status_code=404, detail=f"任务不存在: {task_id}")

    task_info = task_results[task_id]
    future = task_info["future"]

    if future.done():
        try:
            result = future.result()
            task_info["status"] = "completed"
            task_info["result"] = result
            task_info["completed_at"] = datetime.now().isoformat()

            logger.info(f"任务完成: {task_id}")

            return {
                "task_id": task_id,
                "status": "completed",
                "result": result,
                "filename": task_info["filename"]
            }
        except Exception as e:
            task_info["status"] = "failed"
            task_info["error"] = str(e)
            task_info["completed_at"] = datetime.now().isoformat()

            logger.error(f"任务失败: {task_id}, 错误: {str(e)}")

            return {
                "task_id": task_id,
                "status": "failed",
                "error": str(e),
                "filename": task_info["filename"]
            }
    else:
        return {
            "task_id": task_id,
            "status": "processing",
            "message": "任务正在处理中...",
            "filename": task_info["filename"],
            "created_at": task_info["created_at"]
        }


@router.get("/audio/transcriptions-cleanup/{task_id}")
async def cleanup_task(task_id: str):
    """
    清理已完成的任务结果（释放内存）

    参数:
    - task_id: 任务 ID

    返回:
    - 清理结果
    """

    if task_id not in task_results:
        raise HTTPException(status_code=404, detail=f"任务不存在: {task_id}")

    task_info = task_results[task_id]

    if task_info["status"] == "processing":
        raise HTTPException(status_code=400, detail="任务仍在处理中，无法清理")

    del task_results[task_id]
    logger.info(f"任务已清理: {task_id}")

    return {
        "successful": True,
        "message": f"任务已清理: {task_id}"
    }


@router.delete("/api/speakers/{speaker_id}")
async def delete_speaker(speaker_id: str):
    """
    删除指定的说话人
    
    参数:
    - speaker_id: 说话人ID
    
    返回:
    - 删除结果
    """
    if recognition_system is None:
        raise HTTPException(status_code=503, detail="模型未加载")
    
    try:
        if speaker_id not in recognition_system.speaker_db:
            raise HTTPException(status_code=404, detail=f"说话人不存在: {speaker_id}")
        
        speaker_name = recognition_system.speaker_db[speaker_id]["name"]
        del recognition_system.speaker_db[speaker_id]
        recognition_system.save_speaker_db()
        
        return {
            "success": True,
            "message": f"已删除说话人: {speaker_name} ({speaker_id})"
        }
    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"错误: 删除说话人失败: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"删除失败: {str(e)}")


# 将路由器包含到应用中
app.include_router(router)


if __name__ == "__main__":
    # 启动服务
    uvicorn.run(
        "api_server:app",
        host="0.0.0.0",
        port=8000,
        reload=False,  # 生产环境设置为 False
        workers=1,  # 因为模型加载占用大量内存，建议只用1个worker
        timeout_keep_alive=7200,  # Keep-alive 超时 2 小时（7200秒，防止长时间转录时连接断开）
        timeout_graceful_shutdown=300  # 优雅关闭超时 5 分钟
    )

