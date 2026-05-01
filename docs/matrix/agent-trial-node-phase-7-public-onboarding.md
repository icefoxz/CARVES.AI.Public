# Agent Trial Node Phase 7 Public Onboarding

Status: accepted for public onboarding copy.

Date: 2026-04-20

Phase 7 turns the accepted Phase 6 operator-review path into beginner-facing
public copy. It does not promote the Node Windows playable zip to the default
public local trial entry.

Related records:

- `docs/matrix/agent-trial-node-phase-5-release-artifact.md`
- `docs/matrix/agent-trial-node-phase-6-operator-review.md`
- `docs/matrix/agent-trial-node-windows-playable-quickstart.md`
- `docs/matrix/agent-trial-v1-local-quickstart.md`

## Decision

Phase 7 is accepted.

The Node Windows playable quickstart is now documented as a parallel official
local trial entry for users who already have the Windows playable zip.

The existing source-checkout `carves test demo` / `carves test agent` path
remains the default public local trial entry.

Default-entry promotion remains a separate Phase 8+ product decision.

## Public Copy Added

New user-facing guide:

```text
docs/matrix/agent-trial-node-windows-playable-quickstart.md
```

Linked from:

- `README.md`
- `docs/matrix/README.md`
- `docs/matrix/agent-trial-v1-local-quickstart.md`

## Acceptance Checklist

### Beginner Entry

Expected:

- explains what the zip is without requiring CARVES internal vocabulary;
- tells users to open only `agent-workspace/` in the tested agent;
- explains the first run in 5 to 7 steps;
- names the zip and the current Phase 5 artifact record;
- points advanced users back to the source-checkout quickstart.

Result: pass.

### Prerequisites

Expected:

- names Windows x64;
- names Git on `PATH`;
- names Node.js on `PATH`;
- names zip extraction;
- names an AI coding agent or IDE agent.

Result: pass.

### Command Meaning

Expected:

- says what `SCORE.cmd` does;
- says what `RESULT.cmd` does;
- says what `RESET.cmd` does;
- says commands run from the package root, not from `agent-workspace/`;
- explains repeated score readback and reset before another local practice run.

Result: pass.

### Prompt Choice

Expected:

- explains blind mode for stricter comparison;
- explains guided mode for learning and practice;
- warns not to mix guided runs into strict comparisons.

Result: pass.

### Non-Claims

Expected docs do not imply:

- certification;
- hosted verification;
- leaderboard eligibility;
- package signing;
- producer identity;
- operating-system sandboxing;
- semantic source-code correctness;
- local anti-cheat;
- tamper-proof local execution.

Result: pass.

### Positioning Boundary

Expected:

- describes the Node Windows playable quickstart as a parallel official local
  trial entry;
- says it is not yet the default public local trial entry;
- keeps public hosting, signing, Linux/macOS playable support, and default-entry
  promotion out of scope.

Result: pass.

## Residual Risks

- Phase 7 is copy and routing only. It is not an external first-time user trial.
- The zip is still recorded as a local artifact, not a hosted public download.
- Linux/macOS playable packages remain out of scope.
- Default-entry promotion still needs Phase 8 user evidence.

## Go / No-Go

Recommendation:

```text
GO for Phase 8 small user trial planning.
NO-GO for default public local trial promotion until Phase 8 evidence exists.
```

Rationale:

- The public copy now gives first-time users the short Windows Node playable
  path.
- The copy preserves the local-only boundaries and non-claims.
- The remaining question is whether real first-time users can follow the copy
  without maintainer help.

## Phase 8 Entry Packet

Recommended next card:

```text
CARD-NODE-STARTER-PHASE-8
Run small user trial for Node Windows playable onboarding
```

Recommended sample:

- 2 to 3 users or near-user reviewers;
- at least one Node/Web developer;
- at least one AI coding agent user who has not read CARVES internals.

Recommended observations:

- did they find the right guide;
- did they understand that the zip is a parallel official local trial entry;
- did they open only `agent-workspace/`;
- did they choose blind vs guided mode correctly;
- did the agent write `artifacts/agent-report.json`;
- did `SCORE.cmd`, `RESULT.cmd`, and `RESET.cmd` make sense;
- did they understand that the result is local-only;
- where they got stuck.

Default-entry promotion should wait for this evidence.
