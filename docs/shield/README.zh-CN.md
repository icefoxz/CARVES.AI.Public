# CARVES Shield

语言：[英文](README.md)

CARVES Shield 是 CARVES Runtime 面向仓库的 local-first AI governance self-check 能力。

它帮助项目回答一个实际问题：

```text
这个仓库能否展示证据，证明 AI-assisted code work 是有边界、交接清楚、可审计的？
```

Shield v0 有意保持简单：

- `carves-shield evaluate` 读取 `shield-evidence.v0` summary evidence。
- `carves-shield badge` 渲染本地静态 SVG badge。
- 组合式 `carves` 工具仍然暴露兼容 wrapper：`carves shield ...`。
- GitHub Actions proof lane 会写本地 artifact，供 pull request inspection。
- 来自 Audit 的 Guard CI evidence 是 workflow 文本 heuristic evidence，不证明 hosted CI service 已经运行。
- 默认不会上传 source code、raw diff、prompt、模型回复、secret、credential 或 private file payload。
- v0 输出是 self-check。它不是 model safety benchmark、hosted verification、public ranking、certification、source review、semantic correctness proof 或 operating-system sandboxing。

## 从这里开始

- [Shield Wiki 首页](wiki/README.zh-CN.md)
- [新手教程：从零跑第一次 Shield 自检](wiki/shield-beginner-guide.zh-CN.md)
- [术语表：每个关键字是什么意思](wiki/glossary.zh-CN.md)
- [流程图：本地、自检、CI 怎么走](wiki/workflow.zh-CN.md)
- [证据模板：如何准备 shield-evidence.v0](wiki/evidence-starter.zh-CN.md)
- [GitHub Actions 接入](wiki/github-actions.zh-CN.md)
- [Badge 解读与发布](wiki/badge.zh-CN.md)

## Stable v0 Commands

```powershell
carves-shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]
carves-shield badge <evidence-path> [--json] [--output <svg-path>]
carves-shield challenge <challenge-pack-path> [--json]
```

兼容 wrapper：

```powershell
carves shield evaluate <evidence-path> [--json] [--output <lite|standard|combined>]
carves shield badge <evidence-path> [--json] [--output <svg-path>]
carves shield challenge <challenge-pack-path> [--json]
```

## 分数含义

Shield Standard 保持三个维度分开：

```text
G  Guard    output governance
H  Handoff  input governance
A  Audit    history governance
```

每个维度使用这个可见 scale：

```text
0      gray    not enabled / no evidence
C      red     critical failure
1-4    white   basic configured
5-7    yellow  disciplined
8-9    green   sustained or strong
```

Shield Lite 把同一份 evidence 转成一个 0-100 的 workflow governance self-check score，方便分享。Lite 更容易读，但 Standard 是细节来源。两种模式都不评价 AI model safety。
