#!/bin/bash

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

if [ "$EUID" -ne 0 ]; then
    echo -e "${RED}Please run this script with sudo.${NC}"
    exit 1
fi

echo -e "${YELLOW}Removing FunASR services...${NC}"

systemctl stop funasr-consumer.service 2>/dev/null || true
systemctl stop funasr-api.service 2>/dev/null || true

systemctl disable funasr-consumer.service 2>/dev/null || true
systemctl disable funasr-api.service 2>/dev/null || true

rm -f /etc/systemd/system/funasr-api.service
rm -f /etc/systemd/system/funasr-consumer.service

systemctl daemon-reload

echo -e "${GREEN}Services removed.${NC}"
