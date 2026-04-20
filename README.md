# AuthService

简易认证服务，~~男大自用99新~~

## 项目结构

```
AuthService/          — .NET 后端
frontend/             — Vue 3 前端 (Vite + TypeScript)
```

## 前端开发

```bash
cd frontend
npm install
npm run dev          # 启动开发服务器 (默认代理 API 到 localhost:5252)
npm run build        # 构建生产版本
npm run typecheck    # 类型检查
```

## 重新生成 API 客户端

前端使用 [NSwag CLI](https://github.com/RicoSuter/NSwag) 从后端 OpenAPI 文档生成 TypeScript 客户端。

```bash
# 1. 确保后端正在运行 (需要能访问 swagger.json)
cd AuthService
dotnet run

# 2. 使用 NSwag CLI 生成客户端 (需要安装 nswag 全局工具)
#    dotnet tool install -g NSwag.ConsoleCore
nswag openapi2tsclient \
  /input:http://localhost:5252/swagger/v1/swagger.json \
  /output:frontend/src/api/client.ts \
  /TypeScriptVersion:5.0 \
  /GenerateClientInterfaces:true \
  /Template:Fetch
```

> 生成后的 `client.ts` 是纯净的独立客户端，不需要额外的基类。
> `frontend/src/api/index.ts` 封装了 token 注入、401 自动刷新等逻辑。