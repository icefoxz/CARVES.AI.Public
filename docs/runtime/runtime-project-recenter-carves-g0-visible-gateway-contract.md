# Runtime Project Recenter CARVES G0 Visible Gateway Contract

Status: carves_g0_visible_gateway_contract

## Problem

UX7 made agent-started CARVES safer, but it still does not match the product
signal users expect from OpenClaw/Hermes-style CLI gateways. If the operator runs
`carves`, opens the Host root URL, or asks an agent to start CARVES and sees
silence, a raw route error, or only hidden JSON diagnostics, the product feels
dead even when the Host is actually running.

G0 does not claim implementation of the final gateway UX. It freezes the user
visible contract that later slices must satisfy.

## Product Rule

Every startup or gateway surface must answer these questions in plain language:

- Is CARVES installed or reachable?
- Is CARVES Host running?
- Which `runtime_root` is selected?
- Which `target_project` is selected?
- Is the target a new project, an existing CARVES project, or blocked for
  operator rebind?
- What command was accepted, rejected, or still waiting?
- What should the human do next?
- What should the agent do next?
- What is explicitly not authorized?

No user-facing startup surface should be silent, look like a raw developer
exception, or require the user to already understand Host internals.

## Surface Contract

`carves` with no args:

- Must show a friendly landing screen instead of doing nothing.
- Must point to `carves up <target-project>` as the product startup path.
- Must explain how to reach a visible gateway/status mode once implemented.
- Must not imply that global `carves` is lifecycle truth authority.

`carves up <target-project>`:

- Remains the safe startup command for new and old projects.
- Must report `runtime_root`, `target_project`, Host readiness, target
  classification, binding posture, human next action, and agent next action.
- Must keep `.carves/carves agent start --json` as the project-local agent entry.
- Must not dispatch worker automation or API/SDK worker execution.

`carves gateway`:

- Should be the foreground visible gateway mode once implemented.
- Must show a heartbeat-style "CARVES gateway is running" signal.
- Must show the selected Runtime root and attached target project when known.
- Must show last command, current blocker, and next safe command.
- Must remain a routing/status gateway, not a worker execution authority.

`carves gateway status` and `carves status --watch`:

- Should provide read-only gateway/Host status.
- Must be safe to run repeatedly from a target project.
- Must not mutate task truth, review truth, `.ai` truth, state sync, or worker
  execution state.

Host `/`:

- Must never return only a raw `Unknown host route '/'` message to a browser.
- Must show a readable "CARVES Host is running" pointer.
- Must say it is not_dashboard and not the normal project startup path.
- Must point the user back to `carves up <target-project>` and `start CARVES`.

## Global CLI Boundary

A future global `carves` command can be useful as a locator/dispatcher, but it is
not truth authority. The safest target entry remains the generated
project-local launcher:

```bash
.carves/carves agent start --json
```

`START_CARVES.md` remains the best agent bootstrap instruction after CARVES is
unzipped because it pins the selected Runtime root. Agents must not search the
whole machine for CARVES installs or silently switch Runtime roots.

## Non-Claims

G0 does not authorize:

- dashboard-first onboarding
- API/SDK worker execution
- worker automation dispatch
- Host-routed review approval
- task completion claims
- state sync
- merge or release
- direct `.ai` truth writeback
- treating global `carves` as lifecycle truth authority

Current Runtime posture remains:

```text
null_worker_current_version_no_api_sdk_worker_execution
```

## Acceptance

The contract is satisfied only when startup documentation and acceptance tests
keep the following product promises visible:

- `carves` must not be silent.
- `carves up` remains the safe product startup path.
- `carves gateway` is the planned visible foreground gateway lane.
- `carves gateway status` and `carves status --watch` are read-only visibility
  lanes.
- Host `/` is a running-status pointer, not_dashboard.
- Gateway visibility never becomes worker execution authority.
