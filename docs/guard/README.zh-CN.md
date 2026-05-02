# CARVES.Guard 文档

语言：[英文](README.md)

CARVES.Guard 是 CARVES Runtime 的本地 patch boundary check 能力。这里不解释其它尚未公开稳定的 CARVES 概念，只说明 Guard surface：它检查什么、为什么重要、怎么接入、怎么读结果、怎么接到 GitHub Actions。

## 推荐阅读顺序

1. [五分钟上手](quickstart.zh-CN.md)
2. [新手教程](wiki/guard-beginner-guide.zh-CN.md)
3. [关键术语表](wiki/glossary.zh-CN.md)
4. [工作流和流程图](wiki/workflow.zh-CN.md)
5. [Policy 模板](wiki/guard-policy-starter.zh-CN.md)
6. [GitHub Actions 接入](wiki/github-actions.zh-CN.md)
7. [可复制 GitHub Actions 模板](github-actions-template.md)

## 最短使用路径

```powershell
carves-guard init
carves-guard check --json
carves-guard audit
carves-guard report
carves-guard explain <run-id>
```

兼容入口仍然可用：

```powershell
carves guard init
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

## Standalone `run` 边界

`carves-guard run <task-id>` 是 Runtime-host 命令。Standalone Guard 可以初始化 policy、检查 git diff、audit 本地 decisions、生成 report，并解释已记录 run。首次 standalone 检查请使用 `carves-guard check`。

standalone help 会说明 `run` 需要 CARVES Runtime host；直接在 standalone 中调用 `run` 会返回清晰的 Runtime-host-required 信息。

## 边界

CARVES.Guard 是 patch admission gate。它检查已经出现在 git working tree 里的 patch，并返回 `allow`、`review` 或 `block`。

Guard decision records 写入 `.ai/runtime/guard/decisions.jsonl`，使用本地 file-exclusive append lock 和 bounded retention。这可以保护 JSONL 文件免于同仓库普通本地并发写入冲突，但它仍然只是本地 sidecar log，不是远程 registry，也不是 tamper-proof ledger。

Exit codes：

- `init`：policy 写入成功为 `0`；写入被拒绝或失败为 `1`；非法用法为 `2`。
- `check`：只有 `allow` 返回 `0`；`review` 和 `block` 返回 `1`；非法用法返回 `2`。
- `run`：实验性 task-aware path；只有 `allow` 返回 `0`，否则返回 `1`；非法用法返回 `2`。
- `audit`：能生成本地 decision readback 时返回 `0`。
- `report`：能生成 report 时返回 `0`；policy load issue 会出现在 payload 中。
- `explain`：找到 run id 时返回 `0`；缺失返回 `1`；非法用法返回 `2`。

它不是操作系统沙箱，不承诺实时阻止写入、拦截 syscall、隔离网络、虚拟化文件系统或自动回滚任意写入。
