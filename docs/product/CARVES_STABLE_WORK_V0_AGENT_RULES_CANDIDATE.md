DRAFT PROJECTION CANDIDATE - NOT ACTIVE - NOT TRUTH

# CARVES Stable Work v0.0 Agent Rules Candidate

Truth role: projection_candidate_not_truth

Policy status: proposed

Runtime authority: none

Source files:

- `docs/product/CARVES_STABLE_WORK_V0_DECISION.md`
- `docs/product/policy/CARVES_EXECUTION_INTAKE_POLICY_V0.json`
- `docs/product/policy/CARVES_RUNTIME_POLICY_PROFILE_V0.json`
- `docs/product/policy/CARVES_GOVERNANCE_GATE_FIXTURES_V0.json`

This file is a compact candidate projection for future agent-facing use. It is not active policy. It must not be referenced by `AGENTS.md` as active policy until governed activation passes.

## Candidate Rules

- Before execution, classify the request.
- Direct user instruction is not automatically task authorization.
- Not every input is an executable task.
- Low-risk, scoped, non-contract changes may use direct lightweight execution.
- Lightweight execution does not include submit, truth write, completion, or artifact write permission.
- Bug fixes, tools, scripts, prototypes, and subproject work usually require at least a lightweight task summary unless narrowly scoped and low risk.
- High-risk or product-contract changes require governed-card-first handling.
- Policy surface changes require governed-card-first handling after activation.
- Completed cards cannot accept new scope.
- Mixed lifecycle or uncarded diff requires recovery first.
- Tool and subproject work must declare `scope_domain` and must not be treated as main project completion.
- `scope_domain` is a write boundary, not a label.
- Derived artifacts cannot replace card or task truth.
- Markdown projection cannot override machine truth.
- Host unavailable or Host route unverified blocks governed completion unless scoped fallback is explicitly authorized.
- Agent inference is not truth.
- Prototype work is not completion.
- Current mixed diff cannot be legalized by this proposed bundle.
- This proposed bundle has no runtime authority before activation.

## Projection Validator Contract

This section describes the candidate projection check. It does not run the check and does not make this file a checked projection.

Validator output:

```json
{
  "status": "pass | fail",
  "reason_codes": [],
  "missing_rules": [],
  "overreach_rules": [],
  "version_mismatches": [],
  "forbidden_claims": []
}
```

The validator must fail if this candidate:

- removes or changes the first line `DRAFT PROJECTION CANDIDATE - NOT ACTIVE - NOT TRUTH`
- stops declaring `projection_candidate_not_truth`
- claims policy is active before governed activation
- claims runtime authority before governed activation
- grants permissions absent from the machine policy JSON
- softens submit, truth write, completion, artifact, or prototype limits
- omits policy self-governance
- omits current mixed diff non-retroactivity
- claims blocking fixtures passed before recorded fixture results exist
- claims `AGENTS.md` may reference this file before activation
- mismatches policy, profile, or fixture-set versions

A passing projection check would still not authorize activation, `AGENTS.md` changes, initialization reporting changes, default governance entry changes, submit, or current mixed diff approval.

## Activation Reminder

Activation requires a separate governed process. Until then:

- do not modify `AGENTS.md` to reference this as active policy
- do not modify initialization reporting
- do not change default execution behavior
- do not claim policy active
- do not submit current mixed diff as governed work
