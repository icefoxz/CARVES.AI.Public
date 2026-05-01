# CARVES.Handoff Docs

中文 | [English](#english)

CARVES.Handoff 是 AI session continuity packet 工具。它帮助上一个会话把“当前目标、已经完成的事实、剩余工作、禁止重复的坑、下一步建议”写成一个可检查的 JSON packet，让下一个会话不用从头摸索。

## 推荐阅读顺序

1. [五分钟上手](quickstart.zh-CN.md)
2. [英文五分钟上手](quickstart.en.md)
3. [分发说明](../guides/CARVES_HANDOFF_DISTRIBUTION.md)
4. [发布检查点](../release/handoff-publish-checkpoint.md)

## 默认路径

```text
.ai/handoff/handoff.json
```

最短命令：

```powershell
carves-handoff draft --json
carves-handoff inspect --json
carves-handoff next --json
```

默认 repo root：

- 未传 `--repo-root` 时，standalone `carves-handoff` 会从当前目录向上查找最近的 git repository。
- 如果向上没有找到 git repository，则使用当前目录。
- 需要固定目标仓库时，显式传 `--repo-root <path>`。

## 边界

Handoff 是 session continuity，不是 planner，也不是 memory database。它不拥有 Guard 或 Audit 的 truth，不会修改 Guard decisions，不会生成长期记忆，也不会自动决定下一步工作是否应该执行。

---

## English

CARVES.Handoff is an AI session continuity packet tool. It helps an outgoing session leave the current objective, completed facts, remaining work, must-not-repeat notes, and next guidance in an inspectable JSON packet.

## Recommended Reading Order

1. [Five-minute quickstart](quickstart.en.md)
2. [Chinese quickstart](quickstart.zh-CN.md)
3. [Distribution guide](../guides/CARVES_HANDOFF_DISTRIBUTION.md)
4. [Publish checkpoint](../release/handoff-publish-checkpoint.md)

## Default Path

```text
.ai/handoff/handoff.json
```

Shortest path:

```powershell
carves-handoff draft --json
carves-handoff inspect --json
carves-handoff next --json
```

Default repo root:

- Without `--repo-root`, standalone `carves-handoff` walks upward from the current directory to the nearest git repository.
- If no git repository is found, it uses the current directory.
- Pass `--repo-root <path>` when the target repository must be explicit.

## Boundary

Handoff is session continuity, not a planner and not a memory database. It does not own Guard or Audit truth, mutate Guard decisions, create long-term memory, or decide whether the next action should execute.
