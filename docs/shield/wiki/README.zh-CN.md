# CARVES Shield Wiki

语言：[En](README.md)

入口：

- [新手教程：从零跑第一次 Shield 自检](shield-beginner-guide.zh-CN.md)
- [术语表：每个关键字是什么意思](glossary.zh-CN.md)
- [流程图：本地、自检、CI 怎么走](workflow.zh-CN.md)
- [证据模板：如何准备 shield-evidence.v0](evidence-starter.zh-CN.md)
- [GitHub Actions 接入](github-actions.zh-CN.md)
- [Badge 解读与发布](badge.zh-CN.md)

稳定命令：

```powershell
carves shield evaluate <evidence-path> --json --output combined
carves shield badge <evidence-path> --json --output shield-badge.svg
```

边界：CARVES Shield v0 是 local-first self-check。它默认不上传 source，不运行 hosted verifier，不发布 public ranking，也不认证项目。
