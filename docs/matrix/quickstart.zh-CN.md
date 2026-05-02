# CARVES Matrix 新手快速开始

语言：[En](quickstart.en.md)

CARVES Matrix 会运行本地 Guard -> Handoff -> Audit -> Shield local consistency proof 链路，并写出可以后续验证的 summary-only proof bundle。

这份指南用于第一次跑通 native first-run local workflow self-check、查看 summary-only proof bundle、解读 Shield badge，并理解当前限制。

如果你想让自己的 AI agent 跑一个标准本地任务并获得本地 Agent Trial 分数，请看 [Agent Trial V1 本地试玩快速开始](agent-trial-v1-local-quickstart.md)。

## 前置条件

- 能构建本仓库 target framework 的 .NET SDK。
- `PATH` 上可用的 Git。
- 本仓库的本地 checkout。
- 只有在需要 full release proof 或 packaged-install smoke lane 时才需要 PowerShell 7 或更新版本。

本 quickstart 不需要托管服务、NuGet.org push、源码上传、raw diff 上传、prompt 上传、model response 上传、secret 上传或 credential 上传。该 bundle 不建立 producer identity、signature、transparency log、hosted verification、certification、benchmarking、OS sandbox 或 semantic correctness proof。

## 构建 Native Matrix CLI

在仓库根目录运行：

```bash
dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release
```

这会构建本地 Matrix CLI 项目，以及它依赖的 Guard、Handoff、Audit、Shield runner。

## 运行 Native Minimal Proof

先运行 Linux-native first-run proof：

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json
```

这会创建一个 bounded 临时外部 git 仓库，通过本地 .NET runner 依次运行 Guard、Handoff、Audit、Shield，然后把 summary-only Matrix artifacts 写到 `artifacts/matrix/native-quickstart`。这条路径不调用 `pwsh` 或 `scripts/matrix/*.ps1`。

预期 native 顶层输出：

```text
artifacts/matrix/native-quickstart/matrix-proof-summary.json
artifacts/matrix/native-quickstart/matrix-artifact-manifest.json
```

预期 native project 输出：

```text
artifacts/matrix/native-quickstart/project/decisions.jsonl
artifacts/matrix/native-quickstart/project/handoff.json
artifacts/matrix/native-quickstart/project/shield-evidence.json
artifacts/matrix/native-quickstart/project/shield-evaluate.json
artifacts/matrix/native-quickstart/project/shield-badge.json
artifacts/matrix/native-quickstart/project/shield-badge.svg
artifacts/matrix/native-quickstart/project/matrix-summary.json
```

## 验证 Native Bundle

只验证 artifact bundle，而不重新运行 Guard、Handoff、Audit、Shield 或任何 proof script：

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- verify artifacts/matrix/native-quickstart --json
```

`verify` 是 Linux-native public recheck path。它输出 `matrix-verify.v0`，只读取已有本地文件，不调用 `pwsh`、Matrix proof scripts、Guard、Handoff、Audit 或 Shield。

public proof summary 使用 closed public contract。未知 public 字段会让验证失败；native summary 字段、full release summary 字段和 Shield evaluation 字段只有经过 manifest-bound verified reads 后才会被信任。

Public JSON examples 位于 `docs/matrix/examples/`。文件名以 `.schema-example.json` 结尾的是 schema examples, not runnable verification bundles；它们只用于查看 public shape，不应作为 `verify` 输入。真正可运行的示例 bundle 如果出现，会使用 `.runnable-bundle` 目录名，并包含 manifest 引用的全部 artifact 文件。

重要字段：

- `status`: 只有 bundle 通过验证时才是 `verified`。
- `proof_capabilities`: 该 proof bundle 的 lane、backend、coverage 和环境要求。
- `reason_codes`: 面向自动化的稳定失败家族，例如 `missing_artifact`、`hash_mismatch`、`schema_mismatch`、`privacy_violation`、`unverified_score` 或 `unsupported_version`。
- `issues`: 精确 verifier 诊断。
- `trust_chain_hardening.gates_satisfied`: 当前本地 verifier gates 是否通过。

退出码：

- `0`: bundle verified。
- `1`: verification failed。
- `2`: usage 或 argument error。

## Installed Tool 形式

安装 `carves-matrix` 后，同一条 native first-run path 可以写成：

```bash
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json
carves-matrix verify artifacts/matrix/native-quickstart --json
```

## Full Release Proof Lane

只有在你需要完整 project-mode + packaged-install release evidence 时，才运行 PowerShell lane：

```powershell
pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot artifacts/matrix/quickstart-release -Configuration Release
```

该 full release proof 会写出：

```text
artifacts/matrix/quickstart-release/project-matrix-output.json
artifacts/matrix/quickstart-release/packaged-matrix-output.json
artifacts/matrix/quickstart-release/matrix-proof-summary.json
artifacts/matrix/quickstart-release/matrix-artifact-manifest.json
```

PowerShell scripts 是 release proof 与 packaged smoke lane，不是 Linux first-run requirement。

## 可选：Packaged Smoke

如果你希望在公开 package 发布前，在当前 shell 里直接使用 standalone commands，可以保留 packaged smoke 的临时安装目录：

```powershell
$workRoot = Join-Path ([System.IO.Path]::GetTempPath()) "carves-matrix-tools"
pwsh ./scripts/matrix/matrix-packaged-install-smoke.ps1 -WorkRoot $workRoot -ArtifactRoot artifacts/matrix/quickstart-packaged -Configuration Release -Keep
$env:PATH = "$(Join-Path $workRoot "tool")$([System.IO.Path]::PathSeparator)$env:PATH"
carves-matrix help
```

该脚本会构建本地 `.nupkg` 文件，并从本地 package directory 安装这些 dotnet tools：

| Package | Command |
| --- | --- |
| `CARVES.Guard.Cli` | `carves-guard` |
| `CARVES.Handoff.Cli` | `carves-handoff` |
| `CARVES.Audit.Cli` | `carves-audit` |
| `CARVES.Shield.Cli` | `carves-shield` |
| `CARVES.Matrix.Cli` | `carves-matrix` |

这些本地 package 不代表已经发布到 NuGet.org。

## 解读 Shield Badge

native proof 会写出：

```text
artifacts/matrix/native-quickstart/project/shield-badge.svg
artifacts/matrix/native-quickstart/project/shield-badge.json
```

可见 badge 文本来自 Shield Lite。badge metadata 会保留 Shield Standard dimensions，例如 `G4.H3.A5` 或更高，具体取决于本地证据。

应该把它理解成本地 workflow governance self-check：

- G = Guard evidence。
- H = Handoff evidence。
- A = Audit evidence。
- Lite score 和 band 用于快速阅读。
- `self_check=true` 和 `certification=false` 表示 badge 不是认证。

准确术语定义见 [Shield 术语表](../shield/wiki/glossary.zh-CN.md)。

不要把 badge 写成 certified、verified safe、model safety rating 或 security audit。

## 在自己的仓库里手动运行链路

安装 tools 后，在你想检查的 git 仓库里运行：

```powershell
carves-guard init
carves-guard check --json
carves-handoff draft --json
carves-audit summary --json
carves-audit timeline --json
carves-audit evidence --json --output .carves/shield-evidence.json
carves-shield evaluate .carves/shield-evidence.json --json --output combined
carves-shield badge .carves/shield-evidence.json --json --output docs/shield-badge.svg
carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/local --configuration Release --json
carves-matrix verify artifacts/matrix/local --json
```

如果 Guard 返回 `review` 或 `block`，仍然需要正常的人类 review。Matrix 记录 local consistency proof；它不决定 patch 是否应该 merge。

## 产品入口

- Guard: [docs/guard/README.md](../guard/README.md) 和 [Guard quickstart](../guard/quickstart.zh-CN.md)
- Handoff: [docs/handoff/README.md](../handoff/README.md) 和 [Handoff quickstart](../handoff/quickstart.zh-CN.md)
- Audit: [docs/audit/README.md](../audit/README.md) 和 [Audit quickstart](../audit/quickstart.zh-CN.md)
- Shield: [docs/shield/README.md](../shield/README.md)、[badge 解读](../shield/wiki/badge.zh-CN.md) 和 [Shield Lite starter challenge](../shield/lite-challenge-quickstart.md)
- Matrix: [docs/matrix/README.md](README.md)、[artifact manifest](matrix-artifact-manifest-v0.md)、[GitHub Actions proof](github-actions-proof.md)、[large-log stress limits](large-log-stress.md) 和 [known limitations](known-limitations.md)

## 当前限制

Matrix 是 local-first 和 summary-only。当前 proof 与 verifier 不提供：

- producer identity；
- signature；
- transparency-log backing；
- model safety benchmarking；
- hosted verification；
- public certification；
- public leaderboard ranking；
- operating-system sandboxing；
- syscall interception；
- real-time file write prevention；
- network isolation；
- automatic rollback；
- semantic source-code correctness proof；
- source code upload；
- raw diff upload；
- prompt 或 model response upload；
- secret 或 credential upload。

大型 Guard decision history 通过 Audit 的 1000 行 bounded tail window 读取。超过 131072 bytes 的单条 Guard decision record 会被跳过，并产生显式 diagnostics。Matrix artifact manifest 会保存大型 artifact 的 hash 和 size，但不会把大型 artifact bytes 内联进 manifest。
