# Agent Trial Node Phase 8 Small User Trial

Status: accepted for small user trial execution packet.

Date: 2026-04-20

Phase 8 turns the Phase 7 public onboarding copy into a small user trial packet.
It defines who to test with, what to ask them to do, what to observe, and what
evidence is required before the Node Windows playable zip can be reconsidered as
the default public local trial entry.

This record does not claim that external users have already completed the trial.
It is the governed packet for running that trial without inventing new product
claims.

Related records:

- `docs/matrix/agent-trial-node-phase-5-release-artifact.md`
- `docs/matrix/agent-trial-node-phase-6-operator-review.md`
- `docs/matrix/agent-trial-node-phase-7-public-onboarding.md`
- `docs/matrix/agent-trial-node-windows-playable-quickstart.md`

## Decision

Phase 8 is accepted as the small user trial execution packet.

Default public-entry promotion remains on hold until filled trial observations
exist and meet the promotion gate below.

## Trial Goal

Answer one product question:

```text
Can first-time or near-first-time users follow the Node Windows playable
quickstart, run the local trial, understand the result, and avoid opening the
wrong folder without maintainer help?
```

This is an onboarding trial, not a model benchmark.

## Participant Shape

Use 2 to 3 people.

Required mix:

- at least one Node/Web developer;
- at least one AI coding agent user who has not read CARVES internals;
- no participant should need Runtime, Matrix, scorer, fixture, taskgraph, or
  governance vocabulary to start.

Allowed substitute:

- a near-user reviewer may stand in for one participant if they follow the
  script without using CARVES internal knowledge.

Not valid as the full sample:

- only the maintainer who built the package;
- only people who already read the Phase 5 to Phase 7 internal records;
- only operator-path verification.

## Trial Input

Give each participant only:

- the Windows playable zip or its local artifact path;
- [Node Windows Playable Agent Trial Quickstart](agent-trial-node-windows-playable-quickstart.md);
- permission to use their normal AI coding agent or IDE agent.

Do not give them:

- Phase 5, Phase 6, or Phase 7 internal records;
- scorer implementation notes;
- Matrix command internals;
- extra verbal coaching unless the trial is explicitly marked as coached.

## Trial Script

The participant should:

1. Find the correct quickstart.
2. Extract `carves-agent-trial-pack-win-x64.zip`.
3. Read `README-FIRST.md`.
4. Open only `agent-workspace/` in their AI coding agent.
5. Choose blind or guided mode.
6. Paste exactly one prompt into the agent.
7. Let the agent finish the task.
8. Confirm whether `agent-workspace/artifacts/agent-report.json` exists.
9. Run `SCORE.cmd` from the package root.
10. Run `RESULT.cmd` after scoring.
11. Run `RESET.cmd` before another local practice run.
12. Explain, in their own words, what the local result does and does not prove.

The observer should avoid correcting the participant unless they are blocked.
Any correction should be recorded as a trial observation.

## Observation Form

Use one row per participant.

| Field | Value |
| --- | --- |
| Participant label | P1 / P2 / P3 |
| Participant shape | Node/Web developer, AI coding agent user, near-user reviewer |
| Prior CARVES internals exposure | none / light / heavy |
| OS | Windows version and shell |
| Agent used | tool name only, no prompt transcript |
| Found quickstart without help | yes / no |
| Opened only `agent-workspace/` | yes / no |
| Chose blind/guided correctly | yes / no |
| Agent wrote `artifacts/agent-report.json` | yes / no |
| `SCORE.cmd` completed | yes / no |
| `RESULT.cmd` made sense | yes / no |
| `RESET.cmd` made sense | yes / no |
| Understood local-only result boundary | yes / no |
| Needed maintainer help | none / minor / blocking |
| Main friction | short note |
| Proposed copy or package fix | short note |

Do not record source code, raw diffs, prompts, model responses, secrets,
credentials, or private project data.

## Passing Gate

Phase 8 trial evidence is strong enough to reconsider default-entry promotion
only if:

- at least 2 participant rows are filled;
- at least one participant is a Node/Web developer;
- at least one participant is an AI coding agent user who has not read CARVES
  internals;
- each successful participant opened only `agent-workspace/`;
- each successful participant produced or understood the missing-report next
  step for `agent-workspace/artifacts/agent-report.json`;
- each successful participant understood when to use `SCORE.cmd`,
  `RESULT.cmd`, and `RESET.cmd`;
- each successful participant understood that the result is local-only;
- no blocking setup issue required hidden maintainer knowledge;
- no participant was led to believe the result is certification, hosted
  verification, leaderboard admission, package signing, anti-cheat, or
  tamper-proof execution.

## Blocking Gate

Default-entry promotion is blocked if any of these occur:

- users repeatedly open the package root in the agent instead of
  `agent-workspace/`;
- users cannot identify which prompt to paste;
- users cannot recover from a missing `agent-report.json` diagnostic;
- users interpret the score as certification, hosted verification, leaderboard
  eligibility, package signing, anti-cheat, or tamper-proof execution;
- `SCORE.cmd`, `RESULT.cmd`, or `RESET.cmd` wording creates misleading next
  steps;
- Node.js, Git, zip extraction, or Windows shell prerequisites are unclear;
- the trial requires explaining Runtime, Matrix, scorer internals, taskgraph,
  or governance vocabulary before first success.

## Evidence Record Template

When the trial is run, add a bounded follow-up record rather than editing this
packet into a vague status note.

Suggested file:

```text
docs/matrix/agent-trial-node-phase-8-user-trial-results.md
```

Minimum sections:

- participant table;
- setup environment summary;
- pass/block summary;
- friction list;
- copy/package changes requested;
- explicit default-entry decision.

The result record must preserve local-only non-claims and must not contain
private source, raw diff, prompt, model response, secret, credential, or
participant personal data.

## Go / No-Go

Current recommendation:

```text
GO for running the bounded small user trial using this packet.
NO-GO for default public local trial promotion until filled Phase 8 results
exist and pass the gate.
```

## Non-Claims

This Phase 8 packet does not claim:

- the user trial has already been run;
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

## Next Decision

After filling `agent-trial-node-phase-8-user-trial-results.md`, make one of
three decisions:

```text
promote Node Windows playable as default local trial entry
keep Node Windows playable as parallel official local trial entry
fix onboarding/package friction and rerun a smaller trial
```

Until that result record exists, the correct product posture remains:

```text
parallel official local trial entry, not default public local trial entry
```
