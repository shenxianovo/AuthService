#!/bin/bash
set -e

export NVM_DIR="/home/shenxianovo/.nvm"
source "$NVM_DIR/nvm.sh"
nvm use 20

# ==== 配置区域 ====
APP_NAME="AuthService"
APP_DIR="/srv/AuthService"
DOTNET_PROJECT="AuthService/AuthService.csproj"
VUE_PROJECT="frontend"
LOG_FILE="$APP_DIR/$APP_NAME.log"
PID_FILE="$APP_DIR/$APP_NAME.pid"

# ==== 停止服务 ====
if [ -f "$PID_FILE" ]; then
    PID=$(cat "$PID_FILE")
    if ps -p $PID > /dev/null; then
        echo "Stopping existing service (PID $PID)..."
        kill $PID
        sleep 2
    fi
    rm -f "$PID_FILE"
fi

# ==== 更新代码 ====
cd "$APP_DIR"
echo "Pulling latest code..."
git reset --hard origin/main
git pull origin main

# ==== 复制密钥 ====
cp /srv/keys/AuthService/* "$APP_DIR/AuthService/Keys/"

# ==== 启动服务 ====
echo "Starting service..."
npm ci --prefix "$VUE_PROJECT"
npm run build --prefix "$VUE_PROJECT"

nohup dotnet run --project "$DOTNET_PROJECT" > "$LOG_FILE" 2>&1 &

# 记录 PID
echo $! > "$PID_FILE"

echo "Service started (PID $(cat $PID_FILE)), logs: $LOG_FILE"