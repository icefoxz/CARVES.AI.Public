# CARVES Agent Trial V1 Local User Smoke

Status: CARD-916 end-to-end local user smoke for the offline Agent Trial path.

This smoke pins the first-run path for a user who wants to download or clone CARVES, prepare the official local starter pack, run an agent in that folder, generate a local score, verify the Matrix bundle, and record a local history entry. It does not require a server account, registration, private operator setup, hosted API, source upload, raw diff upload, prompt response upload, model response upload, secret upload, or credential upload.

CARD-933 collapses the public first-run path to `carves test`. The commands below are the first commands a normal user should see:

```bash
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test agent --demo-agent --json
```

For an installed CLI:

```bash
carves test demo
carves test agent
carves test result
carves test verify
```

The default output root is `./carves-trials/`. The local score measures reviewability, traceability, explainability, report honesty, constraint adherence, and reproducibility evidence. It does not prove certification, leaderboard eligibility, hosted verification, producer identity, OS sandboxing, semantic correctness, or local anti-cheat.

CARD-928 adds the shorter Matrix-owned entry points for this same local path:

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial demo --json
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial play
```

The advanced command order below remains the pinned lower-level path that those entries must reuse rather than bypass.

CARD-929 pins latest-run discovery for the short path. Completed `trial demo` and `trial play --demo-agent` runs write `./carves-trials/latest.json` after the run reaches a terminal local state. The pointer is UX-only and excluded from the Matrix manifest; `trial verify` without `--bundle-root` uses it only to find the latest bundle and then reruns strict bundle verification.

CARD-930 adds the public Runtime alias:

```bash
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test demo --json
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test agent --demo-agent --json
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test verify --json
dotnet run --project ./src/CARVES.Runtime.Cli/carves.csproj --configuration Release --no-build -- test result --json
```

The alias is intentionally thin. It delegates to Matrix trial commands and must not duplicate Matrix collector, scorer, verifier, manifest, result-card, or history logic.

CARD-932 adds the Windows double-click launcher:

```text
Start-CARVES-Agent-Test.cmd
Start-CARVES-Agent-Test.ps1
```

The `.cmd` file is the double-click entry. It launches the PowerShell wrapper without requiring persistent execution policy changes, and the PowerShell wrapper resolves the same `carves test` command path that command-line users can run directly. The launcher pauses before exit so the score summary, result-card path, or preserved CLI diagnostic remains visible.

## Pinned Command Order

From the CARVES repository root, build the CLI first:

```bash
dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release
```

Then run the local sequence:

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial plan --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial prepare --workspace ./carves-agent-trial --json
cd ./carves-agent-trial
git init
git config user.email "agent-trial-local@example.test"
git config user.name "Agent Trial Local"
git add .
git commit -m "baseline"
cd ..
```

Run the agent inside `./carves-agent-trial`, using `AGENTS.md`, `.carves/constraints/base.md`, `.carves/trial/task-contract.json`, and `prompts/official-v1-local-mvp/task-001-bounded-edit.prompt.md`. The agent must write:

```text
./carves-agent-trial/artifacts/agent-report.json
```

After the agent finishes, run:

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial local --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial verify --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial record --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --history-root ./carves-agent-trial-history --run-id day-1 --json
```

## Expected Local Outputs

The smoke pins these output locations:

```text
./carves-agent-trial/artifacts/diff-scope-summary.json
./carves-agent-trial/artifacts/test-evidence.json
./carves-agent-trial/artifacts/carves-agent-trial-result.json
./carves-agent-trial/artifacts/matrix-trial-bundle/matrix-artifact-manifest.json
./carves-agent-trial/artifacts/matrix-trial-bundle/matrix-proof-summary.json
./carves-agent-trial/artifacts/matrix-trial-bundle/matrix-agent-trial-result-card.md
./carves-agent-trial/artifacts/matrix-trial-bundle/trial/carves-agent-trial-result.json
./carves-agent-trial-history/runs/day-1.json
```

`trial local` must report:

- `status=verified`;
- `collection.local_collection_status=collectable`;
- `verification.status=verified`;
- `verification.trial_artifacts_verified=true`;
- `local_score.score_status=scored`;
- `server_submission=false`.

`trial verify` must use the same strict Matrix verifier as normal users. The smoke does not accept a side-channel verifier or a test-only manifest bypass.

## Friendly Failure Probe

The smoke also pins this setup failure:

```bash
dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- trial prepare --workspace ./carves-agent-trial --pack-root ./missing-pack --json
```

Expected diagnostic:

- `code=trial_setup_pack_missing`;
- `category=user_setup`;
- `evidence_ref=--pack-root`;
- no private absolute workspace or missing-pack path in the user-facing diagnostic fields.

## Test Anchor

The repo test anchor is:

```bash
dotnet test tests/Carves.Matrix.Tests/Carves.Matrix.Tests.csproj --no-restore --verbosity minimal --filter "FullyQualifiedName~AgentTrialLocalE2ESmokeTests"
```

The test mirrors this command order with the repo-local official starter pack. It simulates the agent's bounded edit and `artifacts/agent-report.json`, then runs `trial local`, `trial verify`, and `trial record` through `MatrixCliRunner`.

## Non-Claims

This smoke does not prove:

- server receipt issuance;
- registration;
- hosted verification;
- leaderboard eligibility;
- certification;
- producer identity;
- OS sandboxing;
- semantic correctness;
- local anti-cheat;
- tamper-proof local execution.

It only proves that the local offline first-run loop can generate and verify summary evidence without server or private operator setup.
