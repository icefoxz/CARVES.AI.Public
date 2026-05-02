# 术语表：每个关键字是什么意思

语言：[英文](glossary.en.md)

## Shield

CARVES Shield 是 AI 代码治理的自检标签。它读取摘要证据，输出 Standard G/H/A 结果、Lite 分数和本地 badge。

## Self-check

Self-check 表示证据由项目本地生成，Shield v0 按规则评估它。Self-check 有价值，但不是第三方认证。

## Local self-check

Local self-check 是当前 release 里 Shield、Matrix、badge、challenge run 和 verification output 的公开姿态。它表示工具只评估本地摘要证据和本地 artifact。它不等于 hosted verification、public certification、model safety benchmarking、AI model ranking、source-code security review、semantic correctness proof 或 operating-system sandboxing。

## Verified evaluation

Verified evaluation 是保留给未来的词。Shield v0 不输出 verified，不声称认证，也不声称已经由中心服务复核。

## Verification result

Verification result 表示本地 verifier 按 manifest、required files、hashes、sizes、schema metadata、privacy flags 和 summary consistency 检查已有 artifact bundle。成功的 verification result 只说明本地 bundle 符合当前 verifier contract。它不是 Shield verified evaluation、hosted review、certification、public leaderboard result、model safety rating，也不证明源码语义正确。

## Evidence

Evidence 是证据摘要。它记录项目有什么治理能力，例如 policy 是否存在、CI 是否运行、决策数量、handoff packet 是否新鲜、audit log 是否可读。

Evidence 默认不包含源码、raw diff、prompt、模型回复、secret、credential。

## shield-evidence.v0

Shield v0 的证据 JSON schema。当前本地 evaluator 支持这个版本。

## Privacy posture

Privacy posture 表示证据文件是否保持隐私边界。默认必须是 summary-first，不能上传或包含敏感材料。

## Source upload

Source upload 指上传源码内容。Shield v0 默认不需要，也不接受把源码作为 self-check 必需材料。

## Raw diff

Raw diff 是 git diff 的原文。Shield v0 默认不需要 raw diff。证据里只应放计数、路径摘要或决策摘要。

## Guard

Guard 是输出治理维度，缩写为 `G`。它看 AI patch 是否经过边界检查，例如 protected paths、change budget、CI fail-closed、decision records。

## Handoff

Handoff 是输入治理维度，缩写为 `H`。它看下一次 AI 会话是否有可靠上下文，例如当前目标、剩余工作、已完成事实、不要重复的错误、目标仓库匹配。

## Audit

Audit 是历史治理维度，缩写为 `A`。它看过去的 AI 相关判断能不能被读取、解释和复查，例如 log 是否可读、记录是否有 rule id 和 evidence refs、block/review 是否有 explain coverage。

## G/H/A

`G/H/A` 是 Shield Standard 的三维标签：

- `G` 是 Guard evidence，表示输出治理。
- `H` 是 Handoff evidence，表示输入治理。
- `A` 是 Audit evidence，表示历史治理。

这三个维度描述 governance maturity。它们不是 model safety score、certification level、security audit result、semantic correctness proof，也不保证 patch 应该 merge。

## Standard

Standard 是三维结果，不合并成一个总分。格式类似：

```text
CARVES G8.H5.A3 /30d PASS
```

Standard 的价值是让弱点可见。一个项目可以 G 很强，但 H 或 A 很弱。

## Lite

Lite 是 0-100 分的快速 self-check。它适合分享、比较、拉新和快速定位，但不能替代 Standard。

## Lite score

Lite score 是从 Shield evidence 和 Standard G/H/A 姿态推导出的 0-100 数字投影。它只是本地 workflow governance maturity 的便捷读数。它不能隐藏薄弱的 G/H/A 维度，也不能被写成 model safety、certification、hosted verification、public ranking 或 source-code correctness。

## PASS

`PASS` 表示当前 Shield Standard evaluation 没有因为输入摘要证据触发 critical governance gate。它是 local self-check result，不是 merge approval、certification、hosted verification、public safety label、security audit，也不证明生成代码正确。

## REVIEW

`REVIEW` 表示 Guard decision 或相关治理输出发现了需要人类检查的风险，然后才能进入正常 merge 或 release 处理。它是 governance workflow signal，不表示模型不安全、项目被认证为不安全、或源码语义错误。

## BLOCK

`BLOCK` 表示 Guard decision 或相关治理输出发现了不应原样继续的 policy violation。合并前应修复、缩小或显式处理这个 patch。它是本地 workflow boundary，不是 model safety verdict、certification result、hosted enforcement action 或 operating-system sandbox。

## Level 0

`0` 表示未启用或没有可用证据。它不是失败，只是没有提出治理证据。

## Critical Gate

Critical Gate 是关键门。它只用于机械、严重、影响可信度的失败，例如 policy schema 无效、fail-closed 被关闭、CI 不阻断 block 结果。

## C

`C` 表示 Critical Gate 失败。它不是数字，不能平均。`C` 比 `0` 更严重，因为 `0` 是没有证据，`C` 是已经声称治理但关键门坏了。

## Sample window

Sample window 是证据覆盖的时间窗口，例如 `/30d` 表示最近 30 天。8-9 这类强等级通常需要持续窗口证据。

## Badge

Badge 是本地生成的 SVG 标记。它可以放在 README 里展示 self-check 结果，但不是认证标志。

## Challenge result

Challenge result 表示 `carves-shield challenge` 评估了本地 Shield Lite challenge pack，并报告 case pass/fail status、pass rate，以及固定标签 `local challenge result, not certified safe`。Challenge result 只演练已知的本地治理 fixture。它不是 certification、hosted verification、public leaderboard entry、model safety benchmark、semantic source-code correctness proof 或 operating-system sandbox proof。

## GitHub Actions proof

GitHub Actions proof 是在 CI 里运行本地 evaluate 和 badge 命令，并上传 JSON/SVG artifact。它不需要 provider secrets，不需要 hosted API。
