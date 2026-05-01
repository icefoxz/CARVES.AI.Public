# CARVES Pilot Support And Issue Intake

This guide defines the support and issue intake contract for limited CARVES product pilots.

It covers Guard Beta local-package pilots, Handoff Alpha local-package pilots, and Audit Alpha local-package pilots.

It does not create a public support program, SLA, paid support contract, public issue tracker, public security process, or production support claim.

## Product Maturity

| Product | Current maturity | Supported pilot surface |
| --- | --- | --- |
| CARVES Guard | Beta pilot | Diff-only `carves guard check`, `guard audit`, `guard report`, and `guard explain` through local package or source-tree use. |
| CARVES Handoff | Alpha pilot | Explicit packet-path `carves-handoff draft`, `inspect`, and `next` through local package use. |
| CARVES Audit | Alpha pilot | Read-only `carves-audit summary`, `timeline`, and `explain` over supplied Guard decision JSONL and explicit Handoff packets. |

The maturity labels are part of the support contract:

- Beta means suitable for bounded external pilots, not production/stable use.
- Alpha means suitable for narrow proof and dogfood, not production/stable use.
- Local package means installed from an explicit local nupkg source, not a public registry.

## Support Scope

### Guard Beta

Supported during limited pilots:

- local package install and tool-path execution
- policy loading and fail-closed policy errors
- allow, review, and block decision readback
- audit, report, and explain readbacks
- external repo pilot behavior over normal git working trees
- documented Beta boundaries and known limitations

Not supported:

- OS sandboxing
- filesystem virtualization
- syscall interception
- automatic rollback of arbitrary writes
- production CI enforcement guarantees
- stable `guard run` promotion
- semantic correctness or architecture review claims

### Handoff Alpha

Supported during limited pilots:

- explicit packet-path draft, inspect, and next commands
- fail-closed handling for malformed, missing, blocked, stale, or repo-mismatched packets
- local package install and tool-path execution
- separated incoming-session dogfood behavior
- no-write boundaries outside explicit draft packet paths

Not supported:

- default packet discovery
- Memory product state
- TaskGraph export
- multi-packet lifecycle management
- automatic context ranking
- evidence-backed ready-packet writer beyond the current skeleton draft command
- production resume guarantees

### Audit Alpha

Supported during limited pilots:

- summary, timeline, and explain over supplied input paths
- Guard decision JSONL as a supplied input
- explicit Handoff packets as supplied inputs
- degraded and input-error readback over malformed or missing inputs
- read-only behavior that does not mutate Guard or Handoff inputs

Not supported:

- durable Audit database
- dashboard
- compliance archive
- retention guarantee
- automatic input discovery
- complete historical observability
- production compliance reporting

## Issue Intake Fields

Pilot issues should be reported with these fields:

```text
product:
maturity:
package_id:
package_version:
tool_command:
install_channel:
target_repo_shape:
os:
dotnet_sdk_version:
command:
exit_code:
expected_behavior:
actual_behavior:
input_paths:
output_schema_version:
decision_or_handoff_id:
package_sha256:
write_boundary_observation:
reproduction_steps:
attachments_or_evidence:
security_sensitive:
```

Use `N/A` when a field is not applicable.

`security_sensitive` must be either:

```text
yes
no
unknown
```

## Product-Specific Evidence

Guard issue evidence should include:

- `.ai/guard-policy.json` with secrets removed
- relevant `guard check --json` output
- `guard audit --json` output when available
- `guard explain <run-id> --json` output when available
- changed-file summary

Handoff issue evidence should include:

- packet path
- `inspect --json` output
- `next --json` output when the issue involves resume projection
- whether the packet was ready, blocked, stale, repo-mismatched, or operator-review-required
- write-boundary observation after the command

Audit issue evidence should include:

- supplied Guard decision JSONL path
- supplied Handoff packet path
- `summary --json`, `timeline --json`, or `explain --json` output
- input error diagnostics when present
- confirmation that supplied inputs were not mutated

## Security Issue Path

Security-sensitive reports should not be posted to a public issue tracker by default during limited pilots.

For limited pilots, security-sensitive reports must go through the private pilot coordinator or operator-designated private channel.

If no private pilot coordinator or private channel exists, the issue is blocked from normal pilot intake until a security contact is established.

Before public release, CARVES still needs a public vulnerability reporting reference such as a `SECURITY.md` file or equivalent documented private reporting channel.

## Response Expectations

Limited pilots have no SLA.

Expected responses are best-effort and may be:

- acknowledged
- needs reproduction
- accepted as product bug
- accepted as documentation bug
- accepted as support-boundary clarification
- rejected as unsupported use
- deferred to a later phase
- security-sensitive, moved to private handling

Response language must preserve maturity boundaries. Do not promise production support, guaranteed turnaround, or long-term maintenance from pilot evidence.

## Unsupported Use Cases

The following are outside pilot support:

- public registry installation
- private remote feed installation
- unsigned public release use
- package signing claims without signed artifacts
- production deployment guarantees
- compliance certification
- broad AI agent orchestration
- Memory product workflows
- Runtime TaskGraph external product use
- OS sandbox or containment claims
- recovery of arbitrary filesystem writes
- issue reports without enough reproduction evidence

## Public Release Blockers

This guide satisfies the limited-pilot support and issue intake policy.

It does not satisfy every public support requirement.

Phase 45 accepts controlled internal pilot readiness only under local package bundle controls:

- package source commit or retained bundle identity is recorded
- SHA-256 is recorded for every pilot nupkg
- tools are installed with explicit `dotnet tool --tool-path`
- no public registry or private remote feed is used
- no production, stable, public support, or package signing claim is made
- issue reports use the intake fields in this guide
- security-sensitive reports stay on the private pilot path

Before public release, CARVES still needs:

- public repository or project issue destination
- public vulnerability reporting reference
- release-specific support scope
- release-specific known limitations
- production/stable support decision, even if that decision is explicitly no production support
