# 用 Agent 启动 CARVES

语言：[En](START_CARVES.md)

这个文件是 AI 编码 agent 使用的 CARVES Runtime 根入口。

## 要做什么

1. 确认你知道这个 `START_CARVES.md` 文件的绝对路径。
2. 把包含这个文件的目录视为 `runtime_root`。
3. 如果目标项目根目录还不明确，先让 operator 确认。
4. 不要把全局 `carves` alias 当成权威，也不要在整台机器上搜索另一个 CARVES 安装。使用这个文件路径作为选定的 Runtime root。
5. 运行：

```bash
<runtime_root>/carves up <target_project>
```

6. 阅读 `carves up` 结果。把 `target_project_classification`、`target_classification_owner`、`target_startup_mode`、`target_runtime_binding_status`、`existing_project_handling` 视为 CARVES 拥有的启动事实。
7. 如果 `carves up` 返回 `ready_for_agent_start`，打开目标项目根目录，阅读 `CARVES_START.md`，然后运行：

```bash
.carves/carves agent start --json
```

8. 在 `agent start` readback 中重新检查 `startup_boundary_ready`、`startup_boundary_posture`、`startup_boundary_gaps`、`target_project_classification`、`target_classification_owner`、`target_startup_mode`、`target_runtime_binding_status`、`target_bound_runtime_root`、`agent_target_classification_allowed`、`agent_runtime_rebind_allowed`。
9. 如果 `startup_boundary_ready=false` 或 `thread_start_ready=false`，停止并把 CARVES 输出展示给 operator。不要自行计划、编辑、rebind 或修复 bootstrap。
10. 按 CARVES 输出继续。在 `agent start` readback 之前不要计划或编辑。

## 可见 Gateway 预期

G0 visible gateway contract 位于：

```text
docs/runtime/runtime-project-recenter-carves-g0-visible-gateway-contract.md
```

如果 operator 询问可见 CARVES gateway，先说明当前安全路径：运行 `<runtime_root>/carves up <target_project>`，打开目标项目，然后运行 `.carves/carves agent start --json`。`carves gateway`、`carves gateway status`、`carves status --watch` 和 Host `/` 是 visibility surface。它们必须显示 running/waiting/blocked heartbeat，但不能成为 worker execution authority 或 lifecycle truth authority。

如果 operator 询问如何让 `carves` 全局可调用，运行：

```bash
<runtime_root>/carves shim
```

这会打印一个由人负责采用的 shim pattern，把 `CARVES_RUNTIME_ROOT` 固定到这个 Runtime root。它只是指导：不会安装文件、修改 PATH、调度 worker automation，或替代 `.carves/carves agent start --json`。

## 如果你不知道这个文件路径

停止，并向 operator 索要 `START_CARVES.md` 的绝对路径。

不要根据记忆、PATH、shell history、相邻目录或大范围文件系统搜索来推断 `runtime_root`。

## 不要做什么

- 不要自行判断目标是新项目还是旧项目。
- 不要自行修复过期 binding。
- 不要编辑 `.ai/runtime.json` 或 `.ai/runtime/attach-handshake.json` 来 rebind 项目。
- 不要覆盖目标项目拥有的 `AGENTS.md`。
- 不要直接编辑 `.ai/` truth。
- 不要调度 worker automation、API/SDK worker execution、review approval、state sync、merge 或 release。

`carves up` 拥有目标分类和启动阻塞判断。如果它返回 `blocked`、`rebind_required` 或任何其他不安全状态，把输出展示给 operator 并停止。
