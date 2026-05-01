# Host and Provider Quickstart

## 目标

这份文档只回答三个问题：

1. 如何启动和停止 CARVES resident host
2. 如何通过 host 调用最常用的运行时命令
3. 如何配置 provider，并把 Gemini / OpenAI / Codex 接成 worker

如果你需要 operator 入口与分流页，再读：

- `docs/guides/RUNTIME_OPERATOR_MANUAL.md`

如果你需要更细的 host / lifecycle 或 provider / worker reference，再读：

- `docs/guides/RUNTIME_OPERATOR_HOST_AND_LIFECYCLE_REFERENCE.md`
- `docs/guides/RUNTIME_PROVIDER_AND_WORKER_REFERENCE.md`

如果你需要 remote API worker 的架构细节，再读：

- `docs/runtime/remote-api-worker-architecture.md`

## 一条总规则

所有 host 命令都通过同一个入口执行：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- <command>
```

例如：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host status
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker providers
```

如果你已经构建了 friendly CLI，也可以从项目目录直接走：

```powershell
carves attach
carves status
carves run
carves repair
```

其中：

- `carves attach`：自动识别当前 git repo、检查/初始化 runtime，并提示下一步
- `carves status`：输出项目级 runtime 摘要
- `carves run`：触发一次 host dispatch / continue
- `carves repair`：委托现有 repair 主链

friendly CLI 不替代 host；它只是把 repo 定位和常用入口收口到更自然的用户命令。

## Stage 5 试跑入口对齐

当前 Runtime Agent v1 的 bounded trial entry 仍然只有这一条：

1. 在 Runtime repo 启动 resident host
2. 在目标项目目录执行 `carves attach`
3. attach 完成后，先按最小 onboarding 顺序读取 `README.md -> AGENTS.md -> carves inspect runtime-first-run-operator-packet`
4. 再从同一条 Runtime-owned lane 继续读 `status` 或后续 governed surfaces
5. 如果要宣称 friend-trial 或 delivery readiness，先读 `inspect runtime-agent-delivery-readiness`
6. 再补读 `inspect runtime-agent-validation-bundle` 并按 bundle 里的 bounded commands 验证

边界要点：

- `carves attach` 不会发明第二套初始化流程；它只是把现有 Host-owned attach 收到更自然的入口
- attach 后的 first-run / bootstrap 解释仍然回到 Runtime-owned packet 和 host surfaces
- delivery readiness 也先回到 Runtime-owned delivery-readiness surface
- delivery readiness 也必须回到 Runtime-owned validation bundle，而不是凭作者记忆挑测试
- 第一张真正的项目初始化卡仍然要走 Host-routed card/taskgraph 路径，而不是 friendly CLI 自己长出 onboarding state
- 如果 host `not_running`，trial path 才回到 `carves host start`
- 如果 host 投影成 `host_session_conflict`，不要循环 `host start`；改走 `carves host reconcile --replace-stale --json`
- 后续如果加入 guided planning chat / graph，引导层也只能帮助用户形成 candidate card；正式 card 写回仍然必须回到 Host-routed 路径

## 1. 启动前准备

先确认你在仓库根目录：

```powershell
Get-Location
Test-Path .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj
```

推荐先做一次 build：

```powershell
dotnet build .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj
```

## 2. Host 最小启动路径

### 2.0 先判断是 start 还是 reconcile

默认不要上来就重复 `host start`。先看：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host ensure --json
```

按返回值判断：

- `host_readiness=connected`：host 已健康
- `host_readiness=healthy_with_pointer_repair`：旧健康 host 已被安全修回，不需要 replace
- `host_readiness=not_running`：再执行 `host start`
- `host_readiness=host_session_conflict`：不要继续 `start/ensure`，改执行 `host reconcile --replace-stale --json`

### 2.1 启动 resident host

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host start --interval-ms 250
```

这条命令会：

1. 把 host payload 部署到独立 runtime 目录
2. 从独立副本启动 resident host
3. 返回 handshake summary

它不会把当前源码目录锁成运行目录，所以适合一边开发 runtime，一边让 host 常驻。

### 2.2 查看 host 状态

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host status
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
```

重点看：

- `Base URL`
- `Runtime stage`
- `Planner state`
- `Workers`
- `Pending approvals`
- `Actionability`

### 2.3 停止 host

优先使用 graceful stop：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host stop "operator shutdown"
```

如果 graceful stop 失败，再用：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host stop --force "forced cleanup"
```

## 3. 最常用的 host 命令

### 3.1 观察面

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- dashboard --text
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker providers
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker profiles CARVES.Runtime
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker select CARVES.Runtime
```

### 3.2 Task 执行入口

先 inspect，再 delegated run：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect task <task-id>
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task run <task-id>
```

例如：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect task T-CARD-192-001
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task run T-CARD-192-001
```

默认规则：

- `task run` 要求 resident host 已启动
- host `not_running` 时，先执行 `host start`
- host `host_session_conflict` 时，先执行 `host reconcile --replace-stale --json`
- `--cold task run <task-id>` 只作为显式 operator fallback

### 3.3 Provider / Worker 路由面

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider list
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider inspect gemini
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider bind CARVES.Runtime gemini-worker-balanced
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker select CARVES.Runtime
```

这几条命令分别回答：

- 有哪些 provider/profile
- 某个 provider 当前 capability 是什么
- 当前 repo 绑定了哪个 worker profile
- 实际会选哪个 backend 执行

## 4. Provider 配置文件

repo-local provider 配置位于：

```text
.ai/config/ai_provider.json
```

这个文件现在按“全局默认 + profiles + roles”解释：

- 顶层字段是全局默认值
- `profiles` 里放命名模型方案
- `roles.worker`、`roles.planner` 等只引用 profile id
- 多个角色可以共用一个 profile，也可以各自指向不同 profile
- 旧的 inline 角色对象仍兼容，但新配置建议直接写 profile 引用

推荐结构：

```json
{
  "default_profile": "shared-gemini",
  "provider": "gemini",
  "enabled": true,
  "model": "gemini-2.5-pro",
  "base_url": "https://generativelanguage.googleapis.com/v1beta",
  "api_key_environment_variable": "GEMINI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 45,
  "max_output_tokens": 500,
  "reasoning_effort": "low",
  "profiles": {
    "shared-gemini": {
      "provider": "gemini",
      "model": "gemini-2.5-pro",
      "base_url": "https://generativelanguage.googleapis.com/v1beta",
      "api_key_environment_variable": "GEMINI_API_KEY"
    },
    "planner-claude": {
      "provider": "claude",
      "model": "claude-sonnet-4-5",
      "base_url": "https://api.anthropic.com/v1",
      "api_key_environment_variable": "ANTHROPIC_API_KEY",
      "request_family": "messages_api"
    }
  },
  "roles": {
    "worker": "shared-gemini",
    "planner": "planner-claude"
  }
}
```

如果你只想统一用一个模型 API，就保留一个 profile，然后让所有角色都指向它。

如果你想按角色分模型，就让 `worker`、`planner` 等分别指向不同 profile。

### 4.1 Codex 作为 worker / planner

先设置环境变量：

```powershell
$env:OPENAI_API_KEY="your-key"
```

再把 `.ai/config/ai_provider.json` 配成一个单 profile：

```json
{
  "provider": "codex",
  "enabled": true,
  "model": "gpt-5-codex",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 45,
  "max_output_tokens": 500,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
```

然后绑定 repo 到 Codex worker profile：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider bind CARVES.Runtime codex-worker-trusted
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker select CARVES.Runtime
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider inspect codex
```

你应该看到：

- selected backend 指向 `codex_sdk`
- protocol family 显示 `sdk_bridge`

### 4.2 OpenAI 作为 worker

```powershell
$env:OPENAI_API_KEY="your-key"
```

`.ai/config/ai_provider.json` 单 profile 示例：

```json
{
  "provider": "openai",
  "enabled": true,
  "model": "gpt-5-mini",
  "base_url": "https://api.openai.com/v1",
  "api_key_environment_variable": "OPENAI_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 30,
  "max_output_tokens": 500,
  "reasoning_effort": "low",
  "organization": null,
  "project": null
}
```

### 4.3 Groq / DeepSeek 作为 OpenAI-compatible worker

如果 provider 提供的是 OpenAI-compatible `chat/completions`，例如 Groq 或 DeepSeek，配置方式仍然使用 `provider = "openai"`，但应显式声明：

- 对应 `base_url`
- 对应 API key 环境变量
- `request_family = "chat_completions"`

Groq 示例：

```powershell
$env:GROQ_API_KEY="your-key"
```

```json
{
  "provider": "openai",
  "enabled": true,
  "model": "llama-3.3-70b-versatile",
  "base_url": "https://api.groq.com/openai/v1",
  "api_key_environment_variable": "GROQ_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 30,
  "max_output_tokens": 500,
  "reasoning_effort": "low",
  "request_family": "chat_completions",
  "organization": null,
  "project": null
}
```

DeepSeek 示例：

```powershell
$env:DEEPSEEK_API_KEY="your-key"
```

```json
{
  "provider": "openai",
  "enabled": true,
  "model": "deepseek-chat",
  "base_url": "https://api.deepseek.com",
  "api_key_environment_variable": "DEEPSEEK_API_KEY",
  "allow_fallback_to_null": false,
  "request_timeout_seconds": 30,
  "max_output_tokens": 500,
  "reasoning_effort": "low",
  "request_family": "chat_completions",
  "organization": null,
  "project": null
}
```

### 4.4 Codex SDK / Codex CLI

Codex SDK worker：

```powershell
$env:OPENAI_API_KEY="your-key"
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider bind CARVES.Runtime codex-worker-trusted
```

本地 Codex CLI worker：

```powershell
codex --version
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider bind CARVES.Runtime codex-worker-local-cli
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker select CARVES.Runtime
```

如果 `codex` 不在 `PATH`，再显式设置：

```powershell
$env:CARVES_CODEX_CLI_PATH="C:\\path\\to\\codex.exe"
```

## 5. 一个最小可运行示例

下面是“启动 host -> 绑定 Codex -> inspect -> run task”的完整流程：

```powershell
$env:OPENAI_API_KEY="your-key"

dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host ensure --json
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- provider bind CARVES.Runtime codex-worker-trusted
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker select CARVES.Runtime
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- inspect task <task-id>
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- task run <task-id>
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
```

结束时：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host stop "finished operator session"
```

## 6. 常见问题

### Q1. 为什么 `task run` 提示先启动 host？

因为 delegated execution 的默认入口就是 resident host。先执行：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host ensure --json
```

然后按结果分流：

- `not_running` -> `host start`
- `host_session_conflict` -> `host reconcile --replace-stale --json`

### Q2. 我怎么确认当前到底会用哪个 worker？

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker select CARVES.Runtime
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- worker providers
```

重点看：

- `Selected backend`
- `Selected provider`
- `Protocol family`
- `Reason code`

### Q3. 为什么配置了 Codex，但还是没走 Codex？

逐项检查：

1. `.ai/config/ai_provider.json` 里 `provider` 是否是 `codex`
2. `enabled` 是否是 `true`
3. `OPENAI_API_KEY` 是否存在
4. repo 是否已绑定 `codex-worker-trusted`
5. `worker select CARVES.Runtime` 是否显示 `Allowed: true`

### Q4. 我只想看 host summary，不想记一堆命令怎么办？

最少记这三条：

```powershell
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host ensure --json
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- status --summary
dotnet run --project .\src\CARVES.Runtime.Host\Carves.Runtime.Host.csproj -- host stop "done"
```

## 7. 推荐阅读顺序

如果你刚开始操作这个 runtime，推荐按这个顺序：

1. 本文
2. `docs/guides/RUNTIME_OPERATOR_MANUAL.md`
3. `docs/guides/RUNTIME_OPERATOR_HOST_AND_LIFECYCLE_REFERENCE.md`
4. `docs/guides/RUNTIME_PROVIDER_AND_WORKER_REFERENCE.md`
5. `docs/runtime/remote-api-worker-architecture.md`
6. `docs/runtime/operator-actionability-contract.md`
