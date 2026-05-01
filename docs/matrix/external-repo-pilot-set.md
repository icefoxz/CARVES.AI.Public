# CARVES Matrix External Repo Pilot Set

Status: summary-only pilot catalog.

This catalog defines the external repository shapes that Matrix should cover after the single temporary toy repository proof. It is a pilot definition, not an execution result.

The machine-readable catalog is:

```text
docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json
```

Validate the catalog locally:

```powershell
pwsh ./scripts/matrix/matrix-external-pilot-set.ps1
```

The script emits `matrix-external-repo-pilot-set.v0` JSON and can optionally write it with `-OutputPath <path>`.

## Coverage

| Pilot id | Shape | Setup summary | Expected Matrix behavior | Known limitations |
| --- | --- | --- | --- | --- |
| `node_single_package` | Small Node package | One package directory with `package.json`, a source folder, and a test folder. | Guard records a small diff summary; Handoff records continuation; Audit emits `shield-evidence.v0`; Shield evaluates local evidence; Matrix records summary artifacts only. | Does not run npm install, does not require registry credentials, and does not prove JavaScript semantic correctness. |
| `dotnet_small_project` | Small .NET project | One solution or project with a library file and a test file. | Matrix should behave like the existing project-mode smoke while treating the target as an external repo. | Does not publish packages, does not prove NuGet.org readiness, and does not replace `dotnet test`. |
| `python_package` | Python package | One `pyproject.toml`, package module, and test module. | Matrix should record summary evidence without requiring source upload or raw diff upload. | Does not install from PyPI, does not create a virtual environment proof, and does not prove Python package correctness. |
| `monorepo_nested_project` | Monorepo-like nested project | Multiple nested package roots with Matrix pointed at the selected target root. | Matrix should keep artifact paths scoped to the selected target and record nested-project limitations. | Does not discover every workspace automatically and does not claim whole-monorepo coverage. |
| `dirty_worktree` | Dirty worktree scenario | Baseline commit plus uncommitted local edits that are intentionally left unstaged. | Guard should summarize the working tree change; Audit and Shield should preserve local-only, summary-only evidence; Matrix should not require a clean checkout. | Does not certify the dirty change, does not upload raw diffs, and does not replace human review before commit. |

## Summary-Only Artifacts

Each pilot may record these artifacts:

- `pilot-setup-summary.json`
- `guard-check-summary.json`
- `handoff-summary.json`
- `audit-evidence-summary.json`
- `shield-evaluate-summary.json`
- `matrix-pilot-summary.json`

These artifacts must be summary-only. They must not include source code, raw diffs, prompts, model responses, secrets, credentials, customer payloads, hosted uploads, public leaderboard claims, or certification claims.

## Explicit Non-Coverage

This pilot set does not cover large production monorepos, private dependency installation, package registry publication, hosted verification, public certification, public ranking, model safety benchmarking, operating-system sandboxing, automatic rollback, semantic source correctness, or live network-provider integration.

The pilot set is a definition of the shapes Matrix should exercise. A pilot run can pass only for the local target and artifact bundle it actually produced.
