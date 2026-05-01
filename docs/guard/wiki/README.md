# CARVES.Guard Wiki

中文入口：

- [新手教程：从零跑第一次 Guard 检查](guard-beginner-guide.zh-CN.md)
- [术语表：每个关键字是什么意思](glossary.zh-CN.md)
- [流程图：本地、决策、CI 怎么走](workflow.zh-CN.md)
- [Policy 模板：复制后按项目修改](guard-policy-starter.zh-CN.md)
- [GitHub Actions 接入](github-actions.zh-CN.md)

English entry:

- [Beginner guide: run your first Guard check](guard-beginner-guide.en.md)
- [Glossary: what every keyword means](glossary.en.md)
- [Workflow diagrams: local, decision, CI](workflow.en.md)
- [Policy template: copy and adapt](guard-policy-starter.en.md)
- [GitHub Actions integration](github-actions.en.md)

Stable commands:

```powershell
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

Boundary: CARVES.Guard checks an existing git patch before review or merge. It is not an operating-system sandbox, not an AI coding agent, and not a replacement for human review.
