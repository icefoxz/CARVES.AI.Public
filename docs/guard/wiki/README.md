# CARVES.Guard Wiki

Language: [Chinese](README.zh-CN.md)

- [Beginner guide: run your first Guard check](guard-beginner-guide.en.md)
- [Glossary: what every keyword means](glossary.en.md)
- [Workflow diagrams: local, decision, CI](workflow.en.md)
- [Policy template: copy and adapt](guard-policy-starter.en.md)
- [GitHub Actions integration](github-actions.en.md)

## Stable Commands

```powershell
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

Boundary: CARVES.Guard checks an existing git patch before review or merge. It is not an operating-system sandbox, not an AI coding agent, and not a replacement for human review.
