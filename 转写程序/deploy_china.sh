#!/bin/bash

set -e

echo "=========================================="
echo "ASR 服务中国地区部署"
echo "=========================================="

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

check_cmd() {
    command -v "$1" >/dev/null || {
        echo -e "${RED}$1 未安装${NC}"
        exit 1
    }
}

check_cmd docker
check_cmd docker-compose

mkdir -p logs temp_audio temp_oss_files speaker_workspace/unknown_speakers
[ -f .env ] || cp .env.example .env
[ -f config.ini ] || cp config.docker.ini config.ini

echo -e "${YELLOW}预拉取镜像...${NC}"
for image in rabbitmq:3.12-management python:3.10-slim; do
    docker pull "$image" || true
done

echo -e "${YELLOW}构建镜像...${NC}"
docker-compose build

echo -e "${YELLOW}启动服务...${NC}"
docker-compose up -d

echo -e "${GREEN}部署完成${NC}"
echo "API 文档: http://localhost:8000/funasr/docs"
echo "RabbitMQ 管理: http://localhost:15672"
