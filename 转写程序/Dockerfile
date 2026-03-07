FROM python:3.10-slim AS base

WORKDIR /app

RUN apt-get update && apt-get install -y \
    ffmpeg \
    libsndfile1 \
    build-essential \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

FROM base AS api-server

COPY . .
RUN mkdir -p logs temp_audio temp_oss_files speaker_workspace/unknown_speakers

EXPOSE 8000
CMD ["uvicorn", "api_server:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]

FROM base AS consumer

COPY . .
RUN mkdir -p logs temp_audio temp_oss_files speaker_workspace/unknown_speakers

CMD ["python", "asr_consumer.py"]
