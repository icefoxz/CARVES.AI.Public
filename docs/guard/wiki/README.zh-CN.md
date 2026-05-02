# CARVES.Guard Wiki

语言：[英文](README.md)

入口：

- [新手教程：从零跑第一次 Guard 检查](guard-beginner-guide.zh-CN.md)
- [术语表：每个关键字是什么意思](glossary.zh-CN.md)
- [流程图：本地、决策、CI 怎么走](workflow.zh-CN.md)
- [Policy 模板：复制后按项目修改](guard-policy-starter.zh-CN.md)
- [GitHub Actions 接入](github-actions.zh-CN.md)

稳定命令：

```powershell
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

边界：CARVES.Guard 在 review 或 merge 前检查已有 git patch。它不是操作系统沙箱，不是 AI coding agent，也不能替代人工 review。
