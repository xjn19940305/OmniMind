from funasr import AutoModel
import torch
import numpy as np
import json
import os
import wave
import contextlib
from typing import Dict, List, Tuple, Optional
from datetime import datetime
import shutil
from logger_config import get_logger

# 初始化日志
logger = get_logger("asr")

class AutoSpeakerRecognitionSystem:
    def __init__(self, workspace_dir="speaker_workspace"):
        # 创建工作目录
        self.workspace_dir = workspace_dir
        self.unknown_speakers_dir = os.path.join(workspace_dir, "unknown_speakers")
        os.makedirs(workspace_dir, exist_ok=True)
        os.makedirs(self.unknown_speakers_dir, exist_ok=True)
        
        # 检查 GPU 可用性
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        logger.info(f"使用设备: {self.device}")
        
        model_dir = "iic/speech_seaco_paraformer_large_asr_nat-zh-cn-16k-common-vocab8404-pytorch"

        # 初始化模型
        logger.info("初始化语音识别模型...")
        self.asr_model = AutoModel(
            # model="iic/speech_paraformer-large-vad-punc_asr_nat-zh-cn-16k-common-vocab8404-pytorch", # paraformer-zh
            # model="iic/speech_seaco_paraformer_large_asr_nat-zh-cn-16k-common-vocab8404-pytorch", # paraformer-zh
            model=model_dir,
            # model_revision="v2.0.4",
            vad_model="iic/speech_fsmn_vad_zh-cn-16k-common-pytorch", # fsmn-vad
            vad_model_revision="v2.0.4",
            # punc_model="ct-punc-c", 
            punc_model="iic/punc_ct-transformer_zh-cn-common-vocab272727-pytorch", # iic/punc_ct-transformer_cn-en-common-vocab471067-large
            punc_model_revision="v2.0.4",
            spk_model="iic/speech_campplus_sv_zh-cn_16k-common",
            spk_model_revision="v2.0.2",
            # lm_model='iic/speech_transformer_lm_zh-cn-common-vocab8404-pytorch',
            # batch_size_s=600,  # 批处理大小(秒)
            # threshold=0.65  # 声纹相似度阈值
            # spk_model="iic/speech_campplus_speaker-diarization_common",
            # spk_model_revision="v1.0.4",
            # disable_pbar=True,
            device=self.device,  # 关键：指定设备
            # dtype="float16" if self.device == "cuda" else "float32",  # GPU 可用半精度
            disable_update=True
        )

        self.asr_model_asr_only= AutoModel(
            # model="iic/speech_paraformer-large-vad-punc_asr_nat-zh-cn-16k-common-vocab8404-pytorch", # paraformer-zh
            # model="iic/speech_seaco_paraformer_large_asr_nat-zh-cn-16k-common-vocab8404-pytorch", # paraformer-zh
            model=model_dir,
            # model_revision="v2.0.4",
            vad_model="iic/speech_fsmn_vad_zh-cn-16k-common-pytorch", # fsmn-vad
            vad_model_revision="v2.0.4",
            # punc_model="ct-punc-c", 
            punc_model="iic/punc_ct-transformer_zh-cn-common-vocab272727-pytorch", # iic/punc_ct-transformer_cn-en-common-vocab471067-large
            punc_model_revision="v2.0.4",
            # lm_model='iic/speech_transformer_lm_zh-cn-common-vocab8404-pytorch',
            # batch_size_s=600,  # 批处理大小(秒)
            # threshold=0.65  # 声纹相似度阈值
            # spk_model="iic/speech_campplus_speaker-diarization_common",
            # spk_model_revision="v1.0.4",
            # disable_pbar=True,
            device=self.device,  # 关键：指定设备
            # dtype="float16" if self.device == "cuda" else "float32",  # GPU 可用半精度
            disable_update=True
        )

        logger.info("初始化声纹模型...")
        self.sv_model = AutoModel(
            model="iic/speech_campplus_sv_zh-cn_16k-common",  # 关键：指定设备
            device=self.device,  # 关键：指定设备
            disable_update=True
        )

        # 声纹数据库
        self.speaker_db = {}
        self.speaker_colors = ["🔵", "🔴", "🟢", "🟡", "🟣", "🟠", "⚫", "⚪"]
        
        
        logger.info(f"系统初始化完成，已加载 {len(self.speaker_db)} 个说话人")

    def load_speaker_model(self):
        """加载说话人识别模型，带降级策略"""
        models_to_try = [
            {
                "model": "iic/speech_eres2netv2_sv_zh-cn_16k-common",
                "model_revision": "v1.0.5",
                "hub": "ms",
            },
            {
                "model": "iic/speech_eres2net_sv_zh-cn_16k-common",
                "model_revision": "v1.0.4",
            },
            {
                "model": "cam++",
                "model_revision": "v2.0.2",
            },
        ]

        for model_config in models_to_try:
            try:
                print(f"尝试加载模型: {model_config['model']}")
                return AutoModel(**model_config)
            except Exception as e:
                print(f"加载失败: {e}")
                continue

        raise RuntimeError("所有说话人模型加载失败")
    def extract_audio_segment(self, audio_path: str, start_time: float, end_time: float, output_path: str) -> bool:
        """
        从音频中提取指定时间段的片段（毫秒）
        """
        try:
            import soundfile as sf
            import numpy as np
            
            # 读取音频文件
            data, samplerate = sf.read(audio_path)
            
            # 将毫秒转换为采样点
            start_sample = int(start_time * samplerate / 1000)
            end_sample = int(end_time * samplerate / 1000)
            
            # 提取片段
            segment = data[start_sample:end_sample]
            
            # 保存片段
            sf.write(output_path, segment, samplerate)
            return True
        except Exception as e:
            logger.error(f"提取音频片段失败: {e}")
            return False
    
    def register_speaker(self, audio_paths: List[str], user_id: str, speaker_id: str, speaker_name: str) -> bool:
        """注册说话人声纹"""
        self.speaker_db = {}
        if len(audio_paths) == 0:
            logger.warning("需要至少一个音频文件来注册说话人")
            return False
        
        embeddings = []
        valid_files = 0
        
        for audio_path in audio_paths:
            if not os.path.exists(audio_path):
                logger.error(f"音频文件不存在: {audio_path}")
                continue
            
            try:
                res = self.sv_model.generate(input=audio_path)
                if res and len(res) > 0 and "spk_embedding" in res[0]:
                    embedding = res[0]["spk_embedding"]
                    embeddings.append(embedding)
                    valid_files += 1
                    logger.info(f"成功从 {os.path.basename(audio_path)} 提取声纹特征")
                else:
                    logger.warning(f"无法从 {audio_path} 提取声纹特征")
            except Exception as e:
                logger.error(f"处理音频 {audio_path} 时出错: {e}")
        
        if valid_files == 0:
            logger.error("无法从任何音频文件中提取声纹特征")
            return False
        
        # 计算平均声纹嵌入
        avg_embedding = torch.mean(torch.stack(embeddings), dim=0)
        
        # 分配颜色
        color_index = len(self.speaker_db) % len(self.speaker_colors)
        
        # 存储到数据库
        self.speaker_db[speaker_id] = {
            "name": speaker_name,
            "embedding": avg_embedding,
            "color": self.speaker_colors[color_index],
            "registration_time": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
            "audio_count": valid_files,
            "is_auto_registered": False  # 标记是否为自动注册
        }
        
        logger.info(f"成功注册说话人: {speaker_name} (ID: {speaker_id})")
        logger.info(f"   使用 {valid_files} 个音频文件，颜色: {self.speaker_colors[color_index]}")
        
        # 保存数据库
        self.save_speaker_db(user_id)
        return True
    
    def auto_register_unknown_speaker(self, audio_path: str, spk_id: str, similarity: float) -> str:
        """
        自动注册未知说话人
        :return: 分配的说话人姓名
        """
        # 生成新的说话人ID和姓名
        new_speaker_id = f"auto_{len([k for k in self.speaker_db if k.startswith('auto_')]) + 1:03d}"
        new_speaker_name = spk_id #f"未知说话人{new_speaker_id.replace('auto_', '')}"
        
        logger.info(f"检测到新说话人 {spk_id}，正在自动注册为 {new_speaker_name}...")
        
        try:
            # 提取声纹特征
            res = self.sv_model.generate(input=audio_path)
            
            logger.debug(f"提取声纹特征: {res}")
            if res and len(res) > 0 and "spk_embedding" in res[0]:
                embedding = res[0]["spk_embedding"]
                
                # 分配颜色
                color_index = len(self.speaker_db) % len(self.speaker_colors)
                
                # 存储到数据库
                self.speaker_db[new_speaker_id] = {
                    "name": new_speaker_name,
                    "embedding": embedding,
                    "color": self.speaker_colors[color_index],
                    "registration_time": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
                    "audio_count": 1,
                    "is_auto_registered": True,
                    "original_spk_id": spk_id,
                    "detection_confidence": similarity
                }
                
                # 保存未知说话人的音频样本
                sample_path = os.path.join(self.unknown_speakers_dir, f"{new_speaker_id}.wav")
                shutil.copy(audio_path, sample_path)
                
                logger.info(f"已自动注册新说话人: {new_speaker_name}")
                logger.info(f"   声纹样本已保存: {sample_path}")
                
                # 保存数据库
                self.save_speaker_db(spk_id)
                
                return new_speaker_name
            else:
                logger.warning("无法从音频提取声纹特征")
                return f"未知{spk_id}"
                
        except Exception as e:
            logger.error(f"自动注册说话人失败: {e}", exc_info=True)
            return f"未知{spk_id}"
    
    def identify_speaker(self, audio_path: str, threshold: float = 0.7) -> Tuple[Optional[str], float, str]:
        """
        识别说话人，如果未识别则自动注册
        :return: (speaker_id, confidence, speaker_name)
        """
        if not self.speaker_db:
            # 如果没有注册任何说话人，自动注册第一个
            return self.auto_register_unknown_speaker(audio_path, "spk0", 0.0), 0.0, "第一个说话人"
        
        try:
            # 提取声纹特征
            res = self.sv_model.generate(input=audio_path)
            if not res or len(res) == 0 or "spk_embedding" not in res[0]:
                return None, 0.0, "未知"
            
            query_embedding = res[0]["spk_embedding"]
            
            # 与数据库中的声纹对比
            best_match_id = None
            best_similarity = -1
            
            for speaker_id, speaker_data in self.speaker_db.items():
                db_embedding = speaker_data["embedding"]
                similarity = torch.cosine_similarity(
                    query_embedding.unsqueeze(0), 
                    db_embedding.unsqueeze(0)
                ).item()
                
                if similarity > best_similarity:
                    best_similarity = similarity
                    best_match_id = speaker_id
            
            # 判断是否达到阈值
            if best_similarity >= threshold:
                speaker_name = self.speaker_db[best_match_id]["name"]
                return best_match_id, best_similarity, speaker_name
            else:
                # 未达到阈值，自动注册新说话人
                new_speaker_name = self.auto_register_unknown_speaker(audio_path, f"spk{len(self.speaker_db)}", best_similarity)
                new_speaker_id = [k for k, v in self.speaker_db.items() if v["name"] == new_speaker_name][0]
                return new_speaker_id, best_similarity, new_speaker_name
            
        except Exception as e:
            logger.error(f"识别说话人时出错: {e}")
            return None, 0.0, "未知"
    
    def diarize_with_auto_registration(self, audio_url: str, threshold: float = 0.7, hotWord: str = "", language: str = "", separationSpeaker: bool = True, enable_speaker_verification: bool = False) -> Dict:
        """
        进行说话人区分并自动补充声纹库（优化版）
        返回完整的说话人信息、时间戳和转写文本
        
        参数:
            audio_url: 音频文件路径
            threshold: 声纹识别阈值
            enable_speaker_verification: 是否启用声纹识别（False=仅使用FunASR的说话人ID）
        """
        if enable_speaker_verification:
            logger.info("开始说话人区分（启用声纹识别）...")
        else:
            logger.info("开始说话人区分（仅使用FunASR说话人ID）...")
        
        logger.info("新文件")
        # 获取ASR结果
        # asr_result = self.asr_model.generate(
        #     input=audio_url, 
        #     vad_kwargs={
        #         "speech_noise_thres": 0.5,            # 🔴 更低的噪音阈值
        #         "min_silence_duration_ms": 300,       # 更长的静默判定
        #         "max_single_segment_time": 10000,     # 更长的单段时间
        #     },
        #     batch_size_s=300,
        #     beam_size=10,                              # 🔴 更小的波束（防重复，牺牲准确率）
        #     # threshold=0.55,                           # 更低的置信度
        #     # vad_kwargs={
        #     #     "speech_noise_thres": 0.5,           # ⬇️ 降低噪音阈值（更敏感）
        #     #     "min_silence_duration_ms": 500,      # ⬇️ 缩短最小静默时长（避免误分段）
        #     #     "max_single_segment_time": 20000,    # ⬆️ 增大单个片段时长（减少切割）
        #     #     "speech_pad_ms": 100                 # 🆕 添加语音填充（毫秒）
        #     # },
        #     # batch_size_s=600,
        #     # beam_size=10,                            # ⬇️ 降低波束宽度（减少重复）
        #     # beam_search_diversity_penalty=0.5,       # 🆕 添加多样性惩罚（避免重复）
        #     # ctc_weight=0.5,                          # 🆕 调整CTC权重
        #     # lm_weight=0.1,                           # 🆕 语言模型权重
        #     # threshold=0.6,                           # ⬇️ 降低阈值（提高检测灵敏度）
        #     # hotword='狡猾 20 兔子 10 童子军 15 愚蠢 15 狐狸 15 这样 10 ',  # 🆕 添加常见词
        #     # hotword='./data/dev/hotword.txt',
        # )
        
        asr_result = self.asr_model.generate(
            input=audio_url, 
            cache={},
            language=language,  # "zn", "en", "yue", "ja", "ko", "nospeech"
            use_itn=True,
            batch_size_s=600,
            merge_vad=True,  #
            merge_length_s=15,
            threshold=0.7,
            hotWord=hotWord
        )
        
        if not asr_result or len(asr_result) == 0:
            logger.info("未识别到任何语音内容")
            return {
                "success": False,
                "message": "未识别到任何语音内容",
                "data": None
            }
        
        result = asr_result[0]
        logger.info("ASR识别完成")
        logger.info(f"result: {result}")
        
        # ✅ 检查识别结果是否为空
        recognized_text = result.get("text", "")
        if not recognized_text or recognized_text.strip() == "":
            logger.warning("⚠️ 音频识别结果为空 - 可能原因: 1)音频太短(<1秒) 2)没有人声 3)音频质量问题 4)静音/噪音")
            return {
                "success": False,
                "message": "未识别到语音内容。可能原因：音频过短、没有人声、音频质量差或静音。",
                "data": {
                    "text": "",  # ✅ text 字段放在前面
                    "original_text": "",
                    "duration_ms": 0,
                    "speakers": {},
                    "segments": [],
                    "speaker_count": 0,
                    "error_code": "EMPTY_RECOGNITION"
                }
            }
        
        # 初始化返回结果
        final_result = {
            "success": True,
            "message": "识别成功",
            "data": {
                "text": recognized_text,  # ✅ 添加 text 字段
                "original_text": recognized_text,  # 保留兼容性
                "duration_ms": 0,  # 总时长（毫秒）
                "speakers": {},
                "segments": [],
                "speaker_count": 0,
                "new_speakers_registered": 0
            }
        }
        
        # 存储说话人ID映射（ASR检测的ID -> 真实说话人信息）
        speaker_mapping = {}
        
        # 第一步：处理每个唯一的说话人，进行声纹识别
        if "sentence_info" in result and len(result["sentence_info"]) > 0:
            unique_speakers = {}  # 存储每个说话人的音频片段信息
            
            # 收集每个说话人的所有片段
            for spk_info in result["sentence_info"]:
                spk_id = str(spk_info.get("spk", "unknown"))  # 统一转换为字符串
                start_time = int(spk_info.get("start", 0))  # 转换为 Python int
                end_time = int(spk_info.get("end", 0))  # 转换为 Python int
                
                if spk_id not in unique_speakers:
                    unique_speakers[spk_id] = []
                
                unique_speakers[spk_id].append({
                    "start": start_time,
                    "end": end_time,
                    "duration": end_time - start_time
                })
            
            logger.info(f"检测到 {len(unique_speakers)} 个不同的说话人")
            
            if enable_speaker_verification:
                # 对每个唯一说话人进行声纹识别
                for idx, (spk_id, segments) in enumerate(unique_speakers.items()):
                    # 选择最长的片段进行声纹识别
                    longest_segment = max(segments, key=lambda x: x["duration"])
                    
                    # 提取该片段的音频
                    segment_path = os.path.join(self.workspace_dir, f"temp_segment_{spk_id}.wav")
                    
                    if self.extract_audio_segment(
                        audio_url, 
                        longest_segment["start"], 
                        longest_segment["end"], 
                        segment_path
                    ):
                        # 进行声纹识别
                        logger.info(f"正在识别说话人 {spk_id}...")
                        identified_id, confidence, speaker_name = self.identify_speaker(segment_path, threshold)
                        
                        # 清理临时文件
                        try:
                            os.remove(segment_path)
                        except:
                            pass
                        
                        # 检查是否是新注册的说话人
                        is_new_speaker = identified_id and identified_id.startswith("auto_") and identified_id not in speaker_mapping.values()
                        
                        if identified_id:
                            speaker_data = self.speaker_db[identified_id]
                            speaker_info = {
                                "real_id": identified_id,
                                "real_name": speaker_name,
                                "color": speaker_data["color"],
                                "confidence": float(confidence),
                                "is_auto_registered": bool(speaker_data.get("is_auto_registered", False)),
                                "segment_count": int(len(segments)),
                                "total_duration_ms": int(sum(s["duration"] for s in segments))
                            }
                            
                            speaker_mapping[spk_id] = identified_id
                            final_result["data"]["speakers"][identified_id] = speaker_info
                            
                            if is_new_speaker:
                                final_result["data"]["new_speakers_registered"] += 1
                                logger.info(f"自动注册新说话人: {speaker_name} (置信度: {confidence:.3f})")
                            else:
                                logger.info(f"识别为: {speaker_name} (置信度: {confidence:.3f})")
                        else:
                            # 无法识别，使用默认名称
                            default_id = f"unknown_{spk_id}"
                            default_name = f"说话人{int(spk_id)+1}"
                            speaker_info = {
                                "real_id": default_id,
                                "real_name": default_name,
                                "color": self.speaker_colors[int(spk_id) % len(self.speaker_colors)],
                                "confidence": 0.0,
                                "is_auto_registered": False,
                                "segment_count": int(len(segments)),
                                "total_duration_ms": int(sum(s["duration"] for s in segments))
                            }
                            
                            speaker_mapping[spk_id] = default_id
                            final_result["data"]["speakers"][default_id] = speaker_info
                            logger.info(f"无法识别，标记为: {default_name}")
                    else:
                        logger.info(f"提取音频片段失败，跳过说话人 {spk_id}")
            else:
                # 不启用声纹识别，直接使用FunASR给出的说话人ID
                for idx, (spk_id, segments) in enumerate(unique_speakers.items()):
                    speaker_id = f"speaker_{spk_id}"
                    speaker_name = f"说话人{int(spk_id)+1}"
                    speaker_info = {
                        "real_id": speaker_id,
                        "real_name": speaker_name,
                        "color": self.speaker_colors[int(spk_id) % len(self.speaker_colors)],
                        "confidence": 1.0,  # FunASR模型的说话人分离置信度设为1.0
                        "is_auto_registered": False,
                        "segment_count": int(len(segments)),
                        "total_duration_ms": int(sum(s["duration"] for s in segments))
                    }
                    
                    speaker_mapping[spk_id] = speaker_id
                    final_result["data"]["speakers"][speaker_id] = speaker_info
                    logger.info(f"说话人 {spk_id} -> {speaker_name}")
        
        # 第二步：构建详细的对话片段信息（包含时间戳）
        max_end_time = 0
        
        
        # 优先使用 text_spk（如果可用），因为它通常包含完整的文字片段
        if "text_spk" in result and len(result["text_spk"]) > 0:
            logger.info(f"使用 text_spk 构建片段，共 {len(result['text_spk'])} 个片段")
            
            # 合并同一说话人的连续片段（特别是标点符号片段）
            merged_segments = []
            current_segment = None
            
            for item in result["text_spk"]:
                spk_id = str(item.get("spk", "unknown"))
                text = str(item.get("text", ""))
                start_time = int(item.get("start", 0))
                end_time = int(item.get("end", 0))
                
                # 更新最大结束时间
                if end_time > max_end_time:
                    max_end_time = end_time
                
                # 判断是否应该合并到当前片段
                should_merge = False
                if current_segment:
                    is_same_speaker = (spk_id == current_segment['speaker_id'])
                    time_gap = start_time - current_segment['end_time_ms']
                    # 如果当前文本只是标点符号（长度<=2且都是标点）
                    is_punctuation_only = len(text.strip()) <= 2 and all(c in '，。、！？；：""''（）《》【】' for c in text.strip())
                    
                    # 同一说话人且时间间隔<500ms，或者当前是标点符号
                    if is_same_speaker and (time_gap < 500 or is_punctuation_only):
                        should_merge = True
                
                if should_merge:
                    # 合并到当前片段
                    current_segment['text'] += text
                    current_segment['end_time_ms'] = int(end_time)
                    current_segment['duration_ms'] = int(end_time - current_segment['start_time_ms'])
                else:
                    # 保存之前的片段
                    if current_segment:
                        merged_segments.append(current_segment)
                    
                    # 创建新片段
                    real_speaker_id = speaker_mapping.get(spk_id, f"unknown_{spk_id}")
                    speaker_info = final_result["data"]["speakers"].get(real_speaker_id, {
                        "real_id": real_speaker_id,
                        "real_name": f"未知{spk_id}",
                        "color": "⚫",
                        "confidence": 0.0,
                        "is_auto_registered": False
                    })
                    
                    current_segment = {
                        "speaker_id": str(spk_id),
                        "real_speaker_id": speaker_info["real_id"],
                        "speaker_name": speaker_info["real_name"],
                        "color": speaker_info["color"],
                        "text": text,
                        "start_time_ms": int(start_time),
                        "end_time_ms": int(end_time),
                        "duration_ms": int(end_time - start_time),
                        "confidence": float(speaker_info.get("confidence", 0.0)),
                        "is_auto_registered": bool(speaker_info.get("is_auto_registered", False))
                    }
            
            # 添加最后一个片段
            if current_segment:
                merged_segments.append(current_segment)
            
            # 片段级声纹校正（仅在启用声纹识别时生效）
            if enable_speaker_verification and len(merged_segments) > 0:
                logger.info("开始进行片段级声纹校正...")
                # 先把 speaker 计数清零，后面重新统计
                for spk_info in final_result["data"]["speakers"].values():
                    spk_info["segment_count"] = 0
                    spk_info["total_duration_ms"] = 0

                for seg in merged_segments:
                    start_ms = int(seg["start_time_ms"])
                    end_ms = int(seg["end_time_ms"])
                    duration_ms = end_ms - start_ms

                    # 片段太短时，声纹不稳定，跳过（阈值可以调整）
                    if duration_ms < 1500:
                        continue

                    temp_segment_path = os.path.join(
                        self.workspace_dir,
                        f"temp_segment_verify_{seg['speaker_id']}_{start_ms}_{end_ms}.wav"
                    )

                    if not self.extract_audio_segment(audio_url, start_ms, end_ms, temp_segment_path):
                        continue

                    try:
                        identified_id, confidence, speaker_name = self.identify_speaker(
                            temp_segment_path, threshold
                        )
                    except Exception as e:
                        logger.error(f"片段级声纹识别失败: {e}")
                        try:
                            os.remove(temp_segment_path)
                        except Exception:
                            pass
                        continue
                    finally:
                        try:
                            os.remove(temp_segment_path)
                        except Exception:
                            pass

                    # 声纹识别失败
                    if not identified_id or identified_id not in self.speaker_db:
                        continue

                    speaker_data = self.speaker_db[identified_id]
                    old_conf = float(seg.get("confidence", 0.0))

                    # 只有在置信度更高时才替换
                    if confidence <= old_conf:
                        continue

                    logger.info(
                        f"片段级修正: 原说话人 {seg['real_speaker_id']} -> {identified_id}, "
                        f"置信度 {old_conf:.3f} -> {confidence:.3f}"
                    )

                    seg["real_speaker_id"] = identified_id
                    seg["speaker_name"] = speaker_name
                    seg["color"] = speaker_data.get("color", seg.get("color", "⚫"))
                    seg["confidence"] = float(confidence)
                    seg["is_auto_registered"] = bool(speaker_data.get("is_auto_registered", False))

                    # 确保 speakers 里有这个人
                    if identified_id not in final_result["data"]["speakers"]:
                        final_result["data"]["speakers"][identified_id] = {
                            "real_id": identified_id,
                            "real_name": speaker_name,
                            "color": speaker_data.get("color", "⚫"),
                            "confidence": float(confidence),
                            "is_auto_registered": bool(speaker_data.get("is_auto_registered", False)),
                            "segment_count": 0,
                            "total_duration_ms": 0,
                        }

                # 重新统计每个说话人的片段数和时长
                for seg in merged_segments:
                    rid = seg["real_speaker_id"]
                    if rid in final_result["data"]["speakers"]:
                        final_result["data"]["speakers"][rid]["segment_count"] += 1
                        final_result["data"]["speakers"][rid]["total_duration_ms"] += int(
                            seg["duration_ms"]
                        )

            # 添加到最终结果，并设置 segment_id
            for idx, seg in enumerate(merged_segments):
                seg['segment_id'] = int(idx)
                final_result["data"]["segments"].append(seg)
            
            logger.info(f"合并后剩余 {len(merged_segments)} 个片段")
        
        # 如果 text_spk 不存在，尝试使用 sentence_info 作为备选
        elif "sentence_info" in result and len(result["sentence_info"]) > 0:
            logger.info(f"备选：使用 sentence_info 构建片段，共 {len(result['sentence_info'])} 个片段")
            
            # 直接使用 sentence_info 中的 text 字段，按说话人合并连续片段
            logger.info(f"直接使用 sentence_info 中的文本，按说话人合并")
            
            speaker_segments = []
            current_segment = None
            
            for idx, sent in enumerate(result["sentence_info"]):
                spk_id = str(sent.get("spk", "unknown"))
                text = str(sent.get("text", ""))
                start_time = int(sent.get("start", 0))
                end_time = int(sent.get("end", 0))
                
                # 跳过只有标点符号的片段（长度<=2）
                if len(text.strip()) <= 2:
                    # 更新最大结束时间
                    if end_time > max_end_time:
                        max_end_time = end_time
                    continue
                
                # 更新最大结束时间
                if end_time > max_end_time:
                    max_end_time = end_time
                
                # 判断是否应该合并到当前片段（同一说话人）
                if current_segment and current_segment["spk"] == spk_id:
                    # 同一说话人，合并文本和时间
                    current_segment["text"] += text
                    current_segment["end"] = end_time
                else:
                    # 说话人切换，保存前一个片段
                    if current_segment:
                        speaker_segments.append(current_segment)
                    
                    # 开始新片段
                    current_segment = {
                        "spk": spk_id,
                        "text": text,
                        "start": start_time,
                        "end": end_time
                    }
            
            # 添加最后一个片段
            if current_segment:
                speaker_segments.append(current_segment)
            
            logger.info(f"检测到 {len(speaker_segments)} 个说话人片段")
            
            # 构建最终的片段信息
            for i, seg in enumerate(speaker_segments):
                spk_id = seg["spk"]
                start_time = seg["start"]
                end_time = seg["end"]
                segment_text = seg["text"]
                
                real_speaker_id = speaker_mapping.get(spk_id, f"unknown_{spk_id}")
                speaker_info = final_result["data"]["speakers"].get(real_speaker_id, {
                    "real_id": real_speaker_id,
                    "real_name": f"未知{spk_id}",
                    "color": "⚫",
                    "confidence": 0.0,
                    "is_auto_registered": False
                })
                
                segment_info = {
                    "id": int(i),
                    "speaker_id": str(spk_id),
                    "speaker": str(speaker_info["real_name"]),
                    "real_speaker_id": str(speaker_info["real_id"]),
                    "seek": int(spk_id), # str(speaker_info["real_name"]),
                    "color": str(speaker_info["color"]),
                    "text": segment_text.strip(),
                    "start": int(start_time),
                    "end": int(end_time),
                    "duration": int(end_time - start_time),
                    "confidence": float(speaker_info.get("confidence", 0.0)),
                    "is_auto_registered": bool(speaker_info.get("is_auto_registered", False))
                }
                
                final_result["data"]["segments"].append(segment_info)
            
            logger.info(f"最终生成 {len(speaker_segments)} 个片段，每个片段包含模型识别的文本")
        else:
            logger.info("警告: 未找到 text_spk 或 sentence_info 数据")
            # 打印 result 的键，帮助调试
            logger.info(f"ASR 结果包含的键: {list(result.keys())}")
        
        # 设置总时长（确保是 Python int）
        if max_end_time > 0:
            final_result["data"]["duration_ms"] = int(max_end_time)
        else:
            # 如果没有时间戳，尝试从音频文件获取
            try:
                import soundfile as sf
                data, samplerate = sf.read(audio_url)
                duration_samples = len(data)
                final_result["data"]["duration_ms"] = int(duration_samples / samplerate * 1000)
            except:
                final_result["data"]["duration_ms"] = 0
        
        # 设置说话人数量（确保是 Python int）
        final_result["data"]["speaker_count"] = int(len(final_result["data"]["speakers"]))
        
        logger.info(f"识别完成: {final_result['data']['speaker_count']} 个说话人, {len(final_result['data']['segments'])} 个片段")
        
        return final_result
    
    def _asr_only(self, audio_url: str, hotWord: str = "", language: str = "") -> Dict:
        """
        仅进行语音识别，不做说话人区分（快速模式）
        
        参数:
            audio_url: 音频文件路径
            hotWord: 热词字符串（格式：词1 权重1 词2 权重2）
            language: 语言类型（"zn", "en", "yue", "ja", "ko", "nospeech"）
        
        返回:
            {
                "success": True/False,
                "message": "识别成功/失败信息",
                "data": {
                    "text": "完整识别文本",
                    "duration_ms": 总时长,
                    "segments": [
                        {
                            "text": "片段文本",
                            "start_time_ms": 开始时间,
                            "end_time_ms": 结束时间,
                            "duration_ms": 片段时长
                        },
                        ...
                    ]
                }
            }
        """
        logger.info("开始仅ASR识别（不进行说话人区分）...")
        
        try:
            # 获取ASR结果
            asr_result = self.asr_model_asr_only.generate(
                input=audio_url,
                cache={},
                language=language,  # "zn", "en", "yue", "ja", "ko", "nospeech"
                use_itn=True,
                batch_size_s=60,
                merge_vad=True,
                merge_length_s=15,
                hotWord=hotWord
            )
            
            if not asr_result or len(asr_result) == 0:
                logger.info("未识别到任何语音内容")
                return {
                    "success": False,
                    "message": "未识别到任何语音内容",
                    "data": None
                }
            
            result = asr_result[0]
            logger.info(f"ASR识别完成:{result}")
            
            # 初始化返回结果
            final_result = {
                "success": True,
                "message": "识别成功",
                "data": {
                    "text": result.get("text", ""),
                    "duration_ms": 0,
                    "segments": []
                }
            }
            
            # 构建片段信息（如果有 text_spk）
            max_end_time = 0
            
            if "text_spk" in result and len(result["text_spk"]) > 0:
                logger.info(f"使用 text_spk 构建片段，共 {len(result['text_spk'])} 个片段")
                
                for idx, item in enumerate(result["text_spk"]):
                    text = str(item.get("text", ""))
                    start_time = int(item.get("start", 0))
                    end_time = int(item.get("end", 0))
                    
                    # 更新最大结束时间
                    if end_time > max_end_time:
                        max_end_time = end_time
                    
                    # 跳过空文本
                    if not text.strip():
                        continue
                    
                    segment_info = {
                        "segment_id": int(idx),
                        "text": text.strip(),
                        "start_time_ms": int(start_time),
                        "end_time_ms": int(end_time),
                        "duration_ms": int(end_time - start_time)
                    }
                    
                    final_result["data"]["segments"].append(segment_info)
                
                logger.info(f"生成 {len(final_result['data']['segments'])} 个片段")
            
            elif "sentence_info" in result and len(result["sentence_info"]) > 0:
                logger.info(f"备选：使用 sentence_info 构建片段，共 {len(result['sentence_info'])} 个片段")
                
                for idx, sent in enumerate(result["sentence_info"]):
                    text = str(sent.get("text", ""))
                    start_time = int(sent.get("start", 0))
                    end_time = int(sent.get("end", 0))
                    
                    # 更新最大结束时间
                    if end_time > max_end_time:
                        max_end_time = end_time
                    
                    # 跳过空文本和仅标点符号的片段
                    if not text.strip() or len(text.strip()) <= 2:
                        continue
                    
                    segment_info = {
                        "segment_id": int(idx),
                        "text": text.strip(),
                        "start_time_ms": int(start_time),
                        "end_time_ms": int(end_time),
                        "duration_ms": int(end_time - start_time)
                    }
                    
                    final_result["data"]["segments"].append(segment_info)
                
                logger.info(f"生成 {len(final_result['data']['segments'])} 个片段")
            
            else:
                logger.info("未找到 text_spk 或 sentence_info 数据")
                logger.info(f"ASR 结果包含的键: {list(result.keys())}")
            
            # 设置总时长
            if max_end_time > 0:
                final_result["data"]["duration_ms"] = int(max_end_time)
            else:
                # 尝试从音频文件获取时长
                try:
                    import soundfile as sf
                    data, samplerate = sf.read(audio_url)
                    duration_samples = len(data)
                    final_result["data"]["duration_ms"] = int(duration_samples / samplerate * 1000)
                except Exception as e:
                    logger.warning(f"无法从音频文件获取时长: {e}")
                    final_result["data"]["duration_ms"] = 0
            
            logger.info(f"识别完成: {len(final_result['data']['segments'])} 个片段，总时长 {final_result['data']['duration_ms']}ms")
            
            return final_result
        
        except Exception as e:
            logger.error(f"ASR识别失败: {e}", exc_info=True)
            return {
                "success": False,
                "message": f"识别失败: {str(e)}",
                "data": None
            }

    def display_results(self, result: Dict):
        """显示结果"""
        if not result:
            logger.info("❌ 无结果显示")
            return
        
        logger.info("\n" + "="*80)
        logger.info("说话人区分结果（自动声纹库）")
        logger.info("="*80)
        
        # 显示新注册的说话人统计
        if result["new_speakers_registered"] > 0:
            logger.info(f"\n🎉 本次分析自动注册了 {result['new_speakers_registered']} 个新说话人！")
        
        # 显示说话人列表
        if result["speakers"]:
            logger.info("\n👥 检测到的说话人:")
            for spk_id, spk_info in result["speakers"].items():
                status = "🆕" if spk_info.get("is_auto_registered", False) else "✅"
                confidence_str = f"(置信度: {spk_info['confidence']:.3f})" if spk_info['confidence'] > 0 else ""
                logger.info(f"  {status} {spk_info['color']} {spk_id} -> {spk_info['real_name']} {confidence_str}")
        
        # 显示对话内容
        if result["segments"]:
            logger.info(f"\n💬 对话内容 ({len(result['segments'])} 个片段):")
            logger.info("-" * 80)
            
            for i, segment in enumerate(result["segments"]):
                new_indicator = "🆕 " if segment.get('is_auto_registered', False) else ""
                logger.info(f"\n{new_indicator}{segment['color']} [{segment['speaker_name']}]:")
                logger.info(f"  {segment['text']}")
                
                if segment['confidence'] > 0:
                    logger.info(f"  📊 声纹置信度: {segment['confidence']:.3f}")
        
        # 显示完整文本
        if result["original_text"]:
            logger.info(f"\n📝 完整识别文本:")
            logger.info("-" * 80)
            logger.info(result["original_text"])
        
        # 显示数据库统计
        auto_count = len([v for v in self.speaker_db.values() if v.get('is_auto_registered', False)])
        manual_count = len(self.speaker_db) - auto_count
        logger.info(f"\n📊 声纹库统计: 总计 {len(self.speaker_db)} 个说话人 ({manual_count} 手动注册, {auto_count} 自动注册)")
    
    def save_speaker_db(self, user_id: str):
        """保存声纹数据库"""
        try:
            self.load_speaker_db(user_id)  # 确保加载最新的数据库
            db_file = os.path.join(self.workspace_dir, user_id ,"speaker_database.json")
            os.makedirs(os.path.dirname(db_file), exist_ok=True)
            save_data = {}
            for spk_id, spk_data in self.speaker_db.items():
                save_data[spk_id] = {
                    "name": spk_data["name"],
                    "embedding": spk_data["embedding"].tolist(),
                    "color": spk_data["color"],
                    "registration_time": spk_data["registration_time"],
                    "audio_count": spk_data["audio_count"],
                    "is_auto_registered": spk_data.get("is_auto_registered", False),
                    "original_spk_id": spk_data.get("original_spk_id", ""),
                    "detection_confidence": spk_data.get("detection_confidence", 0.0)
                }
            
            with open(db_file, 'w', encoding='utf-8') as f:
                json.dump(save_data, f, ensure_ascii=False, indent=2)
            
            logger.info(f"声纹数据库已保存: {db_file}")
        except Exception as e:
            logger.error(f"保存数据库失败: {e}")
        finally:
            self.speaker_db = {}
    
    def load_speaker_db(self, user_id: str):
        """加载指定用户的声纹数据库"""
        db_file = os.path.join(self.workspace_dir, user_id, "speaker_database.json")    
        try:
            if os.path.exists(db_file):
                with open(db_file, 'r', encoding='utf-8') as f:
                    save_data = json.load(f)
                
                for spk_id, spk_data in save_data.items():
                    self.speaker_db[spk_id] = {
                        "name": spk_data["name"],
                        "embedding": torch.tensor(spk_data["embedding"]),
                        "color": spk_data["color"],
                        "registration_time": spk_data["registration_time"],
                        "audio_count": spk_data["audio_count"],
                        "is_auto_registered": spk_data.get("is_auto_registered", False),
                        "original_spk_id": spk_data.get("original_spk_id", ""),
                        "detection_confidence": spk_data.get("detection_confidence", 0.0)
                    }
                
                logger.info(f"声纹数据库已加载: {len(self.speaker_db)} 个说话人")
            else:
                logger.info("📂 未找到现有声纹数据库，将创建新数据库")
        except Exception as e:
            logger.error(f"加载数据库失败: {e}")
    
    def list_speakers(self):
        """列出所有说话人"""
        logger.info(f"\n📋 声纹库中的说话人 (总计: {len(self.speaker_db)})")
        logger.info("-" * 50)
        
        for spk_id, spk_data in self.speaker_db.items():
            reg_type = "🆕 自动" if spk_data.get("is_auto_registered", False) else "✅ 手动"
            logger.info(f"{reg_type} {spk_data['color']} {spk_id}: {spk_data['name']}")
            logger.info(f"     注册时间: {spk_data['registration_time']}")
            logger.info(f"     音频样本: {spk_data['audio_count']} 个")
            if spk_data.get('detection_confidence', 0) > 0:
                logger.info(f"     检测置信度: {spk_data['detection_confidence']:.3f}")
            logger.info("")

# 使用示例
def main():
    # 初始化系统
    system = AutoSpeakerRecognitionSystem()
    
    # 可以手动注册一些已知说话人（可选）
    """
    known_speakers = [
        {"id": "spk_001", "name": "张三", "audios": ["audio/zhang_san1.wav", "audio/zhang_san2.wav"]},
        {"id": "spk_002", "name": "李四", "audios": ["audio/li_si1.wav", "audio/li_si2.wav"]}
    ]
    
    for speaker in known_speakers:
        if all(os.path.exists(audio) for audio in speaker["audios"]):
            system.register_speaker(speaker["audios"], speaker["id"], speaker["name"])
    """
    
    # 显示当前声纹库
    system.list_speakers()
    
    # 进行分析（会自动补充声纹库）
    audio_url = "asr_speaker_demo.wav" # https://isv-data.oss-cn-hangzhou.aliyuncs.com/ics/MaaS/ASR/test_audio/asr_speaker_demo.wav
    
    logger.info(f"\n开始分析音频: {audio_url}")
    result = system.diarize_with_auto_registration(audio_url, threshold=0.6)
    
    # 显示结果
    system.display_results(result)
    
    # 显示更新后的声纹库
    system.list_speakers()

if __name__ == "__main__":
    main()