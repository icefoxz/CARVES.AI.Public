# 流程图：本地、自检、CI 怎么走

Shield v0 的核心原则是 local-first。证据先在本地或 CI 里生成，评估也在本地完成。

## 本地 self-check

```mermaid
flowchart TD
    A["开发者准备 .carves/shield-evidence.json"] --> B["carves shield evaluate"]
    B --> C["Standard G/H/A"]
    B --> D["Lite 0-100 score"]
    C --> E["人工读取弱项"]
    D --> E
    E --> F["修正证据或治理配置"]
```

本地流程适合第一次试用。你可以先只填 Guard 证据，Handoff 和 Audit 暂时保持 `enabled: false`。

## Badge 输出

```mermaid
flowchart TD
    A["shield-evidence.v0"] --> B["carves shield badge"]
    B --> C["shield-badge.svg"]
    B --> D["shield-badge.v0 JSON metadata"]
    C --> E["README 或 PR artifact"]
    D --> F["CI artifact 或审查记录"]
```

Badge 只展示 self-check 结果。它不能被写成认证。

## GitHub Actions proof

```mermaid
flowchart TD
    A["pull_request or push"] --> B["checkout repository"]
    B --> C["install CARVES CLI"]
    C --> D["carves shield evaluate"]
    D --> E["carves shield badge"]
    E --> F["upload shield artifacts"]
    F --> G["reviewer inspects JSON and SVG"]
```

CI proof 的价值是让结果可重复。每个 pull request 都能看到同一套 evidence 是否还能通过。

## 隐私边界

```mermaid
flowchart LR
    A["source code"] -. "not uploaded by default" .-> X["blocked"]
    B["raw diff"] -. "not uploaded by default" .-> X
    C["prompts or model responses"] -. "not uploaded by default" .-> X
    D["secrets or credentials"] -. "not uploaded by default" .-> X
    E["summary evidence"] --> F["local Shield evaluator"]
```

默认路径只处理 summary evidence。任何未来 richer evidence 都必须显式 opt-in，并说明会发送什么。
