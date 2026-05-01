using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static string BuildPrompt(WorkerExecutionRequest request)
    {
        var sections = new List<string>();
        var delegatedWorktreeGuidance = BuildDelegatedWorktreeGuidance(request);
        if (!string.IsNullOrWhiteSpace(delegatedWorktreeGuidance))
        {
            sections.Add(delegatedWorktreeGuidance);
        }

        if (!string.IsNullOrWhiteSpace(request.Instructions))
        {
            sections.Add(request.Instructions);
        }

        if (!string.IsNullOrWhiteSpace(request.Input))
        {
            sections.Add(request.Input);
        }

        return string.Join($"{Environment.NewLine}{Environment.NewLine}", sections);
    }

    private static string? BuildDelegatedWorktreeGuidance(WorkerExecutionRequest request)
    {
        if (!RequiresRepoRootControlPlaneGuidance(request))
        {
            return null;
        }

        var lines = new List<string>
        {
            "Execution environment note:",
            $"- Your current working directory is the delegated worktree: {request.WorktreeRoot}",
            $"- The authoritative repo root is also mounted and readable at: {request.RepoRoot}",
            "- Control-plane truth under `.ai/` is not materialized in the delegated worktree snapshot.",
            "- Host-governed cold initialization and task packet assembly are already satisfied for this delegated run; do not repeat repo bootstrap as shell preflight.",
        };

        var taskTruthPath = ResolveAuthoritativeTaskTruthPath(request);
        if (!string.IsNullOrWhiteSpace(taskTruthPath))
        {
            lines.Add($"- The authoritative task truth for this run is already readable at: {taskTruthPath}");
        }

        var cardId = TryResolveCardIdFromTaskId(request.TaskId);
        var cardTruthPath = ResolveAuthoritativeCardTruthPath(request, cardId);
        if (!string.IsNullOrWhiteSpace(cardTruthPath))
        {
            lines.Add($"- The authoritative card truth for this run is already readable at: {cardTruthPath}");
        }

        if (!string.IsNullOrWhiteSpace(taskTruthPath) || !string.IsNullOrWhiteSpace(cardTruthPath))
        {
            var rediscoveryTargets = string.IsNullOrWhiteSpace(cardId)
                ? $"`{request.TaskId}`"
                : $"`{request.TaskId}` / `{cardId}`";
            lines.Add($"- Do not enumerate `.ai/tasks/cards`, `.ai/tasks/nodes`, or search for {rediscoveryTargets} just to rediscover the current scope; open the direct authoritative path(s) above instead.");
        }

        lines.Add("- Treat the supplied context pack, execution packet, and direct task/card truth paths as the startup entry point for this run.");
        lines.Add("- Unless the task explicitly targets repo-governance sources, do not spend startup budget rereading `README.md`, `AGENTS.md`, `.ai/memory/architecture/*`, `.ai/PROJECT_BOUNDARY.md`, or `.ai/STATE.md`.");
        lines.Add("- Do not emit a `CARVES.AI initialization report`, `Agent bootstrap sources`, or a planning-only preamble in this delegated run; use the budget on bounded implementation work or a concrete blocker.");
        lines.Add("- Delegated managed worktrees may not carry `.git`; do not run `git status` or `git diff` there as routine patch verification. Verify edited files directly with `sed -n`, `rg`, `cat`, `head`, `tail`, or `test -f`, and report changed paths in your final summary.");
        lines.Add("- Do not use `python3` as a bulk read harness to print task truth or dump multiple files during preflight. Use it only for bounded file edits; for inspection, keep reads targeted with `sed -n` or `rg`.");
        lines.Add("- Before the first edit, do not concatenate broad `sed -n`/`cat` bundles or keep widening repeated `rg` sweeps across related modules. Read at most one or two directly relevant files per command; if that still is not enough, return a concrete blocker instead of continuing exploration.");
        lines.Add("- After you edit files, do not concatenate broad `sed -n` or `cat` readbacks across multiple changed files just to inspect your own patch. Spot-check only the minimum lines needed to confirm the write landed, then stop.");
        lines.Add($"- When you need `.ai/*`, `AGENTS.md`, or other repo-governance assets, read them from the authoritative repo root using absolute paths under `{request.RepoRoot}` rather than relative `.ai/...` paths from the worktree.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string? ResolveAuthoritativeTaskTruthPath(WorkerExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskId))
        {
            return null;
        }

        var taskTruthPath = Path.Combine(request.RepoRoot, ".ai", "tasks", "nodes", $"{request.TaskId}.json");
        return File.Exists(taskTruthPath) ? taskTruthPath : null;
    }

    private static string? ResolveAuthoritativeCardTruthPath(WorkerExecutionRequest request, string? cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId))
        {
            return null;
        }

        var cardTruthPath = Path.Combine(request.RepoRoot, ".ai", "tasks", "cards", $"{cardId}.md");
        return File.Exists(cardTruthPath) ? cardTruthPath : null;
    }

    private static string? TryResolveCardIdFromTaskId(string? taskId)
    {
        if (string.IsNullOrWhiteSpace(taskId) || !taskId.StartsWith("T-CARD-", StringComparison.Ordinal))
        {
            return null;
        }

        var taskSuffix = taskId[2..];
        var lastDash = taskSuffix.LastIndexOf("-", StringComparison.Ordinal);
        if (lastDash <= 0 || lastDash == taskSuffix.Length - 1)
        {
            return null;
        }

        var leaf = taskSuffix[(lastDash + 1)..];
        if (!leaf.All(char.IsDigit))
        {
            return null;
        }

        return taskSuffix[..lastDash];
    }

    private static bool RequiresRepoRootControlPlaneGuidance(WorkerExecutionRequest request)
    {
        if (string.Equals(
                Path.GetFullPath(request.RepoRoot),
                Path.GetFullPath(request.WorktreeRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var repoAiRoot = Path.Combine(request.RepoRoot, ".ai");
        var worktreeAiRoot = Path.Combine(request.WorktreeRoot, ".ai");
        return Directory.Exists(repoAiRoot) && !Directory.Exists(worktreeAiRoot);
    }
}
