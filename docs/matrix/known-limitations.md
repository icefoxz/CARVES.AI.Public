# CARVES Matrix Known Limitations

This document freezes the public limitation language for the current matrix release candidate.

## Current Status

The matrix is ready for GitHub publication as a local workflow self-check and local consistency proof for AI coding workflow governance:

1. Guard writes local patch decisions.
2. Handoff writes local continuation packets.
3. Audit discovers those local files and generates `shield-evidence.v0`.
4. Shield evaluates that summary evidence and renders a badge.
5. Matrix orchestrates the summary-only proof bundle and does not add another safety engine.

## Explicit Non-Claims

CARVES currently does not provide:

- producer identity;
- signatures;
- transparency-log backing;
- operating-system sandboxing;
- model safety benchmarking;
- AI model ranking or rating;
- syscall interception;
- real-time file write prevention;
- network isolation;
- automatic rollback of arbitrary writes;
- hosted verification;
- public certification;
- public leaderboard ranking;
- semantic correctness proof for source code;
- source code upload;
- raw diff upload;
- prompt upload;
- model response upload;
- secret or credential upload.

## Agent Trial Local Test

The public first-run path is:

```text
carves test demo
carves test agent
```

From source, the equivalent first command is:

```text
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
```

These commands write local playtest output under `./carves-trials/` by default. `carves test demo` is the automatic local environment check; `carves test agent` prepares the real agent-assisted task run.

The local score measures only the evidence posture for one task run: reviewability, traceability, explainability, report honesty, constraint adherence, and reproducibility evidence. It is useful for comparing whether an agent left a clean, inspectable local trail.

The local test still does not prove:

- certification;
- leaderboard eligibility;
- hosted verification;
- producer identity;
- operating-system sandboxing;
- semantic correctness;
- local anti-cheat or tamper-proof execution.

A user who controls the local machine can still tamper with workspace files, commands, clocks, environment, or output before submission. Server-side challenge retrieval, receipts, hosted reruns, identity, and leaderboard policy are separate surfaces and are not provided by the local quickstart.

## Portable Agent Trial Package

The portable package contract is documented in `docs/matrix/portable-agent-trial-pack.md`. It defines the current download-and-extract directory layout and the local-only scoring boundary.

The current directory-form package writer is:

```text
carves test package --output ./carves-agent-trial-pack
```

The intended package root contains `README-FIRST.md`, `COPY_THIS_TO_AGENT_BLIND.txt`, `COPY_THIS_TO_AGENT_GUIDED.txt`, `SCORE.cmd`, `score.sh`, `RESULT.cmd`, `result.sh`, `RESET.cmd`, `reset.sh`, `agent-workspace/`, `.carves-pack/`, `results/`, `results/local/`, `results/submit-bundle/`, and, for release Windows playable zips, `tools/carves/` with `scorer-manifest.json`.

The package writer prepares the package directory, authority metadata, local score launchers, result readback launchers, and reset launchers. During local scoring, portable packages verify workspace task metadata, instruction metadata, the git baseline, and protected starter metadata against `.carves-pack/` before collection. Users can run `carves test collect`, `./score.sh`, or `SCORE.cmd` from the package root after the agent writes `agent-workspace/artifacts/agent-report.json`.

Users should open only `agent-workspace/` in the tested agent. `.carves-pack/` is the local scorer authority area and must stay outside the agent writable workspace. Score scripts and expected hashes must not live under `agent-workspace/`.

This authority split is a local trust-boundary improvement, not anti-cheat. If the same local user edits both `agent-workspace/` and `.carves-pack/`, or rewrites the package writer itself, the package cannot prove that tampering did not happen. Hosted challenge retrieval, server receipts, signatures, and transparency logs remain future surfaces.

V1 portable packages can show the previous result after the score window closes through `RESULT.cmd` or `./result.sh`. Users can reset the same local folder for another practice run through `RESET.cmd`, `./reset.sh`, or `carves test reset`. Fresh extraction remains the cleanest strict-comparison path.

The package scorer records `.carves-pack/state.json` outside `agent-workspace/`. It refuses stale `results/` files or pre-existing judge evidence as contaminated, and reports diagnostics that point users to reset, fresh extraction, or failed evidence inspection. Reset restores `agent-workspace/` to its local git baseline, archives local output under `results/history/`, and parks unexpected package-root files there. This is still only local contamination hygiene, not anti-cheat or tamper-proof execution.

The Windows playable first-run contract requires a package-local scorer under `tools/carves/` and must not require global `carves`, a CARVES source checkout, or `dotnet build`. The release input script `scripts/matrix/publish-windows-playable-scorer.ps1` stages a self-contained `win-x64` Runtime `carves` scorer root for `--scorer-root`. Current developer directory packages may still rely on a repo-local or PATH `carves` command until release assembly attaches that scorer bundle. That developer fallback must not be marketed as the Windows playable zip.

The release-oriented assembly path is local-only: `carves-matrix trial package --windows-playable --scorer-root <win-publish-root> --output <package-root> --zip-output <zip>`, or the source-checkout wrapper `scripts/matrix/build-windows-playable-package.ps1`. It stages an already available Windows scorer into `tools/carves/` and writes the zip; it does not publish to NuGet.org, host downloads, sign the package, or create a transparency log.

The Windows clean SCORE.cmd smoke is `scripts/matrix/smoke-windows-score-cmd.ps1`. It validates a fresh local extraction, isolated PATH without global `carves.exe`, package-local scorer execution, task runtime tools such as `node.exe` for the official fixture command, local-only score output, submit-bundle artifacts, repeated SCORE readback, the missing-scorer diagnostic, and the missing-Node dependency diagnostic. It is still a local smoke, not a hosted verification service, signature, certification, leaderboard admission, anti-cheat system, or OS sandbox.

The Windows SCORE.cmd path smoke is `scripts/matrix/smoke-windows-score-cmd-paths.ps1`. It wraps the clean smoke with spaces and non-ASCII path text in the work root and build output root. It does not exhaustively certify every Windows locale, shell, archive tool, network share, or long-path configuration.

The Windows playable SCORE.cmd CI smoke is wired in `.github/workflows/matrix-proof.yml`. It runs `scripts/matrix/smoke-windows-score-cmd-paths.ps1` on `windows-latest` and uploads `windows-scorecmd-path-smoke-summary.json`, the generated zip, and local-only score output artifacts. The summary is written for both pass and fail outcomes, redacts the absolute work root and source checkout root, records non-claims, and preserves a bounded failure summary when the smoke fails. This is CI regression evidence, not hosted verification, package signing, public download hosting, certification, or leaderboard eligibility.

`tools/carves/scorer-manifest.json` is diagnostic metadata for the bundled scorer. It is not a tamper-proof signature, certification, server receipt, leaderboard proof, producer identity, anti-cheat, operating-system sandbox, or semantic correctness proof.

This portable shape still does not provide local anti-cheat, tamper-proof local execution, hosted verification, certification, leaderboard eligibility, producer identity, operating-system sandboxing, semantic correctness proof, source upload, raw diff upload, prompt response upload, model response upload, secret upload, or credential upload.

## Release Channel

The current proof uses local package installation from freshly built `.nupkg` files. NuGet.org publication remains an operator-gated release action and is not required for the matrix proof.

## Linux Native And PowerShell Lanes

The Linux-native first-run path is supported through `carves-matrix proof --lane native-minimal --json` and `carves-matrix verify <artifact-root> --json`. It runs through the .NET Matrix CLI and does not require `pwsh` or `scripts/matrix/*.ps1`. The older `proof --json` shorthand remains compatible, but public docs use the explicit lane form.

PowerShell remains part of the full release evidence lane. The project-mode smoke, packaged-install smoke, external pilot catalog script, cross-platform pilot verify script, and release readiness script are still PowerShell scripts. Those scripts exist to prove broader release and packaging behavior; they are not a requirement for the Linux-native Matrix first run.

Those PowerShell release scripts use a shared checked process helper that drains stdout and stderr concurrently, applies bounded timeout handling, caps captured output, and attempts process-tree cleanup on timeout. That hardens release lane process capture, but it does not turn the PowerShell lane into an operating-system sandbox, hosted verification service, or Linux-native first-run requirement.

The native full-release boundary is documented in `docs/matrix/native-full-release-feasibility.md` and `docs/matrix/native-full-release-public-contract.md`. The explicit `native-full-release` lane is implemented as an opt-in C# lane, but it is not the default full-release shorthand and does not remove the PowerShell compatibility lane.

Failed native full-release attempts are intentionally not published into the requested artifact root. They are preserved in an isolated sibling failure directory with `native-full-release-failure.json` evidence, so existing successful bundles stay verifiable after a failed retry.

## Stable Shield Input

The stable Shield input for this release candidate is `shield-evidence.v0` generated by `carves-audit evidence`.

Shield can evaluate hand-written evidence, but the GitHub-publishable matrix proof is the automated Guard -> Handoff -> Audit -> Shield chain.

Shield treats missing scoring fields as insufficient evidence. Partial hand-written evidence can still produce a valid local self-check result, but omitted fields do not satisfy higher G/H/A level predicates.

## Trust-Chain Hardening Status

The current local trust-chain release checkpoint is complete for the local proof lane. This covers Audit evidence integrity, Guard deletion/replacement honesty, Shield evidence contract alignment, Guard decision-store durability, Handoff completed-state semantics, Matrix Shield proof linkage, large-log/output boundaries, Handoff reference portability, and usability coverage cleanup.

Both native minimal and full release proof summaries validate public `proof_capabilities` fields against proof-mode expectations and public `trust_chain_hardening` fields against verifier-computed gates. Matrix also uses manifest-bound verified reads before trusting native/full release source-summary semantics or Shield evaluation semantics, and the public proof summary contract is closed: unknown public fields fail verification.

This does not convert the local self-check into a model safety benchmark, hosted verification, public certification, public leaderboard ranking, operating-system sandboxing, automatic rollback, or semantic proof that generated code is correct.

## Local Decision Storage

Guard writes local decision records to `.ai/runtime/guard/decisions.jsonl` with a file-exclusive append lock and bounded retention. This is intended to keep same-repository local writers from interleaving JSONL records. It is not a remote registry, distributed consensus log, signed ledger, or tamper-proof storage system.

## Artifact Privacy

Matrix proof artifacts must remain summary-only. They may include:

- Guard decision summaries;
- Handoff packet summaries;
- Audit summary, timeline, explain, and evidence JSON;
- Shield evaluation JSON;
- Shield badge JSON and SVG;
- script logs.

They must not include private source, raw diffs, prompts, model responses, secrets, credentials, or customer data.

## External Pilot Coverage

The external pilot catalog is `docs/matrix/external-repo-pilot-set.md`, with a machine-readable summary at `docs/matrix/examples/matrix-external-repo-pilot-set.v0.example.json`.

The current pilot shapes are:

- small Node package;
- small .NET project;
- Python package;
- monorepo-like nested project;
- dirty worktree.

This is a coverage definition for local Matrix proof work. It is not evidence that every production repository shape has passed, and it does not cover private dependency installation, package registry publication, hosted verification, public certification, public ranking, model safety benchmarking, operating-system sandboxing, automatic rollback, semantic source correctness, or live network-provider integration.

The cross-platform pilot verification lane in `docs/matrix/cross-platform-verify-pilot.md` records `matrix-cross-platform-verify-pilot-checkpoint.json` and verifies that failure probes expose reason codes such as `hash_mismatch`. That checkpoint confirms only the generated summary-only pilot bundles and verifier behavior on the runner operating systems.

Large log stress behavior is documented in `docs/matrix/large-log-stress.md`. Current Audit fixtures cover a bounded 1000-line Guard JSONL tail window and a 131072-byte per-record limit. Larger patch metadata is accepted only while it remains summary-only and under that per-record limit. Matrix manifest verification hashes large artifacts and reports hash or size changes as `hash_mismatch`; it does not inline large artifact contents into the manifest.
