# CARVES.Guard Beta

CARVES.Guard 的公开产品文档已经集中到 Guard 文档树：

```text
docs/guard/README.md
docs/guard/wiki/Home.md
```

建议从这里开始：

- [CARVES.Guard Docs](../guard/README.md)
- [中文新手教程](../guard/wiki/guard-beginner-guide.zh-CN.md)
- [English beginner guide](../guard/wiki/guard-beginner-guide.en.md)
- [中文术语表](../guard/wiki/glossary.zh-CN.md)
- [English glossary](../guard/wiki/glossary.en.md)
- [GitHub Actions 接入](../guard/wiki/github-actions.zh-CN.md)

稳定命令：

```powershell
carves guard init
carves guard check --json
carves guard audit
carves guard report
carves guard explain <run-id>
```

保留的 Guard proof 脚本：

```powershell
.\scripts\alpha\guard-packaged-install-smoke.ps1
pwsh ./scripts/beta/guard-packaged-install-smoke.ps1
pwsh ./scripts/beta/guard-external-pilot-matrix.ps1
pwsh ./scripts/beta/guard-beta-proof-lane.ps1
```

这些 smoke 使用 local nupkg / local tool-path proof；remote registry publication 仍然不是这个阶段的动作。

CARVES.Guard 是 patch admission gate，不是操作系统沙箱。它检查已经出现在 git working tree 里的 patch，并给出 `allow`、`review` 或 `block`。

---

CARVES.Guard public product docs now live under the Guard docs tree:

```text
docs/guard/README.md
docs/guard/wiki/Home.md
```

Start here:

- [CARVES.Guard Docs](../guard/README.md)
- [Five-minute quickstart](../guard/quickstart.en.md)
- [Chinese beginner guide](../guard/wiki/guard-beginner-guide.zh-CN.md)
- [English beginner guide](../guard/wiki/guard-beginner-guide.en.md)
- [Chinese glossary](../guard/wiki/glossary.zh-CN.md)
- [English glossary](../guard/wiki/glossary.en.md)
- [GitHub Actions integration](../guard/wiki/github-actions.en.md)
- [Copyable GitHub Actions template](../guard/github-actions-template.md)

Retained Guard proof scripts:

```powershell
.\scripts\alpha\guard-packaged-install-smoke.ps1
pwsh ./scripts/beta/guard-packaged-install-smoke.ps1
pwsh ./scripts/beta/guard-external-pilot-matrix.ps1
pwsh ./scripts/beta/guard-beta-proof-lane.ps1
```

These smokes use local nupkg / local tool-path proof; remote registry publication is still outside this phase.

CARVES.Guard is a patch admission gate, not an operating-system sandbox. It checks a patch already present in the git working tree and returns `allow`, `review`, or `block`.
