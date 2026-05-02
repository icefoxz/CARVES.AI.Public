# 安全策略

语言：[英文](SECURITY.md)

CARVES Guard、Handoff、Audit 和 Shield 是 local-first 工具。公开 matrix proof 不需要上传源码、raw diff、prompt、模型回复、secret、credential，不需要 hosted verification，也不需要 public leaderboard。

## 支持版本

| 产品 | 当前公开姿态 |
| --- | --- |
| Guard | `0.2.0-beta.1`，local-package beta proof |
| Handoff | `0.1.0-alpha.1`，local-package alpha proof |
| Audit | `0.1.0-alpha.1`，local-package alpha proof |
| Shield | `CARVES.Runtime.Cli` 中的本地命令 |

## 报告漏洞

如果这个仓库启用了 GitHub private vulnerability reporting，请使用它。如果无法私密报告，请开一个最小公开 issue，只说明有安全报告可提供，并等待 maintainer 联系。不要在公开 issue 中包含 exploit 细节、私有仓库内容、raw diff、prompt、模型回复、secret、credential 或客户数据。

## 应包含的信息

- 受影响的命令和版本；
- 操作系统；
- 使用 synthetic files 的最小复现；
- 预期和实际 decision 输出；
- 问题是否可能改变 allow/review/block decision、evidence discovery、Shield scoring 或 artifact privacy。

## 非声明

当前 release line 不声明 operating-system sandboxing、syscall interception、automatic rollback、hosted verification、public certification 或 public leaderboard ranking。
