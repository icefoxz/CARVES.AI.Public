# Project Boundary

## 人类控制边界

以下内容必须由人类定义或批准：

- 产品目标
- 架构方向
- 运行时环境
- 外部依赖
- 把 `SUGGESTED` 升级成 `PENDING`
- merge / rebase / release 决策
- 是否允许写入 `.ai/memory/`

## 仓库范围边界

AI 可以：

- 实现已批准任务
- 添加直接需要的日志、测试、验证
- 做为完成当前任务所必需的小范围局部重构
- 更新任务状态与 review 记录

AI 不可以：

- 静默改变核心架构方向
- 未经批准引入新依赖
- 在非 `MEMORY_UPDATE` 任务中改写 `.ai/memory/`
- 把发现的任务自动执行
- 越过 Scope Anchor 修改无关模块
- 因为“现有代码就这样”而继续扩散架构错误

## Official Truth Root Policy

以下路径是 Runtime official truth 或 host-owned control truth，不是普通 worker/adapter workspace 输出：

| Path | Classification | Allowed mutation channel | Unauthorized direct mutation |
| --- | --- | --- | --- |
| `.ai/tasks/` | `task_truth` | plan/task/review Runtime commands and markdown sync | block before writeback |
| `.ai/memory/` | `memory_truth` | governed memory update or explicit operator-approved memory edit | block before writeback |
| `.ai/artifacts/reviews/` | `review_truth` | planner review artifact creation and review lifecycle commands | block before writeback |
| `.carves-platform/` | `platform_truth` | Runtime platform registry/governance commands | block before writeback |

Denied roots:

- `.git/`
- `.vs/`
- `.idea/`
- secret-like paths such as `.env`, `*.pfx`, `*.snk`, `secrets.json`, or paths under `secrets/`

Runtime-owned commands may update protected roots through governed channels. External agents and adapters must return evidence, patches, or replan requests instead of directly mutating these roots.

## 双层架构边界

### 目标产品代码

使用 CARVES 解释和约束：

- C / A / R / V / E / S
- Scope Anchor
- Decision Register
- 绿灯 Lint
- 结构化 patch

### 控制平面代码

使用模块职责约束：

- planner
- worker
- taskgraph
- safety
- code_understanding
- refactoring
- orchestration

不要把控制平面代码误当成一个本身按 `Controller/Actor/Record/...` 目录布局的业务项目。

## 努力边界

原则：

- 选择大于努力
- 证据大于猜测
- 更小、更清晰、更可验证的前进一步，优于大而模糊的推进

当 review horizon 被触发时，必须回到 Planner Review。
