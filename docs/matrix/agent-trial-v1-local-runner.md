# CARVES Agent Trial V1 Local Runner

Status: CARD-910 local command surface implemented for the offline source-repo starter pack path.

This document defines the V1 local command behavior. It does not implement hosted pack distribution, submission, receipts, leaderboards, or server-side challenge validation.

## Goal

The local runner gives users a repeatable way to materialize the official Agent Trial workspace before they run their own agent.

It answers:

```text
How does a user get the official pack, bind it to a prompt version and challenge id, and start from a local-only workspace?
```

The runner must not require source upload, raw diff upload, prompt response upload, model response upload, or hosted submission.

## Command Shape

Current local command shape:

```text
carves-matrix trial plan --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle
carves-matrix trial prepare --workspace ./carves-agent-trial
# run the agent inside ./carves-agent-trial using AGENTS.md and the prompt sample
carves-matrix trial collect --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle
carves-matrix trial verify --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
```

`plan` prints the prepare, run, collect, and verify sequence without side effects.

`prepare` materializes the source-repo starter pack into a clean local workspace. It is offline and refuses to overwrite a non-empty target.

`collect` runs the local collector, writes summary evidence under `artifacts/`, copies the five trial artifacts into a Matrix trial bundle, writes a manifest and proof summary, and tells the user the exact verify command.

The collected trial result includes `local_score`, using `agent-trial-local-safety-posture` version `0.2.0-local`. Command output reads back the local aggregate when it is available, or says the score was suppressed when required evidence is missing.

The same commands also produce a human-readable result card. `collect` and `local` write `matrix-agent-trial-result-card.md` under the bundle root, and JSON output includes `result_card`. The card is a derived summary that links to manifest-covered JSON evidence; it is not the source of truth.

Failure output includes local diagnostics. Diagnostics classify common failures as `user_setup`, `agent_behavior`, or `evidence_integrity`, and point to portable evidence references such as `--pack-root`, `artifacts/test-evidence.json`, `.carves/trial/task-contract.json`, or `matrix-artifact-manifest.json`. They do not lower integrity failures to warnings and do not include private workspace paths. See `docs/matrix/agent-trial-v1-local-diagnostics.md`.

Local runs can be recorded for repeated-run comparison:

```text
carves-matrix trial record --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --history-root ./carves-agent-trial-history --run-id day-1 --json
carves-matrix trial compare --history-root ./carves-agent-trial-history --baseline day-1 --target day-2 --json
```

The history root is outside the verified bundle root. History entries are local-only summaries with hash anchors; they are not server receipts and are not manifest-covered proof artifacts. Compare output is `direct` only when suite, pack, task, prompt, and scoring profile identity match exactly. Otherwise it is `trend_only`. See `docs/matrix/agent-trial-v1-local-history.md`.

`verify` runs strict `verify --trial` semantics against the local trial bundle.

`local` is the after-agent shortcut:

```text
carves-matrix trial local --workspace ./carves-agent-trial --bundle-root ./carves-agent-trial/artifacts/matrix-trial-bundle --json
```

It collects and verifies after the user has already run the agent and produced `artifacts/agent-report.json`. It does not run the agent, submit data, or claim leaderboard eligibility.

Future hosted flows may add `fetch` for server-issued or cached pack material, but the CARD-910 implementation deliberately keeps the first command surface offline.

## Future Hosted Fetch Inputs

These fields are reserved for a future hosted `fetch` command. They are not required by the current offline CARD-910 command surface.

| Field | Required | Meaning |
| --- | --- | --- |
| `suite` | yes | Trial suite id, such as `official-agent-dev-safety` |
| `prompt_version` | yes | Exact version or `latest` alias |
| `pack_version` | no | Exact pack version; default resolves from suite |
| `output_cache` | no | Local cache root for downloaded pack material |
| `offline` | no | Use already cached pack material only |

`latest` must resolve to an exact prompt version before result collection. A collected result must not store `latest` as the comparable prompt version.

## Future Hosted Fetch Outputs

A future `fetch` command would write local cache metadata, not a trial result.

Minimum fetched metadata:

- suite id;
- pack id;
- pack version;
- task pack version;
- prompt id;
- prompt version;
- prompt hash when available;
- pack hash or archive hash when available;
- fetched at timestamp;
- source marker such as `official_server`, `official_archive`, or `local_cache`;
- summary-only privacy posture.

The fetched metadata can be reused by `init`, but it is not by itself evidence that the task was run.

## Prepare Inputs

Minimum `trial prepare` inputs:

| Field | Required | Meaning |
| --- | --- | --- |
| `workspace_path` | yes | Target local workspace path |
| `pack_root` | no | Source-repo starter pack root; default is auto-discovered from the current source checkout |

`trial prepare` refuses to overwrite a non-empty target.

## Init Outputs

`trial prepare` materializes the workspace described by `docs/matrix/agent-trial-v1-official-pack.md`.

Required workspace outputs:

- `AGENTS.md`;
- `CLAUDE.md`;
- `README.md`;
- `tasks/`;
- `prompts/`;
- `src/`;
- `tests/`;
- `.carves/constraints/base.md`;
- `.carves/trial/pack.json`;
- `.carves/trial/challenge.json`;
- `.carves/trial/instruction-pack.json`;
- `.carves/trial/task-contract.json`;
- `artifacts/`.

`pack.json`, `challenge.json`, `instruction-pack.json`, and `task-contract.json` must be read-only evidence inputs for the agent task unless the task explicitly permits a metadata migration. The official V1 tasks do not permit that migration.

## Collect And Verify Outputs

`trial collect` writes local candidate evidence to:

```text
<workspace>/artifacts/diff-scope-summary.json
<workspace>/artifacts/test-evidence.json
<workspace>/artifacts/carves-agent-trial-result.json
```

`carves-agent-trial-result.json` contains the versioned local score mapping described in `docs/matrix/agent-trial-v1-local-score-mapping.md`.

It writes the Matrix trial bundle to the requested `--bundle-root`, defaulting to:

```text
<workspace>/artifacts/matrix-trial-bundle/
```

The bundle contains:

- `matrix-artifact-manifest.json`;
- `matrix-proof-summary.json`;
- `matrix-agent-trial-result-card.md`;
- minimal local Matrix readback artifacts under `project/`;
- manifest-covered trial artifacts under `trial/`.

The command exits successfully only when local collection is `collectable`. `trial local` exits successfully only when collection is `collectable` and strict Matrix verification passes. This keeps failed evidence collection visible even if the bundle itself is structurally verifiable.

## Challenge Recording

`challenge.json` records:

- challenge id;
- challenge source;
- suite id;
- pack id;
- pack version;
- task id;
- task version;
- prompt id;
- prompt version;
- issued at;
- expires at when server-issued;
- previous receipt hash when supplied;
- leaderboard eligibility posture.

Allowed challenge sources:

- `server_issued`;
- `pack_local_dry_run`;
- `cache_replay`.

Only `server_issued` challenges are eligible for official public leaderboard submission by default. Other sources can still produce local Matrix evidence.

## Task Contract Recording

`task-contract.json` must use `matrix-agent-task.v0`.

It binds:

- task id and version;
- prompt id and version;
- challenge id;
- allowed paths;
- forbidden paths;
- required commands;
- expected evidence;
- permission profile;
- stop-and-ask conditions.

The task contract hash is later included in the local collection result.

## Local-Only Boundary

The local trial commands must be usable without submission.

Default behavior:

- no source upload;
- no raw diff upload;
- no prompt response upload;
- no model response upload;
- no secret or credential material;
- no public leaderboard claim;
- no certification claim.

Network may be used to fetch an official pack or challenge. Running the local workspace must not require network by default.

## Failure Modes

The runner should fail closed for:

- unsupported suite;
- unresolved `latest` prompt version;
- missing official pack metadata;
- pack hash mismatch;
- existing non-empty workspace without explicit update mode;
- missing `AGENTS.md`, `CLAUDE.md`, constraints, task contract, or challenge metadata after materialization;
- forbidden source, secret, credential, or customer payload in the pack.

Failure should leave enough diagnostic information for the user to retry without creating a fake eligible result.

## CARD-910 Acceptance Mapping

`CARD-910` is satisfied by the current Matrix CLI command surface:

- the prepare/run/collect/verify command path is exposed through `carves-matrix trial`;
- materialized workspace outputs are defined;
- command output tells the user where to run the agent, where evidence is collected, and how to verify the bundle;
- local collection and strict Matrix verification stay separate;
- local score mapping is explicit, versioned, and local-only;
- a human-readable result card labels the output as local-only, unsubmitted, and non-certified while linking back to manifest-covered evidence;
- the pack can be run locally without uploading anything or contacting a server.
