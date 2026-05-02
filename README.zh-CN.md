# CARVES Runtime

语言：[英文](README.md)

CARVES Runtime 是一个本地 AI Agent 治理 Runtime，用来启动、绑定、查看和检查项目里的 AI Agent 工作。

这个仓库是干净的公开源码快照。它不包含私有开发仓库历史、实时 task/runtime truth、Runtime Host 状态、Codex 状态、本地 artifact，或归档/checkpoint 历史。

## 状态

当前公开快照：

```text
0.6.2-beta source snapshot
```

这仍然是 beta 软件。它适合本地源码阅读、本地构建，以及有限范围的本地启动实验。它不是托管服务，不是已签名 release，不是完整 autonomous agent platform，也不是 API/SDK worker execution authority。

## Runtime 首次运行

从源码构建 Runtime CLI：

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

检查本地 CLI 和可见 gateway surface：

```bash
./carves help
./carves gateway status
```

把 CARVES Runtime 绑定到目标项目：

```bash
./carves up <target-project>
```

安全的 agent 入口是 [START_CARVES.zh-CN.md](START_CARVES.zh-CN.md)。把这个文件的绝对路径交给你的编码 agent，然后按目标项目中生成的 `CARVES_START.md` / `.carves/carves agent start --json` 指令继续。

## Runtime 治理能力

这个源码快照包含这些 Runtime 治理能力：

- Guard：Runtime 的本地 diff patch boundary check。入口见 [docs/guard/README.zh-CN.md](docs/guard/README.zh-CN.md)、[新手指南](docs/guard/wiki/guard-beginner-guide.zh-CN.md)、[GitHub Actions 指南](docs/guard/wiki/github-actions.zh-CN.md)。
- Handoff：Runtime 的 session continuity packet，用于在编码会话之间携带有限上下文。
- Audit：Runtime 的 evidence readback，用于读取本地 Guard decisions 和 Handoff packets。
- Shield：Runtime 的本地治理 self-check，用 summary evidence 做本地评估。
- Matrix：Runtime 的 proof 和 trial lane，用来检查 Guard、Handoff、Audit、Shield 能协同工作，但不改变 Runtime-first 定位。

中文文档总索引在 [docs/INDEX.zh-CN.md](docs/INDEX.zh-CN.md)。

能力命令示例：

```bash
carves test demo
carves test agent
carves-guard init
carves-audit evidence --json --output .carves/shield-evidence.json
carves-shield evaluate .carves/shield-evidence.json
carves-matrix trial plan --workspace ./carves-trials/latest
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native --configuration Release --json
carves-matrix verify artifacts/matrix/native --json
```

`carves test demo` 是 Runtime CLI 的本地 smoke/trial path，不是第一入口。并行的 Windows package 入口见 [docs/matrix/agent-trial-node-windows-playable-quickstart.md](docs/matrix/agent-trial-node-windows-playable-quickstart.md)。

Linux-native Matrix 首次运行不需要 PowerShell。完整 release proof 仍可通过以下命令运行：

```bash
pwsh ./scripts/matrix/matrix-proof-lane.ps1
```

CI 示例在 [.github/workflows/matrix-proof.yml](.github/workflows/matrix-proof.yml)。当前限制见 [docs/matrix/known-limitations.md](docs/matrix/known-limitations.md)。Matrix 不是模型安全 benchmark，不评价模型安全，也不会自动回滚任意写入。

## 免责声明

CARVES.AI 按现状提供。你需要自行审查、批准和验证任何通过 CARVES.AI 采取的动作。不要在敏感、生产、受监管、保密、客户拥有或业务关键系统上使用它，除非你已经具备自己的授权、备份、安全控制和恢复方案。

见 [DISCLAIMER.zh-CN.md](DISCLAIMER.zh-CN.md)。

## 要求

- .NET SDK 10.0
- Git
- PowerShell 7+，用于 PowerShell 脚本

## 从源码构建

```bash
dotnet build CARVES.Runtime.sln --configuration Release
```

从源码运行 CLI：

```bash
./carves help
./carves gateway status
```

Windows：

```powershell
.\carves.ps1 help
.\carves.ps1 gateway status
```

## 在项目中启动 CARVES

安全的 agent 入口是：

```text
START_CARVES.md
```

把这个文件的绝对路径交给你的编码 agent，并要求它先阅读。agent 应运行：

```bash
<carves-root>/carves up <target-project>
```

然后打开目标项目，并按生成的 `CARVES_START.md` / `.carves/carves agent start --json` 指令继续。

## 这个公开快照不包含

- 私有开发仓库的 git 历史。
- 实时 task、runtime、memory、artifact，以及类似控制面 truth。
- `.carves-platform` Host/runtime 状态。
- Codex 本地状态。
- 构建输出、日志、trial、本地 package，以及生成的 release archive。
- 内部 archive/checkpoint phase 历史。

## 当前边界

- dashboard 还不是打磨完成的产品 surface。
- provider-backed API/SDK worker execution 没有在这个快照中开放。
- Runtime CARD、TaskGraph 和 Host truth internals 是 maintainer 概念，不是公开用户首次运行的前置要求。
- 本地 build 成功不等于签名或 hosted-verification claim。
- release tag、GitHub Release、NuGet.org 发布和 package signing 仍然是独立的 operator-owned 步骤。

## License

Apache-2.0。见 [LICENSE](LICENSE)。
