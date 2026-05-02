# Start CARVES With An Agent

Language: [Chinese](START_CARVES.zh-CN.md)

This file is the CARVES Runtime root entry for an AI coding agent.

## What To Do

1. Confirm you know the absolute path of this `START_CARVES.md` file.
2. Treat the directory containing this file as `runtime_root`.
3. Ask the operator to confirm the target project root if it is not already explicit.
4. Do not treat a global `carves` alias as authority and do not search the whole machine for another CARVES install. Use this file path as the selected Runtime root.
5. Run:

```bash
<runtime_root>/carves up <target_project>
```

6. Read the `carves up` result. Treat `target_project_classification`, `target_classification_owner`, `target_startup_mode`, `target_runtime_binding_status`, and `existing_project_handling` as CARVES-owned startup facts.
7. If `carves up` reports `ready_for_agent_start`, open the target project root, read `CARVES_START.md`, then run:

```bash
.carves/carves agent start --json
```

8. In the `agent start` readback, re-check `startup_boundary_ready`, `startup_boundary_posture`, `startup_boundary_gaps`, `target_project_classification`, `target_classification_owner`, `target_startup_mode`, `target_runtime_binding_status`, `target_bound_runtime_root`, `agent_target_classification_allowed`, and `agent_runtime_rebind_allowed`.
9. If `startup_boundary_ready=false` or `thread_start_ready=false`, stop and show CARVES output to the operator. Do not plan, edit, rebind, or bootstrap-repair by yourself.
10. Follow CARVES output. Do not plan or edit before the `agent start` readback.

## Visible Gateway Expectation

The G0 visible gateway contract lives at:

```text
docs/runtime/runtime-project-recenter-carves-g0-visible-gateway-contract.md
```

If the operator asks for a visible CARVES gateway, explain the current safe path
first: run `<runtime_root>/carves up <target_project>`, open the target project,
then run `.carves/carves agent start --json`. `carves gateway`,
`carves gateway status`, `carves status --watch`, and Host `/` are visibility
surfaces. They must show a running/waiting/blocked heartbeat, but they must not
become worker execution authority or lifecycle truth authority.

If the operator asks how to make `carves` callable globally, run:

```bash
<runtime_root>/carves shim
```

This prints a human-owned shim pattern that pins `CARVES_RUNTIME_ROOT` to this
Runtime root. It is guidance only: it does not install files, mutate PATH,
dispatch worker automation, or replace `.carves/carves agent start --json`.

## If You Do Not Know This File Path

Stop and ask the operator for the absolute path to `START_CARVES.md`.

Do not infer `runtime_root` from memory, PATH, shell history, sibling folders, or a broad filesystem search.

## What Not To Do

- Do not classify the target as new or old by yourself.
- Do not repair stale bindings by yourself.
- Do not edit `.ai/runtime.json` or `.ai/runtime/attach-handshake.json` to rebind a project by yourself.
- Do not overwrite target-owned `AGENTS.md`.
- Do not edit `.ai/` truth directly.
- Do not dispatch worker automation, API/SDK worker execution, review approval, state sync, merge, or release.

`carves up` owns target classification and startup blocking. If it reports `blocked`, `rebind_required`, or any other unsafe state, show the output to the operator and stop.
