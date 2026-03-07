#!/bin/bash

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Please run this script with sudo.${NC}"
    echo "Usage: sudo ./install_service.sh"
    exit 1
fi

REAL_USER=${SUDO_USER:-$USER}
SERVICE_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="$(cd "${SERVICE_DIR}/.." && pwd)"

echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW} Install FunASR services${NC}"
echo -e "${YELLOW}========================================${NC}"
echo -e "${GREEN}User: ${REAL_USER}${NC}"
echo -e "${GREEN}App dir: ${APP_DIR}${NC}"
echo ""

echo -e "${YELLOW}[1/5] Prepare runtime directories...${NC}"
mkdir -p "${APP_DIR}/logs"
mkdir -p "${APP_DIR}/temp_oss_files"
mkdir -p "${APP_DIR}/speaker_workspace/unknown_speakers"
chown -R "${REAL_USER}:${REAL_USER}" "${APP_DIR}/logs" "${APP_DIR}/temp_oss_files" "${APP_DIR}/speaker_workspace"

echo -e "${YELLOW}[2/5] Install service files...${NC}"
cp "${SERVICE_DIR}/funasr-api.service" /etc/systemd/system/
cp "${SERVICE_DIR}/funasr-consumer.service" /etc/systemd/system/

sed -i "s|User=wuyou|User=${REAL_USER}|g" /etc/systemd/system/funasr-api.service
sed -i "s|Group=wuyou|Group=${REAL_USER}|g" /etc/systemd/system/funasr-api.service
sed -i "s|/home/wuyou/app/funasr_server|${APP_DIR}|g" /etc/systemd/system/funasr-api.service

sed -i "s|User=wuyou|User=${REAL_USER}|g" /etc/systemd/system/funasr-consumer.service
sed -i "s|Group=wuyou|Group=${REAL_USER}|g" /etc/systemd/system/funasr-consumer.service
sed -i "s|/home/wuyou/app/funasr_server|${APP_DIR}|g" /etc/systemd/system/funasr-consumer.service

echo -e "${YELLOW}[3/5] Reload systemd...${NC}"
systemctl daemon-reload

echo -e "${YELLOW}[4/5] Enable services...${NC}"
systemctl enable funasr-api.service
systemctl enable funasr-consumer.service

echo -e "${YELLOW}[5/5] Start services...${NC}"
systemctl start funasr-api.service
sleep 5
systemctl start funasr-consumer.service

echo ""
echo -e "${GREEN}Install completed.${NC}"
echo "Check status:"
echo "  sudo systemctl status funasr-api"
echo "  sudo systemctl status funasr-consumer"
echo ""
echo "View logs:"
echo "  sudo journalctl -u funasr-api -f"
echo "  sudo journalctl -u funasr-consumer -f"
echo "  tail -f ${APP_DIR}/logs/api_server.log"
echo "  tail -f ${APP_DIR}/logs/consumer.log"
