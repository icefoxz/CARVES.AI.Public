# CARVES.Guard 流程图

语言：[英文](workflow.en.md)

这页用流程图说明 CARVES.Guard 应该放在什么位置，以及 `allow`、`review`、`block` 后应该怎么做。

## 本地开发流程

```mermaid
flowchart TD
    A["人提出一个小需求"] --> B["AI coding tool 修改代码"]
    B --> C["git working tree 出现 patch"]
    C --> D["运行 carves guard check"]
    D --> E{"Guard decision"}
    E -->|allow| F["进入正常人工 review 或 merge"]
    E -->|review| G["人检查 warning 和 evidence"]
    E -->|block| H["不要合并；让 AI 缩小或修正 patch"]
    G --> I{"人是否接受风险"}
    I -->|接受| F
    I -->|不接受| H
    H --> B
```

## Decision 生成流程

```mermaid
flowchart LR
    A["git status / diff"] --> B["changed files"]
    C[".ai/guard-policy.json"] --> D["policy"]
    B --> E["path checks"]
    B --> F["budget checks"]
    B --> G["dependency checks"]
    B --> H["shape checks"]
    D --> E
    D --> F
    D --> G
    D --> H
    E --> I{"findings"}
    F --> I
    G --> I
    H --> I
    I -->|violation exists| J["block"]
    I -->|warnings only| K["review"]
    I -->|no findings| L["allow"]
```

## GitHub Actions 流程

```mermaid
flowchart TD
    A["Pull request opened or updated"] --> B["GitHub Actions starts"]
    B --> C["Checkout PR head and fetch base branch"]
    C --> D["Materialize PR diff into working tree"]
    D --> E["Install or locate carves command"]
    E --> F["Run carves guard check --json"]
    F --> G{"Exit code"}
    G -->|0 allow| H["CI check passes"]
    G -->|1 review/block| I["CI check fails"]
    I --> J["Developer reads JSON or explain output"]
    J --> K["Shrink patch, add tests, or fix policy violation"]
```

## 推荐团队规则

刚开始接入时，建议：

- `allow`：可以进入正常人工 review。
- `review`：CI 可以先 fail，让人主动看 warning；团队成熟后再决定是否允许 review 作为非阻塞。
- `block`：必须修正，不进入 merge。

如果你的团队担心太严格，可以先在 CI 里只上传 JSON 报告，不阻塞合并。等团队熟悉 `rule_id` 后，再把 `review` 和 `block` 设为 required check。

## Guard 的正确位置

```text
AI 负责写 patch。
Guard 负责检查 patch 的边界。
人负责最终语义 review。
```

Guard 不判断业务逻辑一定正确，也不代替测试。它先挡住明显越界、过大、缺测试、碰敏感路径的 patch。
