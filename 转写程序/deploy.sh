#!/bin/bash

set -e

echo "=========================================="
echo "ASR 服务部署"
echo "=========================================="

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

command -v docker >/dev/null || { echo -e "${RED}Docker 未安装${NC}"; exit 1; }
command -v docker-compose >/dev/null || { echo -e "${RED}docker-compose 未安装${NC}"; exit 1; }

mkdir -p logs temp_audio temp_oss_files speaker_workspace/unknown_speakers

if [ ! -f ".env" ]; then
    cp .env.example .env
    echo -e "${GREEN}已创建 .env${NC}"
fi

if [ ! -f "config.ini" ]; then
    cp config.docker.ini config.ini
    echo -e "${GREEN}已创建 config.ini${NC}"
fi

echo -e "${YELLOW}构建镜像...${NC}"
docker-compose build

echo -e "${YELLOW}启动服务...${NC}"
docker-compose up -d

echo -e "${GREEN}部署完成${NC}"
echo "API 文档: http://localhost:8000/funasr/docs"
echo "RabbitMQ 管理: http://localhost:15672"
