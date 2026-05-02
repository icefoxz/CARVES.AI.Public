# CARVES 公开文档索引

语言：[En](INDEX.md)

这个索引用来区分 Runtime-first 公开路径和内部 operator review checkpoint。公开文档应该让新读者能从本地 Runtime CLI 和当前限制开始，而不需要先理解私有 operating history。

CARVES Runtime 是这个快照的公开入口。Guard / Handoff / Audit / Shield / Matrix 是 Runtime 治理能力，不是并列入口。

## Runtime 入口

优先使用这些 Runtime 入口：

- `../README.zh-CN.md`
- `../START_CARVES.zh-CN.md`
- `runtime/runtime-project-recenter-carves-g0-visible-gateway-contract.md`
- `release/runtime-versioning-policy.md`
- `release/runtime-0.6.1-beta-release-identity.md`
- `release/runtime-0.6.1-beta-version-bom.md`

第一条公开路径是源码构建、CLI/gateway readback，然后运行 `carves up <target-project>`。新用户在这之前不需要理解 Runtime CARD、TaskGraph 或 Host truth internals。

## Runtime 能力文档

Runtime 入口清楚后，再看这些能力文档：

- `guard/README.zh-CN.md`
- `handoff/README.zh-CN.md`
- `audit/README.zh-CN.md`
- `shield/README.zh-CN.md`
- `matrix/README.zh-CN.md`
- `matrix/public-boundary.md`
- `matrix/quickstart.zh-CN.md`
- `release/matrix-release-notes.md`
- `matrix/known-limitations.md`
- `matrix/github-actions-proof.md`
- `matrix/packaged-install-matrix.md`
- `guides/public-surface-size-advisory.md`

这些页面描述本地 Runtime 能力行为。它们不应该把 Guard / Handoff / Audit / Shield / Matrix 表达成并列入口，也应该避免要求公开用户先理解 Runtime CARD/TaskGraph 概念，才能运行本地 self-check path。

## 内部 Checkpoint 和 Operator Review

release checkpoint 页面是 operator review evidence，不是新手入口。它们在 maintainer 需要时保留 CARD traceability，但不应该作为新用户的第一路径。

- `release/matrix-github-release-candidate-checkpoint.md`
- `release/trust-chain-hardening-release-checkpoint.md`
- `release/github-publish-readiness-boundary.md`
- `release/github-release-draft.md`
- `release/github-publish-readiness-checkpoint.md`
- `release/product-extraction-readiness-checkpoint.md`
- `release/matrix-verifiable-local-self-check-checkpoint.md`
- `release/matrix-operator-release-gate.md`

发布仍然需要 operator 对 tag、release artifact、checksum、package push 和 hosted release page 执行动作。
