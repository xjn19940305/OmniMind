"""
模型参数优化使用示例
演示如何在实际代码中应用不同场景的配置
"""

from model_config import get_config, list_scenes
from paraformerdome import AutoSpeakerRecognitionSystem


# ============================================================================
# 示例 1: 使用预设场景配置
# ============================================================================

def example_1_use_preset_config():
    """使用预设场景配置"""
    print("="*60)
    print("示例 1: 使用预设场景配置")
    print("="*60)
    
    # 获取高质量录音配置
    config = get_config("high_quality")
    print(f"\n使用场景: {config['name']}")
    print(f"描述: {config['description']}")
    
    # 注意：当前代码需要修改才能使用这些参数
    # 下面展示如何修改
    print("\n需要修改 paraformerdome.py 的 __init__ 方法:")
    print("""
    self.asr_model = AutoModel(
        model="paraformer-zh",
        vad_model="fsmn-vad",
        vad_kwargs={
            "max_single_segment_time": 20000,
            "speech_noise_thres": 0.5,
            "min_silence_duration_ms": 400
        }
    )
    """)


# ============================================================================
# 示例 2: 在 API 中添加场景选择
# ============================================================================

def example_2_api_with_scene():
    """演示如何在 API 中添加场景参数"""
    print("\n" + "="*60)
    print("示例 2: API 中添加场景选择")
    print("="*60)
    
    print("\n修改 api_server.py 的识别接口:")
    print("""
@app.post("/api/recognize")
async def recognize_audio(
    file: UploadFile = File(...),
    threshold: float = Form(0.6),
    scene: str = Form("default"),      # 新增：场景参数
    hotword: str = Form('')             # 新增：热词参数
):
    # 获取场景配置
    from model_config import get_config
    config = get_config(scene)
    
    # 使用配置中的参数
    result = recognition_system.diarize_with_auto_registration(
        temp_file_path, 
        threshold=config['speaker_threshold'],  # 使用配置的阈值
        hotword=hotword or config['generate_kwargs']['hotword']
    )
    """)
    
    print("\n调用示例:")
    print("""
# curl 方式
curl -X POST "http://localhost:8000/api/recognize" \\
  -F "file=@audio.wav" \\
  -F "scene=high_quality" \\
  -F "hotword=项目管理 20 需求分析 15"

# Python requests 方式
import requests

files = {'file': open('audio.wav', 'rb')}
data = {
    'scene': 'high_quality',
    'hotword': '项目管理 20 需求分析 15'
}
response = requests.post('http://localhost:8000/api/recognize', 
                        files=files, data=data)
    """)


# ============================================================================
# 示例 3: 快速提升准确度的 Top 5 方法
# ============================================================================

def example_3_quick_improvements():
    """快速提升准确度的方法"""
    print("\n" + "="*60)
    print("示例 3: 快速提升准确度 Top 5")
    print("="*60)
    
    tips = [
        {
            "rank": 1,
            "title": "添加热词",
            "difficulty": "⭐ 简单",
            "effect": "⭐⭐⭐⭐⭐ 立即见效",
            "example": """
# 修改 api_server.py 或直接在调用时添加
result = system.diarize_with_auto_registration(
    audio_path,
    hotword='阿里云 20 魔搭 15 FunASR 10'  # 添加专业术语
)
            """
        },
        {
            "rank": 2,
            "title": "根据环境调整噪声阈值",
            "difficulty": "⭐⭐ 中等",
            "effect": "⭐⭐⭐⭐ 显著提升",
            "example": """
# 嘈杂环境：提高阈值
vad_kwargs = {
    "speech_noise_thres": 0.75  # 从 0.6 提高到 0.75
}

# 安静环境：降低阈值
vad_kwargs = {
    "speech_noise_thres": 0.5   # 从 0.6 降低到 0.5
}
            """
        },
        {
            "rank": 3,
            "title": "优化语音分段参数",
            "difficulty": "⭐⭐ 中等",
            "effect": "⭐⭐⭐⭐ 避免切断",
            "example": """
# 快速对话：减小静音判断时间
vad_kwargs = {
    "min_silence_duration_ms": 300  # 从 500 减到 300
}

# 慢速演讲：增大静音判断时间
vad_kwargs = {
    "min_silence_duration_ms": 800  # 从 500 增到 800
}
            """
        },
        {
            "rank": 4,
            "title": "使用高质量音频",
            "difficulty": "⭐ 简单",
            "effect": "⭐⭐⭐⭐⭐ 基础保障",
            "example": """
推荐音频格式:
- 格式: WAV, FLAC (无损格式)
- 采样率: 16kHz 或更高
- 位深度: 16-bit 或更高
- 声道: 单声道或双声道
- 避免: 过度压缩的 MP3

转换命令 (ffmpeg):
ffmpeg -i input.mp3 -ar 16000 -ac 1 -c:a pcm_s16le output.wav
            """
        },
        {
            "rank": 5,
            "title": "调整说话人识别阈值",
            "difficulty": "⭐ 简单",
            "effect": "⭐⭐⭐ 优化识别",
            "example": """
# 严格识别（减少误判）
threshold = 0.7  # 从 0.6 提高到 0.7

# 宽松识别（减少漏判）
threshold = 0.55  # 从 0.6 降低到 0.55

# 在 API 调用时
result = system.diarize_with_auto_registration(
    audio_path,
    threshold=0.65  # 根据实际测试调整
)
            """
        }
    ]
    
    for tip in tips:
        print(f"\n{tip['rank']}. {tip['title']}")
        print(f"   难度: {tip['difficulty']}")
        print(f"   效果: {tip['effect']}")
        print(f"   示例:")
        print(tip['example'])


# ============================================================================
# 示例 4: 完整的参数优化流程
# ============================================================================

def example_4_complete_workflow():
    """完整的参数优化工作流"""
    print("\n" + "="*60)
    print("示例 4: 完整的参数优化流程")
    print("="*60)
    
    workflow = """
第一步：评估当前状态
-----------------
1. 准备测试音频（3-5个不同场景的样本）
2. 使用默认参数识别，记录结果
3. 评估问题：
   - 有语音被漏掉？      → 降低 speech_noise_thres
   - 有噪音被误识别？    → 提高 speech_noise_thres
   - 语音被切断？        → 减小 min_silence_duration_ms
   - 分段太多太碎？      → 增大 min_silence_duration_ms
   - 专业术语错误多？    → 添加热词


第二步：选择合适的场景配置
-----------------------
from model_config import list_scenes, get_config

# 查看所有场景
list_scenes()

# 选择最接近的场景
config = get_config("high_quality")  # 或其他场景


第三步：微调参数
--------------
# 基于场景配置，调整个别参数
custom_config = {
    "vad_kwargs": {
        "speech_noise_thres": 0.65,  # 微调
    },
    "generate_kwargs": {
        "hotword": "特定术语 20 专业词汇 15"
    }
}


第四步：测试和对比
----------------
# 测试新配置
# 对比识别结果
# 计算准确率提升


第五步：部署应用
--------------
# 将优化后的参数应用到生产环境
# 持续监控效果
# 定期调整优化
    """
    
    print(workflow)


# ============================================================================
# 示例 5: 不同行业的热词配置
# ============================================================================

def example_5_industry_hotwords():
    """不同行业的热词配置示例"""
    print("\n" + "="*60)
    print("示例 5: 不同行业热词配置")
    print("="*60)
    
    industries = {
        "互联网/IT": [
            "云计算 20", "大数据 20", "人工智能 20", "机器学习 18",
            "API接口 15", "微服务 15", "容器化 15", "DevOps 15",
            "用户体验 12", "敏捷开发 12", "需求分析 12", "架构设计 12"
        ],
        "医疗健康": [
            "血压 20", "心率 20", "体温 20", "血糖 18",
            "诊断 15", "处方 15", "检查 15", "治疗 15",
            "CT 12", "核磁共振 12", "超声波 12", "心电图 12"
        ],
        "金融保险": [
            "账户 20", "余额 20", "交易 20", "转账 18",
            "理财 15", "基金 15", "股票 15", "保险 15",
            "风险 12", "收益 12", "投资 12", "贷款 12"
        ],
        "电商零售": [
            "订单号 20", "物流 20", "快递 20", "配送 18",
            "退款 15", "售后 15", "评价 15", "优惠券 15",
            "支付 12", "发货 12", "收货 12", "退货 12"
        ],
        "教育培训": [
            "课程 20", "学习 20", "作业 20", "考试 18",
            "成绩 15", "辅导 15", "讲解 15", "练习 15",
            "知识点 12", "复习 12", "预习 12", "答疑 12"
        ],
        "房地产": [
            "户型 20", "面积 20", "价格 20", "楼盘 18",
            "地段 15", "配套 15", "交通 15", "学区 15",
            "贷款 12", "首付 12", "月供 12", "产权 12"
        ]
    }
    
    for industry, hotwords in industries.items():
        print(f"\n【{industry}】")
        hotword_str = " ".join(hotwords)
        print(f"热词配置: {hotword_str}")
        print(f"\n使用方式:")
        print(f'hotword="{hotword_str}"')


# ============================================================================
# 主函数
# ============================================================================

if __name__ == "__main__":
    print("\n" + "="*60)
    print("模型参数优化使用示例")
    print("="*60)
    
    # 运行所有示例
    example_1_use_preset_config()
    example_2_api_with_scene()
    example_3_quick_improvements()
    example_4_complete_workflow()
    example_5_industry_hotwords()
    
    print("\n" + "="*60)
    print("总结")
    print("="*60)
    print("""
核心建议:
1. ✅ 先使用默认配置建立基准
2. ✅ 根据场景选择预设配置
3. ✅ 添加行业相关热词（最快见效）
4. ✅ 根据音频质量调整噪声阈值
5. ✅ 根据语速调整分段参数
6. ✅ 多次测试找到最佳配置
7. ✅ 记录参数和效果，便于对比

详细文档:
- 模型参数优化指南.md
- model_config.py
    """)

