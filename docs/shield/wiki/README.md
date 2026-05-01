# CARVES Shield Wiki

中文入口：

- [新手教程：从零跑第一次 Shield 自检](shield-beginner-guide.zh-CN.md)
- [术语表：每个关键字是什么意思](glossary.zh-CN.md)
- [流程图：本地、自检、CI 怎么走](workflow.zh-CN.md)
- [证据模板：如何准备 shield-evidence.v0](evidence-starter.zh-CN.md)
- [GitHub Actions 接入](github-actions.zh-CN.md)
- [Badge 解读与发布](badge.zh-CN.md)

English entry:

- [Beginner guide: run your first Shield self-check](shield-beginner-guide.en.md)
- [Glossary: what every keyword means](glossary.en.md)
- [Workflow diagrams: local, self-check, and CI](workflow.en.md)
- [Evidence starter: prepare shield-evidence.v0](evidence-starter.en.md)
- [GitHub Actions integration](github-actions.en.md)
- [Badge guide](badge.en.md)

Stable commands:

```powershell
carves shield evaluate <evidence-path> --json --output combined
carves shield badge <evidence-path> --json --output shield-badge.svg
```

Boundary: CARVES Shield v0 is a local-first self-check. It does not upload source by default, does not run a hosted verifier, does not publish a public ranking, and does not certify a project.
