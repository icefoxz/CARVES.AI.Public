using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeMarkdownReadPathBudgetService
{
    private const int ColdInitializationTokenBudget = 48_000;
    private const int PostInitializationDefaultTokenBudget = 900;
    private const int GeneratedViewSingleFileBudget = 1_200;
    private const int TaskScopedMarkdownTokenBudget = 1_600;
    private const int TaskScopedMarkdownSourceBudget = 6;

    private static readonly string[] GeneratedMarkdownViewPaths =
    [
        ".ai/STATE.md",
        ".ai/CURRENT_TASK.md",
        ".ai/TASK_QUEUE.md",
    ];

    private readonly string repoRoot;
    public RuntimeMarkdownReadPathBudgetService(string repoRoot, ControlPlanePaths paths)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeMarkdownReadPathBudgetSurface Build(
        RuntimeAgentBootstrapPacketSurface bootstrap,
        RuntimeAgentTaskBootstrapOverlaySurface? overlay,
        string? requestedTaskId = null)
    {
        var policy = bootstrap.Packet.HotPathContext.MarkdownReadPolicy;
        var resolvedTaskId = ResolveTaskId(requestedTaskId, overlay, bootstrap, out var resolvedTaskSource);
        var coldItems = policy.RequiredInitialSources
            .Select(source => BuildItem(
                source,
                "cold_init_mandatory",
                "bootstrap_governance_source",
                "read_before_first_repo_work",
                "not_replaceable",
                ColdInitializationTokenBudget,
                "Required first-session initialization source."))
            .ToArray();
        var generatedItems = GeneratedMarkdownViewPaths
            .Select(path => BuildItem(
                path,
                "generated_markdown_view",
                "generated_projection",
                "defer_after_initialization",
                "carves agent context --json",
                GeneratedViewSingleFileBudget,
                "Generated Markdown view remains projection-only and is not part of the default post-init read path."))
            .ToArray();
        var taskItems = (overlay?.Overlay.MarkdownReadGuidance.TaskScopedMarkdownRefs ?? [])
            .Select(path => BuildItem(
                path,
                "task_scoped_targeted_markdown",
                "task_scoped_markdown_ref",
                "targeted_read_when_named_by_overlay",
                ResolveTaskReplacementSurface(overlay),
                TaskScopedMarkdownTokenBudget,
                "Task-scoped Markdown is a targeted read only when compact surfaces are insufficient."))
            .ToArray();
        var escalationItems = ResolveDeepGovernanceSources(policy)
            .Select(path => BuildItem(
                path,
                "deep_governance_escalation",
                "deep_governance_source",
                "read_only_on_escalation_trigger",
                "carves api runtime-agent-bootstrap-packet",
                ColdInitializationTokenBudget,
                "Deep governance source is outside the daily hot path and remains available when escalation triggers apply."))
            .ToArray();

        var coldSummary = BuildSummary(
            "cold-init-mandatory",
            "cold_init_mandatory",
            ColdInitializationTokenBudget,
            coldItems,
            coldItems,
            deferredItems: []);
        var generatedSummary = BuildSummary(
            "generated-markdown-views",
            "generated_markdown_view",
            GeneratedViewSingleFileBudget,
            defaultItems: [],
            allItems: generatedItems,
            deferredItems: generatedItems);
        var taskSummary = BuildSummary(
            "task-scoped-markdown",
            "task_scoped_targeted_markdown",
            TaskScopedMarkdownTokenBudget,
            taskItems,
            taskItems,
            deferredItems: []);
        var postInitSummary = new MarkdownReadPathBudgetSummary
        {
            BudgetId = "post-initialization-default",
            TierId = "daily_hot_path",
            MaxDefaultMarkdownTokens = PostInitializationDefaultTokenBudget,
            EstimatedDefaultMarkdownTokens = 0,
            DeferredMarkdownTokens = generatedItems.Sum(static item => item.EstimatedTokens),
            MaxDefaultMarkdownSources = 0,
            DefaultMarkdownSourceCount = 0,
            LargestItemTokens = generatedItems.Length == 0 ? 0 : generatedItems.Max(static item => item.EstimatedTokens),
            WithinBudget = true,
            ReasonCodes =
            [
                "machine_surface_first",
                "generated_markdown_views_deferred",
                "task_queue_not_default_read_path",
            ],
        };

        var gaps = BuildGaps(coldItems, taskSummary, overlay, requestedTaskId).ToArray();
        var withinBudget = postInitSummary.WithinBudget
                           && coldSummary.WithinBudget
                           && generatedSummary.WithinBudget
                           && taskSummary.WithinBudget
                           && gaps.Length == 0;
        var posture = ResolvePosture(withinBudget, gaps, generatedSummary, taskSummary);
        var allItems = coldItems
            .Concat(generatedItems)
            .Concat(taskItems)
            .Concat(escalationItems)
            .ToArray();

        return new RuntimeMarkdownReadPathBudgetSurface
        {
            RepoRoot = repoRoot,
            OverallPosture = posture,
            WithinBudget = withinBudget,
            RequestedTaskId = string.IsNullOrWhiteSpace(requestedTaskId) ? "N/A" : requestedTaskId,
            ResolvedTaskId = resolvedTaskId,
            ResolvedTaskSource = resolvedTaskSource,
            ColdInitialization = coldSummary,
            PostInitializationDefault = postInitSummary,
            GeneratedMarkdownViews = generatedSummary,
            TaskScopedMarkdown = taskSummary,
            Items = allItems,
            DefaultMachineReadPath = BuildDefaultMachineReadPath(resolvedTaskId, overlay).ToArray(),
            DeferredMarkdownSources = generatedItems
                .Where(static item => item.EstimatedTokens > 0)
                .OrderByDescending(static item => item.EstimatedTokens)
                .Select(static item => $"{item.Path} ({item.EstimatedTokens} est tokens)")
                .ToArray(),
            EscalationTriggers = policy.EscalationTriggers
                .Concat(overlay?.Overlay.MarkdownReadGuidance.EscalationTriggers ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Gaps = gaps,
            Summary = BuildSummaryLine(postInitSummary, generatedSummary, taskSummary),
            RecommendedNextAction = gaps.Length == 0
                ? "Use `carves agent context --json` as the default warm read path; open generated Markdown views or broad governance docs only when a listed escalation trigger applies."
                : "Resolve the listed gaps before treating the Markdown read-path budget as valid.",
            NonClaims =
            [
                "This surface is read-only and does not rewrite Markdown, task truth, memory, execution artifacts, or context telemetry.",
                "This surface does not remove or replace the mandatory first-session initialization report.",
                "This surface does not make generated Markdown canonical truth; `.ai/tasks/` and execution artifacts remain upstream truth.",
                "This surface estimates budget from file metadata and does not read large Markdown bodies to build the projection.",
            ],
        };
    }

    private MarkdownReadPathBudgetItem BuildItem(
        string path,
        string tierId,
        string sourceKind,
        string readAction,
        string replacementSurface,
        int singleFileBudget,
        string summary)
    {
        var resolved = ResolvePath(path);
        var exists = resolved is not null && File.Exists(resolved);
        var byteSize = exists ? new FileInfo(resolved!).Length : 0;
        var estimatedTokens = EstimateTokensFromBytes(byteSize);
        return new MarkdownReadPathBudgetItem
        {
            Path = NormalizePath(path),
            TierId = tierId,
            SourceKind = sourceKind,
            ReadAction = readAction,
            ReplacementSurface = replacementSurface,
            Exists = exists,
            ByteSize = byteSize,
            EstimatedTokens = estimatedTokens,
            OverSingleFileBudget = estimatedTokens > singleFileBudget,
            Summary = summary,
        };
    }

    private static MarkdownReadPathBudgetSummary BuildSummary(
        string budgetId,
        string tierId,
        int maxDefaultMarkdownTokens,
        IReadOnlyList<MarkdownReadPathBudgetItem> defaultItems,
        IReadOnlyList<MarkdownReadPathBudgetItem> allItems,
        IReadOnlyList<MarkdownReadPathBudgetItem> deferredItems)
    {
        var defaultTokens = defaultItems.Sum(static item => item.EstimatedTokens);
        var deferredTokens = deferredItems.Sum(static item => item.EstimatedTokens);
        var reasonCodes = new List<string>();
        if (defaultTokens > maxDefaultMarkdownTokens)
        {
            reasonCodes.Add("default_markdown_tokens_over_budget");
        }

        if (allItems.Any(static item => item.OverSingleFileBudget))
        {
            reasonCodes.Add("single_markdown_file_over_advisory");
        }

        if (allItems.Any(static item => !item.Exists && IsConcreteMarkdownPath(item.Path)))
        {
            reasonCodes.Add("markdown_source_missing");
        }

        if (deferredTokens > 0)
        {
            reasonCodes.Add("markdown_sources_deferred");
        }

        return new MarkdownReadPathBudgetSummary
        {
            BudgetId = budgetId,
            TierId = tierId,
            MaxDefaultMarkdownTokens = maxDefaultMarkdownTokens,
            EstimatedDefaultMarkdownTokens = defaultTokens,
            DeferredMarkdownTokens = deferredTokens,
            MaxDefaultMarkdownSources = tierId == "task_scoped_targeted_markdown" ? TaskScopedMarkdownSourceBudget : defaultItems.Count,
            DefaultMarkdownSourceCount = defaultItems.Count,
            LargestItemTokens = allItems.Count == 0 ? 0 : allItems.Max(static item => item.EstimatedTokens),
            WithinBudget = defaultTokens <= maxDefaultMarkdownTokens
                           && (tierId != "task_scoped_targeted_markdown" || defaultItems.Count <= TaskScopedMarkdownSourceBudget)
                           && !allItems.Any(static item => !item.Exists && IsConcreteMarkdownPath(item.Path)),
            ReasonCodes = reasonCodes.Count == 0 ? ["within_budget"] : reasonCodes.Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    private static IEnumerable<string> BuildDefaultMachineReadPath(string taskId, RuntimeAgentTaskBootstrapOverlaySurface? overlay)
    {
        yield return "carves agent context --json";
        yield return "carves api runtime-markdown-read-path-budget";
        yield return "carves api runtime-agent-bootstrap-packet";
        if (!IsNoTask(taskId))
        {
            yield return $"carves api runtime-agent-task-overlay {taskId}";
            yield return $"carves api execution-packet {taskId}";
        }
        else if (overlay is not null)
        {
            yield return $"carves api runtime-agent-task-overlay {overlay.Overlay.TaskId}";
        }
    }

    private static IEnumerable<string> BuildGaps(
        IReadOnlyList<MarkdownReadPathBudgetItem> coldItems,
        MarkdownReadPathBudgetSummary taskSummary,
        RuntimeAgentTaskBootstrapOverlaySurface? overlay,
        string? requestedTaskId)
    {
        foreach (var missing in coldItems.Where(static item => !item.Exists && IsConcreteMarkdownPath(item.Path)))
        {
            yield return $"missing_initial_markdown_source:{missing.Path}";
        }

        if (taskSummary.EstimatedDefaultMarkdownTokens > taskSummary.MaxDefaultMarkdownTokens)
        {
            yield return "task_scoped_markdown_over_budget";
        }

        if (taskSummary.DefaultMarkdownSourceCount > TaskScopedMarkdownSourceBudget)
        {
            yield return "task_scoped_markdown_source_count_over_budget";
        }

        if (!IsNoTask(requestedTaskId) && overlay is null)
        {
            yield return "requested_task_overlay_unavailable";
        }
    }

    private static string ResolvePosture(
        bool withinBudget,
        IReadOnlyList<string> gaps,
        MarkdownReadPathBudgetSummary generatedSummary,
        MarkdownReadPathBudgetSummary taskSummary)
    {
        if (gaps.Count > 0)
        {
            return "markdown_read_path_budget_blocked_by_gaps";
        }

        if (!taskSummary.WithinBudget)
        {
            return "markdown_read_path_budgeted_with_task_scope_over_advisory";
        }

        if (generatedSummary.DeferredMarkdownTokens > 0)
        {
            return "markdown_read_path_budgeted_large_views_deferred";
        }

        return withinBudget ? "markdown_read_path_budgeted" : "markdown_read_path_budget_over_advisory";
    }

    private static string BuildSummaryLine(
        MarkdownReadPathBudgetSummary postInitSummary,
        MarkdownReadPathBudgetSummary generatedSummary,
        MarkdownReadPathBudgetSummary taskSummary)
    {
        return $"Post-initialization Markdown default uses {postInitSummary.EstimatedDefaultMarkdownTokens}/{postInitSummary.MaxDefaultMarkdownTokens} estimated tokens; generated Markdown views are deferred ({generatedSummary.DeferredMarkdownTokens} estimated tokens), and task-scoped Markdown uses {taskSummary.EstimatedDefaultMarkdownTokens}/{taskSummary.MaxDefaultMarkdownTokens} estimated tokens when selected.";
    }

    private static string ResolveTaskId(
        string? requestedTaskId,
        RuntimeAgentTaskBootstrapOverlaySurface? overlay,
        RuntimeAgentBootstrapPacketSurface bootstrap,
        out string source)
    {
        if (!string.IsNullOrWhiteSpace(requestedTaskId))
        {
            source = "explicit";
            return requestedTaskId;
        }

        if (!IsNoTask(overlay?.Overlay.TaskId))
        {
            source = "task_overlay";
            return overlay!.Overlay.TaskId;
        }

        var currentTaskId = bootstrap.Packet.HotPathContext.CurrentTaskId;
        if (!IsNoTask(currentTaskId))
        {
            source = "bootstrap_current_task";
            return currentTaskId;
        }

        var activeTask = bootstrap.Packet.HotPathContext.ActiveTasks.FirstOrDefault();
        if (activeTask is not null && !IsNoTask(activeTask.TaskId))
        {
            source = "bootstrap_active_task";
            return activeTask.TaskId;
        }

        source = "none";
        return "N/A";
    }

    private static string ResolveTaskReplacementSurface(RuntimeAgentTaskBootstrapOverlaySurface? overlay)
    {
        return overlay is null
            ? "carves agent context --json"
            : $"carves api runtime-agent-task-overlay {overlay.Overlay.TaskId}";
    }

    private static string[] ResolveDeepGovernanceSources(AgentMarkdownReadPolicy policy)
    {
        return policy.ReadTiers
            .Where(static tier => string.Equals(tier.TierId, "deep_governance_escalation", StringComparison.Ordinal))
            .SelectMany(static tier => tier.Sources)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private string? ResolvePath(string path)
    {
        if (!IsConcreteMarkdownPath(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static bool IsConcreteMarkdownPath(string path)
    {
        return path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimateTokensFromBytes(long byteSize)
    {
        if (byteSize <= 0)
        {
            return 0;
        }

        return (int)Math.Min(int.MaxValue, (byteSize + 3) / 4);
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Trim('`').Replace('\\', '/');
    }

    private static bool IsNoTask(string? taskId)
    {
        return string.IsNullOrWhiteSpace(taskId) || string.Equals(taskId, "N/A", StringComparison.OrdinalIgnoreCase);
    }
}
