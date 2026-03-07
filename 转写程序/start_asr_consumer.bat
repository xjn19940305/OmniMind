@echo off
chcp 65001 >nul
echo ============================================
echo Start ASR consumer
echo ============================================

if not exist "config.ini" (
    echo [ERROR] config.ini not found.
    echo Copy config.ini.example to config.ini first.
    pause
    exit /b 1
)

findstr /C:"your-bucket-name" config.ini >nul
if %errorlevel%==0 (
    echo [ERROR] Please update OSS settings in config.ini first.
    pause
    exit /b 1
)

python asr_consumer.py
pause
