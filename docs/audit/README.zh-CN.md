# CARVES Audit

语言：[En](README.md)

CARVES Audit 是 CARVES Runtime 的只读证据发现和 readback 能力。

它读取 Guard decisions 和 Handoff packets，然后生成 summary、timeline、explain readback，以及安全的 `shield-evidence.v0` summary evidence。

公开 CLI surface 是 `carves-audit`，公开 service type 是 `AuditService`。

Audit 对大型 Guard JSONL history 只在内存中保留有限的最近尾部记录。默认尾部是 1,000 条。

Audit evidence 是保守的。它报告观察到的记录，但不会从这些记录中虚构更高信任级别的 claim。除非存在明确来源证据，生成的 Shield evidence 会把 append-only proof、explain coverage、summary report、change report 和 failure-pattern report 字段保持为 false 或 zero。

## 从这里开始

- [快速开始](quickstart.zh-CN.md)

## 默认输入

```text
.ai/runtime/guard/decisions.jsonl
.ai/handoff/handoff.json
```

在仓库根目录运行：

```powershell
carves-audit summary --json
carves-audit timeline --json
carves-audit evidence --json --output .carves/shield-evidence.json
```

生成的 evidence output 必须留在仓库内，并避开受保护 truth 路径，例如 `.git/`、`.ai/tasks/`、`.ai/memory/`、`.ai/runtime/guard/` 和 `.ai/handoff/`。优先使用 `.carves/shield-evidence.json` 或 `artifacts/` 路径。

## Audit 做什么

- 发现默认 Guard decision 输出。
- 发现默认 Handoff packet。
- 汇总 allow/review/block decisions。
- 读取 Handoff readiness metadata。
- 解释 Guard run id 或 Handoff id。
- 输出 privacy-safe Shield evidence。
- 用简单 workflow 文本 heuristic 检测 Guard GitHub Actions 使用情况；这不是 hosted CI verification service。

## Audit 不做什么

- 不做 Shield scoring。
- 不渲染 badge。
- 不证明 append-only log。
- 不自动声称每个 block/review decision 都有 explain coverage。
- 没有明确 report evidence 时，不声称 summary/change/failure-pattern report artifact 存在。
- 不上传 source。
- 不收集 raw diff。
- 不收集 prompt 或模型回复。
- 不修改 Guard/Handoff。
- 不做 certification claim。
