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

cd "$APP_DIR"

# ==== 拉取最新代码 ====
echo "Pulling latest code..."
git fetch origin main
OLD_HEAD=$(git rev-parse HEAD)
git reset --hard origin/main

# ==== 复制密钥 ====
cp /srv/keys/AuthService/* "$APP_DIR/AuthService/Keys/"

# ==== 检查变更 ====
SERVER_CHANGED=$(git diff --name-only $OLD_HEAD HEAD | grep '^AuthService/' || true)
FRONTEND_CHANGED=$(git diff --name-only $OLD_HEAD HEAD | grep '^frontend/' || true)
DEPLOY_CHANGED=$(git diff --name-only $OLD_HEAD HEAD | grep '^deploy/' || true)

# ==== 构建前端（如果有变动） ====
if [ -n "$FRONTEND_CHANGED" ]; then
    echo "Frontend changes detected. Building frontend..."
    npm ci --prefix "$VUE_PROJECT"
    npm run build --prefix "$VUE_PROJECT"
else
    echo "No frontend changes detected. Skipping frontend build."
fi

# ==== 停止服务（如果 server 有变动） ====
if [ -n "$SERVER_CHANGED" ] || [ -n "$DEPLOY_CHANGED" ]; then
    if [ -f "$PID_FILE" ]; then
        PID=$(cat "$PID_FILE")
        if ps -p $PID > /dev/null; then
            echo "Stopping existing service (PID $PID)..."
            kill $PID
            sleep 2
        fi
        rm -f "$PID_FILE"
    fi

    echo "Starting service..."
    nohup dotnet run --project "$DOTNET_PROJECT" > "$LOG_FILE" 2>&1 &
    echo $! > "$PID_FILE"
    echo "Service started (PID $(cat $PID_FILE)), logs: $LOG_FILE"
else
    echo "No backend changes detected. Skipping backend restart."
fi