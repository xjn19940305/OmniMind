#!/bin/bash

set -e

if [ -d "venv" ]; then
    echo "Activate virtualenv..."
    # shellcheck disable=SC1091
    source venv/bin/activate
fi

if [ ! -f "config.ini" ]; then
    echo "config.ini not found."
    echo "Run: cp config.ini.example config.ini"
    exit 1
fi

if grep -q "your-bucket-name" config.ini; then
    echo "Please update OSS settings in config.ini before starting."
    exit 1
fi

echo "Start ASR consumer..."
python asr_consumer.py
