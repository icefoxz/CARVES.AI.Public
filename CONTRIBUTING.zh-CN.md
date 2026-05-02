# 贡献指南

语言：[En](CONTRIBUTING.md)

CARVES 当前以一组 local-first 命令行工具发布：

- Guard 检查 patch 边界。
- Handoff 记录 continuation packet。
- Audit 发现本地证据。
- Shield 评估 summary evidence 并生成 badge。

项目目前还不接受宽泛的主动 feature PR。当前协作姿态是 issue-first：先开 issue，再开 PR，这样 scope 能保持在公开 matrix 边界内。

## 开 Issue 之前

1. 确认请求属于 Guard、Handoff、Audit 或 Shield。
2. 包含你运行的命令、预期结果和实际结果。
3. 不要附加私有源码、raw diff、prompt、模型回复、secret、credential 或客户数据。
4. 安全问题请按 `SECURITY.zh-CN.md`，不要在公开 issue 中写详细漏洞内容。

## Pull Request

已有 issue 后，欢迎小型文档修正和聚焦 bug fix。PR 应该：

- 保持在公开产品边界内；
- 包含测试，或清楚说明为什么不适用测试；
- 避免 hosted API、certification、public leaderboard 或 operating-system sandbox claim，除非这些已经在 release card 中明确接受；
- 避免向 local matrix proof path 添加 telemetry、upload 或 network call。

如果工作在 matrix 边界准备好之前扩大产品 scope，maintainer 可能会关闭。
