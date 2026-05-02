# CARVES Matrix

语言：[英文](README.md)

CARVES Matrix 是 CARVES Runtime 的 proof 和 trial lane。它把 Runtime 治理能力定位为 AI 编码工作流治理的本地 self-check 和本地一致性 proof，而不是模型安全 benchmark。

它记录这些 Runtime 能力能在外部 git 仓库中协同工作：

```text
Guard -> Handoff -> Audit -> Shield
```

Matrix 不做第五个治理决策，也不是第五个 safety engine。它不拥有 Guard policy logic、Handoff packet truth、Audit discovery logic 或 Shield scoring。它只编排本地 consistency proof lane，并记录该链条是否产出了预期的 summary-only local artifacts。

## 公开首次运行

第一次下载并运行时，先使用 Runtime 拥有的 `carves test` wrapper，再学习 Matrix artifact path：

```powershell
carves test demo
carves test agent
carves test package --output ./carves-agent-trial-pack
carves test result
carves test reset
carves test verify
```

源码 checkout 中等价的首个命令是：

```powershell
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
```

`carves test demo` 是全自动本地环境检查。`carves test agent` 会准备真实 agent-assisted run。两者使用同一套 Matrix Agent Trial collector、scorer、verifier、manifest、result card 和 history logic，默认输出在 `./carves-trials/`。

Windows 用户可以双击仓库或 release folder 中的 `Start-CARVES-Agent-Test.cmd`。launcher 会运行同样的 `carves test` path，并在关闭前暂停，方便用户读取 score summary、result-card path 或 setup diagnostic。

本地分数衡量一次本地 task run 的 reviewability、traceability、explainability、report honesty、constraint adherence 和 reproducibility evidence。它不证明 certification、leaderboard eligibility、hosted verification、producer identity、OS sandboxing、semantic correctness 或 local anti-cheat。本机所有者仍然可以篡改本地过程。

## Runtime 兼容性

独立能力 CLI 仍然可用：

- `carves-guard`
- `carves-handoff`
- `carves-audit`
- `carves-shield`
- `carves-matrix`

Runtime `carves` 工具仍然是主要公开入口和 reference host。能力面命令会尽可能委托给独立 runners：

- `carves guard ...`
- `carves handoff ...`
- `carves audit summary|timeline|explain|evidence ...`
- `carves shield ...`
- `carves matrix ...`

Runtime 内部治理命令与公开能力故事分开。用户不需要理解 Runtime task/card governance 概念，也能运行 Matrix local workflow self-check。

## 从这里开始

- [新手快速开始](quickstart.zh-CN.md)
- [Agent Trial local quickstart](agent-trial-v1-local-quickstart.md)
- [Agent Trial local user smoke](agent-trial-v1-local-user-smoke.md)
- [Node Windows Playable Agent Trial quickstart](agent-trial-node-windows-playable-quickstart.md)
- [Known limitations](known-limitations.md)
- [GitHub Actions proof](github-actions-proof.md)
- [Packaged install Matrix](packaged-install-matrix.md)

## 常用命令

```powershell
carves test demo
carves test agent
carves test package --output ./carves-agent-trial-pack
carves test result
carves test verify
Start-CARVES-Agent-Test.cmd
carves-matrix proof --lane native-minimal --json
carves-matrix verify <artifact-root> --json
```

当你需要最小 Linux-native 本地一致性 proof lane 时，优先使用：

```powershell
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json
```

当你已经有 summary-only proof bundle 时，优先使用 `verify`。它是 Linux-native public artifact recheck path：在 .NET Matrix verifier 内运行，读取已有本地文件，不调用 `pwsh`、`scripts/matrix/*.ps1`、Guard、Handoff、Audit、Shield 或 Matrix proof lane：

```powershell
carves-matrix verify artifacts/matrix/native-quickstart --json
```
