# Session Gateway Governed Agent Handoff Proof

The session gateway should use Runtime surfaces as the agent's collaboration rail.

## Agent-Facing Sequence

1. Ask Runtime for working-mode posture.
2. Start formal planning before durable write work.
3. Keep one active planning card.
4. Materialize task truth with an acceptance contract.
5. Select the recommended workspace or brokered execution mode.
6. Return bounded result evidence.
7. Inspect mutation audit and Mode E review preflight before approval.
8. Let Runtime perform review/writeback.

## Gateway Non-Authority

The gateway does not own task truth, memory truth, review truth, or writeback.

It can prompt, guide, and expose surfaces. It cannot convert returned worker material into official truth without Runtime review/writeback.

## Thin Alias

`agent handoff` is the human/agent friendly alias for the governed handoff proof.

It maps to `runtime-governed-agent-handoff-proof` and is read-side only.

Use `agent handoff --json` when an adapter needs machine-readable proof data.

`pilot guide` is the productized external-project pilot alias.

It maps to `runtime-product-closure-pilot-guide` and is read-side only. Use `pilot guide --json` when an external agent needs the ordered first-project loop as machine-readable stages.

`pilot status` is the productized external-project pilot status alias.

It maps to `runtime-product-closure-pilot-status` and is read-side only. Use `pilot status --json` before planning or editing so the agent can see the current stage, next governed command, and gaps.

`pilot commit-hygiene` is the productized external-project commit closure alias.

It maps to `runtime-target-commit-hygiene` and is read-side only. Use `pilot commit-hygiene --json` before staging or committing target repo changes so the agent can see official truth, target output candidates, local residue, and unclassified path blockers.

`pilot commit-plan` is the productized external-project commit staging-plan alias.

It maps to `runtime-target-commit-plan` and is read-side only. Use `pilot commit-plan --json` after writeback closure and before `git add` so the agent can see `stage_paths`, `excluded_paths`, `operator_review_required_paths`, and command previews.

`pilot closure` is the productized external-project post-commit closure alias.

It maps to `runtime-target-commit-closure` and is read-side only. Use `pilot closure --json` after the operator-reviewed target commit so the agent can see whether `commit_closure_complete` and `target_git_worktree_clean` are true.

`pilot residue` is the productized external-project target residue policy alias.

It maps to `runtime-target-residue-policy` and is read-side only. Use `pilot residue --json` after commit closure when `target_git_worktree_clean=false` but only excluded local/tooling residue remains.

`pilot ignore-plan` is the productized external-project target ignore decision-plan alias.

It maps to `runtime-target-ignore-decision-plan` and is read-side only. Use `pilot ignore-plan --json` after `pilot residue --json` so the agent can see operator-reviewed keep-local, cleanup, or `.gitignore` candidates without mutating `.gitignore`.

`pilot ignore-record` is the productized external-project target ignore decision-record alias.

It maps to `runtime-target-ignore-decision-record` and is read-side only. Use `pilot ignore-record --json` after `pilot ignore-plan --json` so the agent can see whether missing ignore candidates have durable operator decisions. If a decision is required, an operator can run `pilot record-ignore-decision <decision> --all --reason <text>`; CARVES records the decision under `.ai/runtime/target-ignore-decisions/` but still does not mutate `.gitignore`.

`pilot dist` is the productized external-project local distribution handoff alias.

It maps to `runtime-local-dist-handoff` and is read-side only. Use `pilot dist --json` to verify whether the target repo is consuming Runtime doctrine and CLI entry from a frozen local dist root instead of the live Runtime source tree.

`pilot proof` is the productized external-project final pilot proof alias.

It maps to `runtime-product-pilot-proof` and is read-side only. Use `pilot proof --json` after target commit closure and local dist handoff to aggregate both into one external-project pilot proof posture.

`pilot resources` is the productized external-consumer resource-pack alias.

It maps to `runtime-external-consumer-resource-pack` and is read-side only. Use `pilot resources --json` before planning or editing so the agent can see Runtime-owned docs, target-generated bootstrap projections, command entries, and boundaries without copying Runtime docs into target truth.

`pilot invocation` is the productized CLI invocation-contract alias.

It maps to `runtime-cli-invocation-contract` and is read-side only. Use `pilot invocation --json` before relying on a generic `carves` alias so the agent can distinguish source-tree wrapper, frozen local-dist wrapper, Windows cmd shim, and future global alias usage.

`pilot activation` and `pilot alias` are the productized CLI activation-plan aliases.

They map to `runtime-cli-activation-plan` and are read-side only. Use `pilot activation --json` after `pilot invocation --json` to see operator-owned activation choices without letting the agent edit PATH, shell profiles, or tool installation.

`pilot dist-smoke` and `pilot dist-freshness` are the productized local dist freshness-smoke aliases.

They map to `runtime-local-dist-freshness-smoke` and are read-side only. Use `pilot dist-smoke --json` before `pilot dist-binding --json` to prove the frozen local Runtime dist includes current resources and matches the clean Runtime source HEAD.

`pilot dist-binding` and `pilot bind-dist` are the productized target dist binding-plan aliases.

They map to `runtime-target-dist-binding-plan` and are read-side only. Use `pilot dist-binding --json` before claiming stable external-project consumption so the operator can see the frozen local dist wrapper path and agents do not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` manually.

`pilot target-proof` and `pilot external-proof` are the productized frozen dist target readback-proof aliases.

They map to `runtime-frozen-dist-target-readback-proof` and are read-side only. Use `pilot target-proof --json` after dist-smoke, dist-binding, and local dist handoff so the agent can verify the current target is initialized, bootstrapped, and bound to the fresh frozen Runtime dist.

## Product Closure Baseline

The current Phase 0 product closure baseline is recorded in `docs/runtime/carves-product-closure-phase-0-baseline.md`.

Session Gateway consumers should treat that baseline as the completed governed handoff floor and route remaining work into productization follow-ups instead of reopening the completed `CARD-706` through `CARD-718` chain.

The current Phase 1 CLI distribution baseline is recorded in `docs/runtime/carves-product-closure-phase-1-cli-distribution.md`.

External agents should prefer `carves agent start --json` as the first command in a new attached-target thread once the source-tree wrapper or local .NET tool package is available. `carves agent handoff` remains the lower-level governed handoff proof readback.

The current Phase 2 readiness boundary is recorded in `docs/runtime/carves-product-closure-phase-2-readiness-separation.md`.

External agents should use `carves doctor` when setup readiness is unclear, and reserve `carves status` for target-repo status after the repo boundary is known.

The current Phase 3 first-run onboarding boundary is recorded in `docs/runtime/carves-product-closure-phase-3-minimal-init-onboarding.md`.

External agents should use `carves init [path]` as the first-run attach wrapper for an existing git/workspace repo. If `init` reports no changes because the host is stopped, start the host and retry `init`; if `init` succeeds, continue with `carves doctor`, run `carves agent start --json`, and follow its `next_governed_command`. Use `carves agent handoff` and `carves inspect runtime-first-run-operator-packet` only when the start payload asks for deeper evidence.

The Phase 4 external target dogfood proof is recorded in `docs/runtime/carves-product-closure-phase-4-external-target-dogfood-proof.md`.

External agents working from an attached target repo should expect Runtime-owned handoff and first-run packet documents to resolve from the Runtime document root recorded in `.ai/runtime/attach-handshake.json` or `.ai/runtime.json`. They should not copy Runtime docs into the target repo or treat the target repo as the Runtime proof owner.

The Phase 5 real project pilot is recorded in `docs/runtime/carves-product-closure-phase-5-real-project-pilot.md`.

The Phase 6 real project official truth writeback is recorded in `docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md`.

The Phase 7 managed workspace execution proof is recorded in `docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md`.

External agents should not write the attached target repo directly after Phase 6. They should issue the managed workspace for the selected task, stay inside the lease scope, and return material through Runtime review/writeback.

The Phase 8 managed workspace writeback proof is recorded in `docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md`.

External agents should use `carves plan submit-workspace <task-id> [reason...]` after changing lease-scoped workspace files, then inspect `runtime-workspace-mutation-audit` and use `carves review approve <task-id> <reason...>` only when Runtime projects the result as writeback-ready.

The Phase 9 productized pilot guide is recorded in `docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md`.

External agents should start a new CARVES-controlled external-project pilot by reading `carves pilot guide` or `carves pilot guide --json`. The guide is not a mutator; it tells the agent which governed Runtime command owns each stage.

The Phase 10 productized pilot status is recorded in `docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md`.

External agents should run `carves pilot status` or `carves pilot status --json` before planning or editing. The status surface is not a mutator; it maps current repo state to the next governed command.

The Phase 11B target agent bootstrap pack is recorded in `docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md`.

Attach/init now materializes `.ai/AGENT_BOOTSTRAP.md` and creates root `AGENTS.md` only when missing. Existing root `AGENTS.md` remains target-owned and must not be overwritten by Runtime bootstrap projection.

The Phase 12 existing-target bootstrap repair is recorded in `docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md`.

Existing attached targets can now run `carves agent bootstrap` to inspect bootstrap readiness and `carves agent bootstrap --write` to materialize missing bootstrap files without rerunning attach. `pilot status` projects missing bootstrap files as a gap before agents proceed into planning or editing.

The Phase 13 target commit hygiene closure is recorded in `docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md`.

External agents should run `carves pilot commit-hygiene` before `git add` or `git commit`. The surface is not a mutator; it classifies dirty paths and requires operator review for unclassified paths.

The Phase 14 target commit plan closure is recorded in `docs/runtime/carves-product-closure-phase-14-target-commit-plan.md`.

External agents should run `carves pilot commit-plan` before `git add` or `git commit`. The surface is not a mutator; it projects a deterministic staging plan and blocks staging guidance when operator-review paths remain.

The Phase 15 target commit closure is recorded in `docs/runtime/carves-product-closure-phase-15-target-commit-closure.md`.

External agents should run `carves pilot closure --json` after the operator-reviewed target commit. The surface is not a mutator; it only verifies the post-commit clean readback.

The Phase 16 local dist handoff is recorded in `docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md`.

External agents should run `carves pilot dist --json` before treating a target repo as stable external-project consumption. The surface is not a mutator; it only reports whether the Runtime document root has the local dist manifest/version/wrapper shape and whether the current target is bound to that root.

The Phase 17 product pilot proof is recorded in `docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md`.

External agents should run `carves pilot proof --json` as the final read-only aggregate before declaring a target pilot closed. The surface is not a mutator; it combines local dist handoff and target commit closure postures into one readback.

The Phase 18 external consumer resource pack is recorded in `docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md`.

External agents should run `carves pilot resources --json` before planning or editing. The surface is not a mutator; it lists Runtime-owned resources, target-generated bootstrap files, and the boundary that no specific external target repo is the Runtime closure prerequisite.

The Phase 19 CLI invocation contract is recorded in `docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md`.

External agents should run `carves pilot invocation --json` before relying on a global `carves` alias. The surface is not a mutator; it only reports invocation lanes and Runtime-root authority.

The Phase 20 CLI activation plan is recorded in `docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md`.

External agents should run `carves pilot activation --json` before relying on a short `carves` command. The surface is not a mutator; it only reports activation lanes and operator-owned convenience boundaries.

The Phase 21 target dist binding plan is recorded in `docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md`.

External agents should run `carves pilot dist-binding --json` before treating a target repo as stable external-project consumption. The surface is not a mutator; it only reports candidate dist resources, current binding posture, operator binding commands, and manual-manifest-edit boundaries.

The Phase 22 local dist freshness smoke is recorded in `docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md`.

External agents should run `carves pilot dist-smoke --json` before `carves pilot dist-binding --json`. The surface is not a mutator; it only reports whether the candidate frozen local Runtime dist matches the clean Runtime source HEAD and contains the current Runtime-owned resources.

The Phase 23 frozen dist target readback proof is recorded in `docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md`.

External agents should run `carves pilot target-proof --json` before treating a target repo as stable external consumption. The surface is not a mutator; it only reports whether the initialized target, bootstrap files, freshness smoke, binding plan, and local dist handoff agree.

The Phase 24 wrapper Runtime root binding hardening is recorded in `docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md`.

External agents still call the same `target-proof` surface, but the dist wrapper now passes its own Runtime root into `init . --json` so the target cannot be accidentally attached to itself.

The Phase 25 external target product proof closure hardening is recorded in `docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md`.

External agents still must not stage or commit by themselves unless explicitly asked. The new behavior only makes CARVES classify attach/init-generated target truth so `commit-plan` can show a reviewable bootstrap commit path without treating every generated file as unknown.

Phase 26A projection cleanup keeps the active product closure phase aligned to `phase_25_external_target_product_proof_closure_ready` across handoff, pilot, commit, dist, and proof surfaces.

The Phase 26 real external repo pilot is recorded in `docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md`.

External agents may read Phase 26 as proof that the frozen local Runtime dist has been exercised against `CARVES.Operator` as a real sibling consumer repo. This does not make `CARVES.Operator` a Runtime truth owner and does not involve `CARVES.AgentCoach`.

The Phase 27 external target residue policy is recorded in `docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md`.

External agents should use `carves pilot residue --json` to understand whether excluded local/tooling residue may remain local and which `.gitignore` entries are operator-review candidates. The command is read-only and does not mutate `.gitignore`, clean files, stage paths, or commit target changes.

The Phase 28 target ignore decision plan is recorded in `docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md`.

External agents should use `carves pilot ignore-plan --json` before editing `.gitignore` for CARVES local/tooling residue. The command is read-only and exposes decision candidates plus a patch preview; an operator still chooses whether to keep residue local, clean it manually, or make a reviewed `.gitignore` edit. When missing entries require durable review, use `carves pilot ignore-record --json` and record the operator decision before final proof.

The Phase 29 target ignore decision record is recorded in `docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md`.

External agents should use `carves pilot record-ignore-decision <decision> ...` only for operator-reviewed choices. The record is target truth under `.ai/runtime/target-ignore-decisions/` and must be committed through normal target commit closure before it is used as final proof evidence.

The Phase 30 target ignore decision record audit is recorded in `docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md`.

External agents should treat `carves pilot ignore-record --json` as both the decision-record readback and the audit readback. Malformed, invalid, stale, or conflicting decision records do not satisfy current product proof; only well-formed, current-plan, non-conflicting records can make `decision_record_ready=true`.

The Phase 31 target ignore decision record commit readback is recorded in `docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md`.

The Phase 32 alpha external-use readiness rollup is recorded in `docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md`.

Use `carves pilot readiness [--json]`, or the thin alias `carves pilot alpha [--json]`, to read the Runtime-owned readiness rollup before asking an external agent to plan or edit through CARVES. This readback does not replace per-target proof; it only says whether the frozen local Runtime dist and Runtime-owned guidance are ready for bounded alpha use.

External agents should treat a freshly written decision record as incomplete until `decision_record_commit_ready=true`. If `carves pilot ignore-record --json` reports uncommitted decision record paths, the next action is target commit-plan and operator-reviewed commit closure, not final product proof.
