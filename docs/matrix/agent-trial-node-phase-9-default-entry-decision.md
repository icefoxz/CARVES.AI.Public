# Agent Trial Node Phase 9 Default Entry Decision

Status: accepted as default-entry decision gate.

Date: 2026-04-20

Phase 9 records the product decision after the Phase 8 small user trial packet.
It does not invent user-trial evidence and does not promote the Node Windows
playable zip to the default public local trial entry.

Related records:

- `docs/matrix/agent-trial-node-phase-5-release-artifact.md`
- `docs/matrix/agent-trial-node-phase-6-operator-review.md`
- `docs/matrix/agent-trial-node-phase-7-public-onboarding.md`
- `docs/matrix/agent-trial-node-phase-8-small-user-trial.md`
- `docs/matrix/agent-trial-node-windows-playable-quickstart.md`

## Decision

Current decision:

```text
KEEP Node Windows playable as a parallel official local trial entry.
NO-GO for default public local trial promotion.
```

Reason:

```text
Phase 5 to Phase 7 proved artifact, operator path, and public copy readiness.
Phase 8 defined the small user trial packet.
No filled Phase 8 user-trial results exist yet.
```

Default-entry promotion needs user evidence, not another internal assertion.

## What Is Accepted

Accepted:

- the Node Windows playable zip remains an official parallel local trial entry;
- the public quickstart remains valid for users who already have the zip;
- the Phase 8 user-trial packet is the correct next evidence route;
- the existing source-checkout `carves test demo` / `carves test agent` path
  remains the default public local trial entry.

Not accepted:

- replacing the default public local trial entry;
- claiming external user trial completion;
- claiming hosted verification;
- claiming leaderboard eligibility;
- claiming certification;
- claiming package signing;
- claiming anti-cheat or tamper-proof local execution;
- claiming Linux/macOS playable support.

## Promotion Evidence Required

Before reconsidering default-entry promotion, add a filled result record:

```text
docs/matrix/agent-trial-node-phase-8-user-trial-results.md
```

That result record must include:

- at least 2 participant rows;
- at least one Node/Web developer;
- at least one AI coding agent user who has not read CARVES internals;
- whether each participant found the correct guide without help;
- whether each participant opened only `agent-workspace/`;
- whether each participant chose blind or guided mode correctly;
- whether `agent-workspace/artifacts/agent-report.json` was produced or the
  missing-report next step was understood;
- whether `SCORE.cmd`, `RESULT.cmd`, and `RESET.cmd` made sense;
- whether participants understood the result is local-only;
- whether maintainer help was needed;
- friction list;
- copy or package changes requested;
- explicit default-entry decision.

The record must not include private source, raw diff, prompt, model response,
secret, credential, or participant personal data.

## Promotion Gate

Default-entry promotion can be reconsidered only if:

- the filled result record exists;
- at least 2 qualifying participants complete or meaningfully exercise the
  flow;
- no participant needs hidden Runtime, Matrix, scorer, taskgraph, or governance
  knowledge before first success;
- successful participants open only `agent-workspace/`;
- successful participants understand `SCORE.cmd`, `RESULT.cmd`, and
  `RESET.cmd`;
- successful participants understand the local-only result boundary;
- no participant interprets the score as certification, hosted verification,
  leaderboard admission, package signing, anti-cheat, or tamper-proof execution.

If those conditions are met, a later decision may promote Node Windows playable
as the default public local trial entry.

## Blocking Gate

Keep the current parallel-entry posture if any of these occur:

- the result record is missing;
- fewer than 2 qualifying participant rows exist;
- participants repeatedly open the package root in the tested agent;
- participants cannot identify which prompt to paste;
- participants cannot recover from a missing `agent-report.json` diagnostic;
- participants misunderstand the local-only result boundary;
- setup requires hidden maintainer knowledge;
- setup friction points to unclear Git, Node.js, Windows shell, or zip
  extraction prerequisites;
- copy or command output creates certification, hosted verification,
  leaderboard, signing, anti-cheat, or tamper-proof expectations.

## Current Product Posture

The current public posture remains:

```text
Default local trial entry:
  source-checkout carves test demo / carves test agent

Parallel official local trial entry:
  Node Windows playable zip

Default-entry promotion:
  blocked until filled Phase 8 user-trial results exist and pass the gate
```

## Non-Claims

This Phase 9 decision does not claim:

- the small user trial has already run;
- public hosting;
- package signing;
- producer identity;
- certification;
- hosted verification;
- leaderboard eligibility;
- operating-system sandboxing;
- local anti-cheat;
- tamper-proof local execution;
- Linux/macOS playable support;
- replacement of the existing source-checkout default quickstart.

## Next Action

Run the Phase 8 small user trial and add:

```text
docs/matrix/agent-trial-node-phase-8-user-trial-results.md
```

After that result record exists, make one of three explicit decisions:

```text
promote Node Windows playable as default local trial entry
keep Node Windows playable as parallel official local trial entry
fix onboarding/package friction and rerun a smaller trial
```
