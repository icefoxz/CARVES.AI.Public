# CARVES Agent Trial V1 Agent Profile

Status: Phase 1 AgentProfile self-report freeze for the proposed Agent Trial V1 product line.

This document freezes the V1 AgentProfile boundary. It does not implement profile storage, model identity verification, agent binary verification, submissions, receipts, or leaderboards.

## Goal

AgentProfile lets a registered user attach trial submissions to a reusable self-reported agent/model/tool profile.

It answers:

```text
Which user-declared agent/profile produced this submitted result?
```

It does not answer:

```text
Was this actually that vendor model?
Was this agent binary unmodified?
Was this model identity certified?
```

## Public Rule

Public surfaces must label V1 agent/model identity as self-reported.

Use:

```text
self-reported agent profile
self-reported model label
reported reasoning depth
reported permission profile
```

Avoid:

```text
verified model
certified agent
official model identity
attested agent binary
```

## Minimum Fields

AgentProfile fields:

| Field | Required | Public When Result Public | Purpose |
| --- | --- | --- | --- |
| `agent_profile_id` | yes | no | Stable profile identity key |
| `user_id` | yes | no | Owner |
| `agent_label` | yes | yes | User-declared agent/tool name |
| `model_label` | yes | yes | User-declared model name |
| `reasoning_depth` | yes | yes | User-declared reasoning setting |
| `tool_profile` | yes | yes | User-declared tool capability summary |
| `permission_profile` | yes | yes | User-declared permission/sandbox summary |
| `os_summary` | yes | yes | Local operating environment summary |
| `self_reported` | yes | yes | Identity confidence marker |
| `created_at` | yes | no | Profile creation time |
| `updated_at` | yes | no | Latest profile update time |

V1 always sets:

```json
{ "self_reported": true }
```

## Suggested Value Sets

`reasoning_depth` values should be normalized where possible:

- `unknown`
- `low`
- `medium`
- `high`
- `max`
- `custom`

`tool_profile` values should summarize capability rather than enumerate private tool logs:

- `chat_only`
- `edit`
- `edit_shell`
- `edit_shell_network`
- `custom`

`permission_profile` values should describe the declared boundary:

- `read_only`
- `standard_bounded_edit`
- `workspace_write`
- `full_access`
- `custom`

`os_summary` should be coarse:

- `linux`
- `macos`
- `windows`
- `wsl`
- `container`
- `unknown`

These value sets are intentionally small for leaderboard grouping. Later versions can add structured detail.

## Snapshot Rule

Every TrialResult stores an immutable AgentProfile snapshot.

Snapshot fields:

- `agent_label`
- `model_label`
- `reasoning_depth`
- `tool_profile`
- `permission_profile`
- `os_summary`
- `self_reported`

Rules:

- Updating an AgentProfile does not rewrite old TrialResults.
- Leaderboards use the snapshot attached to each result.
- A result cannot enter agent/profile leaderboards without an AgentProfile snapshot.
- A user may create multiple AgentProfiles for different agents, models, reasoning depths, or permission profiles.

## Display Rules

Public leaderboard rows should display:

- display name or username when public profile is enabled;
- agent label;
- model label;
- reasoning depth;
- permission profile;
- verified run count;
- self-reported identity marker.

Example:

```text
alice | Claude Code | model label: user-reported | reasoning: high | permission: standard_bounded_edit | self-reported
```

If profile or user visibility is private, public leaderboards must not expose that profile.

## Submission Rules

Trial submission requires:

- registered User;
- AgentProfile selected or created;
- AgentProfile snapshot copied into TrialChallenge or TrialResult;
- challenge id bound to that snapshot;
- TrialResult includes the same snapshot or a server-approved equivalent.

If a user changes AgentProfile values after a challenge is issued, the system should either:

- keep the challenge-bound snapshot; or
- require a new challenge.

Silent profile drift between challenge and result should not be accepted for leaderboard use.

## Future Verification Boundary

Future versions may add stronger identity evidence:

- vendor OAuth;
- model provider signed run metadata;
- official agent binary hash;
- hosted runner attestation;
- API key provenance;
- GitHub Actions OIDC run evidence.

V1 does not include these. Public V1 language must remain self-reported.

## Example AgentProfile

```json
{
  "schema_version": "agent-profile.v0",
  "agent_profile_id": "ap_01",
  "agent_label": "Claude Code",
  "model_label": "Claude Sonnet user-reported",
  "reasoning_depth": "high",
  "tool_profile": "edit_shell",
  "permission_profile": "standard_bounded_edit",
  "os_summary": "linux",
  "self_reported": true
}
```

## Phase 1 Acceptance Mapping

TRIAL-V1-011 is satisfied by this document:

- users can create and reuse AgentProfiles;
- agent/model identity is labeled self-reported;
- AgentProfile snapshots are required for agent/profile leaderboard eligibility;
- profile updates do not rewrite historical TrialResults.
