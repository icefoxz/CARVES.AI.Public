# CARVES Agent Trial V1 Prompt Versioning

Status: Phase 2 prompt versioning freeze for the proposed Agent Trial V1 product line.

This document freezes how official V1 prompts are identified, versioned, issued, and compared. It does not implement prompt serving, challenge APIs, task execution, submission, receipts, or leaderboards.

## Goal

Prompt versions make Agent Trial results comparable.

The rule is simple:

```text
Every accepted result must say exactly which prompt id and prompt version was used.
```

Results from different prompt versions can be analyzed together only when the UI or API explicitly says it is a cross-version aggregate.

## Prompt Identity

Every official prompt has:

- `prompt_id`;
- `prompt_version`;
- `suite_id`;
- `pack_id`;
- `task_id`;
- `instruction_class`;
- `prompt_text_hash`;
- `prompt_text_included`;
- `created_at`.

Example:

```json
{
  "schema_version": "trial-prompt.v0",
  "suite_id": "official-agent-dev-safety",
  "pack_id": "official-agent-dev-safety-v1",
  "task_id": "official-v1-task-001-bounded-edit",
  "prompt_id": "official-v1-bounded-edit",
  "prompt_version": "1.0.0",
  "instruction_class": "bounded_edit",
  "prompt_text_hash": "sha256:...",
  "prompt_text_included": true
}
```

## Versioning Rules

Prompt versions are immutable once accepted results exist.

New prompt version required when changing:

- task instruction wording that can affect behavior;
- allowed or forbidden path wording;
- required command wording;
- completion criteria;
- failure handling instructions;
- stop-and-ask instructions;
- evidence submission instructions.

Patch metadata only is allowed for:

- typo fixes that do not change meaning;
- formatting changes that do not change meaning;
- additional explanatory docs outside the prompt body.

When unsure, create a new prompt version.

## Prompt Issue Modes

V1 allows two issue modes:

1. pack-issued prompt;
2. server-issued prompt.

Pack-issued prompts live in the official pack and can be used for local dry runs.

Server-issued prompts are bound to a TrialChallenge and are required for public leaderboard submission.

Both modes must preserve:

- prompt id;
- prompt version;
- prompt hash;
- task id;
- task version.

## Prompt Text Visibility

Official V1 prompts can be public by default, but the model must allow redaction later.

Fields:

- `prompt_text_included=true`: prompt text is included in the pack or public prompt readback.
- `prompt_text_included=false`: prompt text is not publicly displayed, but prompt hash and version remain public.

Accepted results do not need to upload the prompt text back to the server. The result only needs to bind to prompt id, prompt version, and server-issued challenge id.

## Challenge Binding

Every TrialChallenge must bind:

- prompt id;
- prompt version;
- prompt hash;
- task id;
- task version;
- pack version;
- AgentProfile snapshot;
- user id.

Every TrialResult must carry the same prompt id/version binding or a server-approved equivalent from the challenge.

Silent prompt drift between challenge and result is not leaderboard eligible.

## Leaderboard Grouping

Prompt version leaderboards group by:

- `suite_id`;
- `pack_id`;
- `pack_version`;
- `task_pack_version`;
- `prompt_id`;
- `prompt_version`.

Prompt leaderboard metrics:

- median score;
- completion rate;
- scope violation rate;
- test evidence rate;
- report honesty rate;
- initial spike rate.

Leaderboards must not silently mix prompt versions. Cross-version analytics must say they are cross-version analytics.

## Prompt Quality Signals

Prompt versions should be evaluated by:

- stability: same agent/profile gets similar results across reruns;
- discrimination: different agents/profiles produce meaningfully different results;
- false-positive rate: good runs are not mislabeled as failures;
- false-negative rate: weak runs are not mislabeled as safe;
- report-honesty pressure: prompt exposes false success claims;
- scope pressure: prompt exposes boundary violations.

These are analytics signals, not certification claims.

## Deprecated Prompts

A prompt can be deprecated without deleting its historical results.

Deprecation rules:

- old results remain bound to old prompt version;
- old leaderboard snapshots remain explainable;
- new challenges should not issue deprecated prompt versions unless explicitly requested;
- official docs should explain the replacement version.

## Phase 2 Acceptance Mapping

TRIAL-V1-022 is satisfied by this document:

- every official prompt has prompt id and prompt version;
- prompt text can be server-issued or pack-issued;
- results cannot be compared across prompt versions without showing the prompt version;
- prompt version leaderboard grouping keys are stable.
