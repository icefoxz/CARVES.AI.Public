# CARVES.Guard 术语表

语言：[En](glossary.en.md)

这份术语表解释 CARVES.Guard 文档和命令输出里的关键字。新手遇到不懂的词，优先查这里。

## 核心概念

| 关键字 | 意义 |
| --- | --- |
| CARVES.Guard | 给 AI 代码改动使用的 patch admission gate。它不写代码，只检查 patch 能不能进入 review 或 merge。 |
| AI coding tool | 写代码的 AI 工具，例如 Claude Code、Cursor、Copilot、Codex 或其它工具。Guard 不替代它们。 |
| patch | 一次尚未合并的代码改动集合。可以理解为 git 里当前这批变更。 |
| diff | git 看到的具体差异，例如新增、删除、修改了哪些行。 |
| working tree | 你的本地项目目录当前状态。`guard check` 默认检查这里还没有提交的改动。 |
| admission gate | 进入下一步之前的门禁。Guard 的门禁位置是在 patch 进入 review 或 merge 之前。 |
| policy | 项目写给 Guard 的规则集合。规则告诉 Guard 哪些路径能改、预算是多少、什么情况要 review 或 block。 |
| `.ai/guard-policy.json` | 默认 policy 文件路径。外部项目只需要这个 Guard policy 文件。 |

## Decision 相关词

| 关键字 | 意义 |
| --- | --- |
| decision | Guard 对一次 patch 的最终裁决。当前只有 `allow`、`review`、`block`。 |
| `allow` | patch 符合 policy，可以进入正常人工 review 或 merge 流程。它不等于代码一定正确。 |
| `review` | patch 没有触发硬拦截，但有风险点需要人看。默认情况下应当先人工确认。 |
| `block` | patch 违反硬规则，不应该进入团队认可的 review 或 merge path。 |
| violation | 触发 `block` 的问题。只要有 violation，最终 decision 就是 `block`。 |
| warning | 触发 `review` 的问题。没有 violation 但有 warning 时，最终 decision 是 `review`。 |
| `rule_id` | 触发规则的稳定编号，例如 `budget.max_changed_files`。排查问题时先看它。 |
| evidence | Guard 为什么做出裁决的证据，例如 `12 files changed; budget is 5`。 |
| evidence ref | 某条 evidence 的引用编号，方便 `explain` 输出和日志追踪。 |
| `run_id` | 某次 Guard 检查的唯一编号。用它可以运行 `carves guard explain <run-id>`。 |

## Command 相关词

| 关键字 | 意义 |
| --- | --- |
| `carves guard check` | 检查当前 git patch，输出 allow/review/block。 |
| `--json` | 让命令输出机器可读 JSON，适合脚本和 CI。 |
| `--repo-root` | 当你不在项目根目录运行命令时，用它指定目标 git repo。 |
| `--policy` | 指定 policy 文件路径。默认是 `.ai/guard-policy.json`。 |
| `--base` | 指定 git diff 的 base ref。当前 beta 仍以 working tree 中已经 materialized 的 patch 为主。 |
| `--head` | 指定 git diff 的 head ref。CI 场景通常先把 PR diff materialize 到 working tree，再运行 check。 |
| `carves guard audit` | 查看最近的 Guard 决策记录。 |
| `carves guard report` | 查看当前 policy 状态和最近 allow/review/block 汇总。 |
| `carves guard explain` | 按 `run_id` 展开某一次 decision 的原因、文件、证据和规则。 |

## Policy 相关词

| 关键字 | 意义 |
| --- | --- |
| `schema_version` | policy 格式版本。当前 v1 使用 `1`。 |
| `policy_id` | 你的 policy 名字。建议能看出项目和用途，例如 `webapp-guard-policy`。 |
| `path_policy` | 路径规则。定义哪些路径允许改，哪些路径受保护。 |
| `allowed_path_prefixes` | AI patch 可以触碰的路径前缀。例如 `src/`、`tests/`。 |
| `protected_path_prefixes` | 敏感路径。触碰这些路径通常应当 `block`。`.git/` 会被 Guard 自动保护。 |
| `outside_allowed_action` | patch 碰到 allowed path 之外的文件时怎么处理：`review` 或 `block` 最常见。 |
| `protected_path_action` | patch 碰到 protected path 时怎么处理。新手应使用 `block`。 |
| `change_budget` | 变更预算。限制一次 patch 最多改多少文件、增删多少行。 |
| `max_changed_files` | 一次 patch 最多允许改变的文件数。 |
| `max_total_additions` | 一次 patch 最多允许新增的总行数。 |
| `max_total_deletions` | 一次 patch 最多允许删除的总行数。 |
| `max_file_additions` | 单个文件最多允许新增的行数。 |
| `max_file_deletions` | 单个文件最多允许删除的行数。 |
| `max_renames` | 一次 patch 最多允许 rename 的文件数。 |
| `dependency_policy` | 依赖规则。检查 manifest 和 lockfile 是否一起变化。 |
| manifest | 依赖清单，例如 `package.json`、`*.csproj`。 |
| lockfile | 锁定依赖版本的文件，例如 `package-lock.json`、`packages.lock.json`。 |
| `change_shape` | patch 形状规则。检查 rename、delete、generated output、源码测试配套等。 |
| generated path | 生成产物目录，例如 `dist/`、`build/`、`coverage/`。 |
| `source_path_prefixes` | 源码路径，例如 `src/`。 |
| `test_path_prefixes` | 测试路径，例如 `tests/`。 |
| `missing_tests_action` | 改源码但没改测试时怎么处理。新手建议先 `review`，稳定后再改 `block`。 |
| `decision.fail_closed` | 出错时是否保守失败。建议保持 `true`：读不到 git diff 或 policy 时，不允许放行。 |
| `review_is_passing` | `review` 是否算通过。v1 要求 `false`，表示 review 不是自动通过。 |
| `emit_evidence` | 是否输出证据。v1 要求 `true`，方便解释和审计。 |

## GitHub Actions 相关词

| 关键字 | 意义 |
| --- | --- |
| CI | 自动化检查流程。GitHub Actions 是一种 CI。 |
| pull request | 准备合并到主分支的一组改动。 |
| materialize PR diff | 在 CI 里把 PR 相对 base branch 的 diff 应用到 working tree，让 `guard check` 能检查它。 |
| fail the job | 命令返回非 0 退出码，让 GitHub Actions 标记检查失败。`review` 和 `block` 都会返回非 0。 |
| self-hosted runner | 你自己管理的 GitHub Actions runner。适合预装 `carves`。 |
| internal package source | 你的团队自己的工具包来源。当前 beta 文档不承诺公开 registry。 |
