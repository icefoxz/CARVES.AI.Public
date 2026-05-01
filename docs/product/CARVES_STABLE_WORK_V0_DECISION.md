# CARVES Stable Work v0.0 Decision

Status: proposed policy bundle design

Runtime authority: none

This document records the human decision for CARVES Stable Work v0.0 as a proposed policy bundle. It is not active default governance policy. It does not authorize file submission, default governance entry changes, AGENTS.md changes, initialization report changes, or retroactive approval of any existing mixed diff.

## Decision

Adopt CARVES Stable Work v0.0 as a Minimal Split Authority policy bundle design.

Do not adopt Planning Card Portfolio v0, seven formal planning card types, or a card maturity state machine for v0.0.

The accepted direction is:

- Markdown records human decision and rationale.
- JSON records machine policy, repository profile, and fixture contract.
- Agent-facing rules remain a candidate projection and do not own truth.
- Activation is a later governed event.

## Hard Boundaries

Design approval is not file creation.

File creation is not activation.

Activation is not retroactive approval.

The v0.0 policy bundle has no runtime authority until activation requirements pass through a governed activation process.

## User Entry

The user-facing entry should remain simple:

```text
现在到哪了？
```

From there CARVES should explain current status, next action, risks, and decisions needed. Users should not need to understand internal task graph, worker routing, or policy mechanics before getting useful direction.

## Problem Being Solved

Work Steering explains how a user drives the work, but it does not decide whether an input is eligible for execution.

CARVES must distinguish:

- direction input
- context
- prototype evidence
- artifact output
- contract change
- recovery work
- executable task work
- policy change

The key rule is:

```text
Not every input is an executable task.
```

## Explicit Rejections

The following are rejected for v0.0:

- Planning Card Portfolio v0
- seven-card taxonomy
- card maturity state machine
- Micro Card as a new task species
- one giant JSON file as all truth
- AGENTS.md owning full policy
- README owning runtime contract
- unchecked generated projection
- Agent inference as truth

## Adopted Shape

The v0.0 bundle consists of:

- `docs/product/CARVES_STABLE_WORK_V0_DECISION.md`
- `docs/product/policy/CARVES_EXECUTION_INTAKE_POLICY_V0.json`
- `docs/product/policy/CARVES_RUNTIME_POLICY_PROFILE_V0.json`
- `docs/product/policy/CARVES_GOVERNANCE_GATE_FIXTURES_V0.json`
- `docs/product/CARVES_STABLE_WORK_V0_AGENT_RULES_CANDIDATE.md`

These files are proposed governance assets only. They do not activate any default gate.

## Current Mixed Diff Rule

The current mixed diff cannot be legalized by this policy bundle.

It must remain recovery-only and may be treated only as unauthorized prototype evidence until a governed recovery and contract-change path accepts or rejects each part.

It must not be:

- marked completed
- attached to a completed card
- submitted as governed work
- justified by a release artifact
- reconciled by hand-editing Markdown projection to match a desired state

## Activation Later

Activation requires a separate governed process with evidence:

- human decision accepted
- policy JSON valid
- runtime profile JSON valid
- fixture contract upgraded to executable input snapshots
- approved evaluator or validation method
- blocking fixtures pass
- projection validator passes
- activation record written by governed activation process

Until then, this bundle remains proposed and non-active.
