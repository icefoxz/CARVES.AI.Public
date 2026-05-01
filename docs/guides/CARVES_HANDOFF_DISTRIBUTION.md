# CARVES Handoff Distribution

CARVES Handoff is the explicit AI session continuity packet tool.

It helps an outgoing agent leave a bounded packet and an incoming agent resume from that packet without broad rediscovery or repeated work.

## Status

CARVES Handoff is currently an Alpha local-package pilot.

It is not published to a public registry. It is not signed. It does not claim production support.

## Packages

| Package | Version | Role |
| --- | --- | --- |
| `CARVES.Handoff.Core` | `0.1.0-alpha.1` | Portable packet inspection and projection core. |
| `CARVES.Handoff.Cli` | `0.1.0-alpha.1` | `carves-handoff` dotnet tool. |

The primary user surface is the CLI. The Core package exists so the CLI surface stays portable and testable.

## Local Tool Install

Build local packages:

```powershell
$packageRoot = Join-Path $env:TEMP "carves-handoff-packages"
dotnet pack .\src\CARVES.Handoff.Core\Carves.Handoff.Core.csproj -c Release -o $packageRoot
dotnet pack .\src\CARVES.Handoff.Cli\Carves.Handoff.Cli.csproj -c Release -o $packageRoot
```

Install into an explicit tool path:

```powershell
$toolRoot = Join-Path $env:TEMP "carves-handoff-tool"
dotnet tool install CARVES.Handoff.Cli --tool-path $toolRoot --add-source $packageRoot --version 0.1.0-alpha.1 --ignore-failed-sources
```

Run the installed tool:

```powershell
& (Join-Path $toolRoot "carves-handoff.exe") --repo-root <target-repo> help
& (Join-Path $toolRoot "carves-handoff.exe") --repo-root <target-repo> draft --json
& (Join-Path $toolRoot "carves-handoff.exe") --repo-root <target-repo> inspect --json
& (Join-Path $toolRoot "carves-handoff.exe") --repo-root <target-repo> next --json
```

The local package smoke is:

```powershell
.\scripts\handoff\handoff-packaged-install-smoke.ps1
```

It builds local nupkgs, installs `CARVES.Handoff.Cli` with `dotnet tool install --tool-path`, and proves `help`, `draft`, `inspect`, and `next` from a temporary external repository. It does not push to NuGet.org.

## Boundary

Handoff uses this default packet path when no packet path is supplied:

```text
.ai/handoff/handoff.json
```

It does not write Guard state, Audit state, long-term memory, or repository discovery state.

`draft` writes only the selected packet path and refuses overwrite. `inspect` and `next` are read-only over the supplied or default packet.

## Writer Contract

`draft` creates a low-confidence packet skeleton for operator review.

It does not automatically generate a ready handoff packet.

The expected flow is:

```powershell
carves-handoff draft --json
```

Then complete the packet manually with bounded context refs and evidence-backed facts, and inspect it:

```powershell
carves-handoff inspect --json
carves-handoff next --json
```

Only packets that inspect as `ready` should be used as incoming-agent resume guides.

Current `draft` behavior:

- default output path is `.ai/handoff/handoff.json`
- explicit output path is still accepted
- existing target file is not overwritten
- protected paths are rejected
- `resume_status=operator_review_required`
- `confidence=low`
- no Guard, Audit, or long-term memory state is written
- no repository-wide packet discovery is performed

## Guard Decision References

Handoff packet `decision_refs` may reference Guard run ids:

```json
"decision_refs": [
  "guard-run:<run-id>"
]
```

During `inspect` and `next`, Handoff reads `.ai/runtime/guard/decisions.jsonl` when present and marks those references as `linked` or `unresolved`.

Missing Guard decisions produce warnings only. Handoff never mutates Guard decisions and never becomes the owner of Guard truth.

## Public Release Blockers

Before public package publication, Handoff still needs:

- package license decision
- repository and project URLs
- public release notes
- public support destination and release-specific support posture
- public vulnerability reporting reference
- signed package implementation and verification proof under the matrix signing posture

Limited-pilot support and issue intake are recorded in:

```text
docs/guides/CARVES_PILOT_SUPPORT_AND_ISSUE_INTAKE.md
```

Handoff writer contract review is recorded in:

```text
docs/runtime/carves-matrix-phase-41-handoff-writer-contract-review-2026-04-14.md
```
