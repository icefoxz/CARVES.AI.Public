CHECKED PROJECTION - ACTIVE POLICY - NOT TRUTH

# CARVES Stable Work v0.0 Agent Rules

Truth role: checked_projection_not_truth

Policy status: active

Runtime authority: policy_bundle_only

Policy version: carves.execution_intake_policy.v0

Profile version: carves.runtime_policy_profile.v0

Fixture set version: carves.governance_gate_regression.v0

Source files:

- `docs/product/CARVES_STABLE_WORK_V0_DECISION.md`
- `docs/product/policy/CARVES_EXECUTION_INTAKE_POLICY_V0.json`
- `docs/product/policy/CARVES_RUNTIME_POLICY_PROFILE_V0.json`
- `docs/product/policy/CARVES_GOVERNANCE_GATE_FIXTURES_V0.json`

This file is a compact checked projection for default-entry agents. It is not
policy truth. Machine policy truth remains in
`docs/product/policy/CARVES_EXECUTION_INTAKE_POLICY_V0.json`.

This projection must not grant permissions absent from policy JSON, override
task graph truth, override Host-routed lifecycle actions, override review or
writeback gates, or override `.ai` canonical truth.

## Compact Rules

- Before execution, classify the request through the active intake policy.
- Direct user instruction is not automatically task authorization.
- Not every input is an executable task.
- Low-risk, scoped, non-contract changes may use direct lightweight execution.
- Lightweight execution does not include submit, truth write, completion, or artifact write permission.
- Bug fixes, tools, scripts, prototypes, and subproject work usually require at least a lightweight task summary unless narrowly scoped and low risk.
- High-risk or product-contract changes require governed-card-first handling.
- Policy surface changes require a governed route.
- Completed cards cannot accept new scope.
- Mixed lifecycle or uncarded diff requires recovery first.
- Tool and subproject work must declare `scope_domain` and must not be treated as main project completion.
- `scope_domain` is a write boundary, not a label.
- Derived artifacts cannot replace card or task truth.
- Markdown projection cannot override machine truth.
- Host unavailable or Host route unverified blocks governed completion unless scoped fallback is explicitly authorized.
- Agent inference is not truth.
- Prototype work is not completion.
- Current mixed diff cannot be legalized by this active policy bundle.
- Active policy does not authorize submit, commit, merge, or current mixed diff approval.
- Active policy does not authorize worker SDK/API implementation.

## Default Entry Use

`AGENTS.md` may reference this checked projection as a short default-entry
surface after the governed default-entry rollout. That reference does not make
this file truth, and it does not replace policy JSON, runtime profile JSON,
fixtures, task graph truth, Host-routed lifecycle actions, review gates, or
writeback gates.
