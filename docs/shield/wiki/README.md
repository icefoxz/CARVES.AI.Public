# CARVES Shield Wiki

Language: [中文](README.zh-CN.md)

- [Beginner guide: run your first Shield self-check](shield-beginner-guide.en.md)
- [Glossary: what every keyword means](glossary.en.md)
- [Workflow diagrams: local, self-check, and CI](workflow.en.md)
- [Evidence starter: prepare shield-evidence.v0](evidence-starter.en.md)
- [GitHub Actions integration](github-actions.en.md)
- [Badge guide](badge.en.md)

## Stable Commands

```powershell
carves shield evaluate <evidence-path> --json --output combined
carves shield badge <evidence-path> --json --output shield-badge.svg
```

Boundary: CARVES Shield v0 is a local-first self-check. It does not upload source by default, does not run a hosted verifier, does not publish a public ranking, and does not certify a project.
