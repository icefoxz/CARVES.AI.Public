using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentBootstrapPacketService
{
    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly TaskGraphService? taskGraphService;

    public RuntimeAgentBootstrapPacketService(string repoRoot, ControlPlanePaths paths, TaskGraphService? taskGraphService = null)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.taskGraphService = taskGraphService;
    }

    public RuntimeAgentBootstrapPacketSurface Build()
    {
        var policy = RuntimeAgentGovernanceSupport.LoadPolicy(repoRoot, paths);
        var session = RuntimeAgentGovernanceSupport.LoadSession(paths);
        var hostSnapshot = RuntimeAgentGovernanceSupport.LoadHostSnapshot(paths);
        var repoPosture = BuildRepoPosture(session);
        var currentCardMemoryRefs = ResolveCurrentCardMemoryRefs(session);
        var hotPathContext = BuildHotPathContext(policy, session, repoPosture, currentCardMemoryRefs);

        return new RuntimeAgentBootstrapPacketSurface
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            PolicyPath = RuntimeAgentGovernanceSupport.ToRepoRelative(repoRoot, paths.PlatformAgentGovernanceKernelFile),
            Packet = new AgentBootstrapPacket
            {
                PolicyVersion = policy.PolicyVersion,
                StartupMode = policy.BootstrapPacketContract.StartupMode,
                InitializationHeading = policy.InitializationContract.ReportHeading,
                SourcesHeading = policy.InitializationContract.SourcesHeading,
                EntryOrder = policy.GovernedEntryOrder,
                CurrentCardMemoryRefs = currentCardMemoryRefs,
                ReportFields = policy.InitializationContract.RequiredFields,
                SourceFields = policy.InitializationContract.SourceFields,
                RepoPosture = repoPosture,
                HostSnapshot = hostSnapshot,
                PostureBasis = hostSnapshot.State == "not_checked" ? "repository_truth" : "dual_report",
                HostRoutedActions = policy.HostRoutedActions,
                StartupInspectCommands = policy.BootstrapPacketContract.DefaultInspectCommands,
                WarmResumeInspectCommands = policy.WarmResumeContract.DefaultInspectCommands,
                OptionalDeepReadCommands = policy.BootstrapPacketContract.OptionalDeepReadCommands,
                NotYetProven = policy.NotYetProven,
                HotPathContext = hotPathContext,
            },
        };
    }

    private AgentBootstrapHotPathContext BuildHotPathContext(
        AgentGovernanceKernelPolicy policy,
        RuntimeSessionState? session,
        AgentBootstrapRepoPosture repoPosture,
        string[] currentCardMemoryRefs)
    {
        var activeTasks = BuildActiveTaskSummaries(session);
        var currentTaskId = repoPosture.CurrentTaskId;
        var startupDefaultCommands = BuildStartupDefaultCommands();
        var taskOverlayCommands = BuildTaskOverlayCommands(policy, activeTasks, currentTaskId);
        var boundedNextCommands = BuildBoundedNextCommands(activeTasks, currentTaskId, startupDefaultCommands);
        var markdownReadPolicy = BuildMarkdownReadPolicy(policy, currentCardMemoryRefs, startupDefaultCommands, taskOverlayCommands, boundedNextCommands);

        return new AgentBootstrapHotPathContext
        {
            Summary = activeTasks.Length == 0
                ? "Compact startup context is available; no active task summary was found in current posture."
                : $"Compact startup context is available with {activeTasks.Length} active task summary item(s).",
            RecommendedStartupRoute = string.Equals(currentTaskId, "N/A", StringComparison.Ordinal) && activeTasks.Length == 0
                ? "bootstrap_packet_then_governance_kernel_when_needed"
                : "bootstrap_packet_then_task_overlay",
            GovernanceBoundary = "compact_context_does_not_replace_initialization_report",
            CurrentTaskId = currentTaskId,
            DefaultInspectCommands = startupDefaultCommands,
            TaskOverlayCommands = taskOverlayCommands,
            BoundedNextCommands = boundedNextCommands,
            FullGovernanceReadTriggers =
            [
                "first meaningful repo response has not emitted CARVES.AI initialization report",
                "cleanup/commit hygiene/repository simplification/mixed diff judgment",
                "runtime/kernel/operator/unity/sibling boundary routing is in scope",
                "current task or card scope is missing, stale, or outside task overlay coverage",
                "surface posture is unknown, host snapshot is not_checked, or operator review is required",
            ],
            ActiveTasks = activeTasks,
            MarkdownReadPolicy = markdownReadPolicy,
        };
    }

    private static AgentMarkdownReadPolicy BuildMarkdownReadPolicy(
        AgentGovernanceKernelPolicy policy,
        string[] currentCardMemoryRefs,
        string[] startupDefaultCommands,
        string[] taskOverlayCommands,
        string[] boundedNextCommands)
    {
        var requiredInitialSources = ResolveInitializationReadSources(policy, currentCardMemoryRefs);
        var hotPathSurfaces = startupDefaultCommands
            .Concat(taskOverlayCommands)
            .Concat(boundedNextCommands.Where(static command => command.StartsWith("inspect ", StringComparison.Ordinal) || command.StartsWith("api ", StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var deepGovernanceSources = policy.GovernanceBoundaryDocPatterns
            .Concat(["docs/guides/AGENT_APPLIED_GOVERNANCE_TEST.md"])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var taskScopedRefs = currentCardMemoryRefs
            .Where(static item => !string.Equals(item, "N/A", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        string[] escalationTriggers =
        [
            "first meaningful repo response has not emitted CARVES.AI initialization report",
            "warm resume receipt is missing, mismatched, or unreadable",
            "cleanup/commit hygiene/repository simplification/mixed diff judgment",
            "runtime/kernel/operator/unity/sibling boundary routing is in scope",
            "task overlay or execution packet is missing, stale, or outside current scope",
            "unclassified paths or mixed roots require operator_review_first",
        ];

        return new AgentMarkdownReadPolicy
        {
            Summary = "After required initialization, bounded daily work should prefer compact Runtime surfaces and escalate to broad Markdown reads only on explicit governance triggers.",
            DefaultPostInitializationMode = "machine_surface_first",
            WarmResumeMode = "receipt_validated_machine_surface_first",
            GovernanceBoundary = "classification_reduces_read_frequency_only",
            RequiredInitialSources = requiredInitialSources,
            NeverReplacedSources =
            [
                "AGENTS.md",
                "docs/guides/AGENT_INITIALIZATION_REPORT_SPEC.md",
                ".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md",
                ".ai/memory/architecture/04_EXECUTION_RUNBOOK_CONTRACT.md",
                ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md",
            ],
            PostInitializationHotPathSurfaces = hotPathSurfaces,
            DeferredAfterInitializationSources =
            [
                ".ai/STATE.md",
                ".ai/TASK_QUEUE.md",
                ".ai/CURRENT_TASK.md",
                "broad .ai/memory/architecture/ reread",
                "broad docs/runtime/ reread",
            ],
            EscalationTriggers = escalationTriggers,
            ReadTiers =
            [
                new AgentMarkdownReadTier
                {
                    TierId = "cold_init_mandatory",
                    DefaultAction = "read_before_first_repo_work",
                    ReadWhen = "new session before the first meaningful repository analysis, implementation, review, or execution suggestion",
                    Sources = requiredInitialSources,
                    PreferredSurfaces = [],
                    Notes =
                    [
                        "This tier preserves the initialization report requirement.",
                        "Machine surfaces can summarize posture but do not replace first-session bootstrap governance.",
                    ],
                },
                new AgentMarkdownReadTier
                {
                    TierId = "warm_resume_validation",
                    DefaultAction = "compare_receipt_before_deferring_broad_reads",
                    ReadWhen = "compressed-session re-entry with a prior governed receipt path",
                    Sources = [],
                    PreferredSurfaces = policy.WarmResumeContract.DefaultInspectCommands,
                    Notes =
                    [
                        "Warm resume is eligible only when receipt comparison remains valid.",
                        "A mismatched receipt escalates back to cold initialization reads.",
                    ],
                },
                new AgentMarkdownReadTier
                {
                    TierId = "daily_hot_path",
                    DefaultAction = "prefer_machine_surfaces",
                    ReadWhen = "bounded implementation, status orientation, or task execution after initialization is already satisfied",
                    Sources = [],
                    PreferredSurfaces = hotPathSurfaces,
                    Notes =
                    [
                        "Use bootstrap packet, receipt, task overlay, and execution packet before broad Markdown rereads.",
                        "This tier lowers read frequency without changing truth ownership.",
                    ],
                },
                new AgentMarkdownReadTier
                {
                    TierId = "task_scoped_targeted_markdown",
                    DefaultAction = "read_only_refs_named_by_task_overlay_or_execution_packet",
                    ReadWhen = "current task context names specific card, memory, or scope refs",
                    Sources = taskScopedRefs,
                    PreferredSurfaces = taskOverlayCommands,
                    Notes =
                    [
                        "Task-scoped refs are targeted reads, not a request to reload every governance document.",
                    ],
                },
                new AgentMarkdownReadTier
                {
                    TierId = "deep_governance_escalation",
                    DefaultAction = "read_full_governance_sources",
                    ReadWhen = "any escalation trigger, applied governance judgment, mixed-root classification, or repo simplification decision is in scope",
                    Sources = deepGovernanceSources,
                    PreferredSurfaces = policy.BootstrapPacketContract.OptionalDeepReadCommands,
                    Notes =
                    [
                        "Deep governance reads stay mandatory for repo-wide judgment.",
                        "Unclassified paths stay operator_review_first.",
                    ],
                },
            ],
        };
    }

    private AgentBootstrapTaskSummary[] BuildActiveTaskSummaries(RuntimeSessionState? session)
    {
        var currentTaskId = session?.CurrentTaskId;
        try
        {
            var graph = taskGraphService?.Load();
            if (graph is not null)
            {
                var tasks = graph.ListTasks()
                    .Where(task => IsActiveTask(task) || string.Equals(task.TaskId, currentTaskId, StringComparison.Ordinal))
                    .OrderByDescending(task => string.Equals(task.TaskId, currentTaskId, StringComparison.Ordinal))
                    .ThenBy(task => PriorityWeight(task.Priority))
                    .ThenBy(task => task.TaskId, StringComparer.Ordinal)
                    .Take(8)
                    .Select(ToTaskSummary)
                    .ToArray();
                if (tasks.Length > 0)
                {
                    return tasks;
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        if (string.IsNullOrWhiteSpace(currentTaskId))
        {
            return [];
        }

        return
        [
            new AgentBootstrapTaskSummary
            {
                TaskId = currentTaskId,
                CardId = "CARD-UNKNOWN",
                Title = "Current task from runtime session posture",
                Status = "unknown",
                Priority = "unknown",
                TaskType = "unknown",
                Summary = "Task graph summary was unavailable; inspect task truth before execution.",
                InspectCommand = $"inspect task {currentTaskId}",
                OverlayCommand = $"inspect runtime-agent-task-overlay {currentTaskId}",
            },
        ];
    }

    private static string[] BuildTaskOverlayCommands(AgentGovernanceKernelPolicy policy, IReadOnlyList<AgentBootstrapTaskSummary> activeTasks, string currentTaskId)
    {
        var commands = activeTasks
            .Select(task => task.OverlayCommand)
            .Where(static command => !string.IsNullOrWhiteSpace(command))
            .ToList();

        if (commands.Count == 0 && !string.Equals(currentTaskId, "N/A", StringComparison.Ordinal))
        {
            commands.Add($"inspect runtime-agent-task-overlay {currentTaskId}");
        }

        if (commands.Count == 0)
        {
            commands.AddRange(policy.TaskOverlayContract.DefaultInspectCommands);
        }

        return commands.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] BuildStartupDefaultCommands()
    {
        return RuntimeSurfaceCommandRegistry.BuildInspectCommands(RuntimeSurfaceContextTier.StartupSafe)
            .Concat(RuntimeSurfaceCommandRegistry.BuildApiCommands(RuntimeSurfaceContextTier.StartupSafe))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildBoundedNextCommands(
        IReadOnlyList<AgentBootstrapTaskSummary> activeTasks,
        string currentTaskId,
        string[] startupDefaultCommands)
    {
        var commands = new List<string>();
        commands.AddRange(startupDefaultCommands);

        string[] taskIds = !string.Equals(currentTaskId, "N/A", StringComparison.Ordinal)
            ? [currentTaskId]
            : activeTasks.Select(static task => task.TaskId).ToArray();

        foreach (var taskId in taskIds.Where(static taskId => !string.IsNullOrWhiteSpace(taskId)).Distinct(StringComparer.Ordinal))
        {
            commands.Add($"inspect runtime-agent-task-overlay {taskId}");
            commands.Add($"inspect execution-packet {taskId}");
            commands.Add($"inspect task {taskId}");
        }

        foreach (var task in activeTasks.Where(static task => string.Equals(task.Status, "pending", StringComparison.Ordinal)))
        {
            commands.Add($"task run {task.TaskId}");
        }

        if (commands.Count == 0)
        {
            commands.AddRange(startupDefaultCommands);
        }

        return commands.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static AgentBootstrapTaskSummary ToTaskSummary(TaskNode task)
    {
        var summary = FirstNonEmpty(task.LastWorkerSummary, task.PlannerReview.Reason, task.Description);
        return new AgentBootstrapTaskSummary
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? "CARD-UNKNOWN",
            Title = task.Title,
            Status = ToSnakeCase(task.Status),
            Priority = task.Priority,
            TaskType = ToSnakeCase(task.TaskType),
            Summary = summary,
            InspectCommand = $"inspect task {task.TaskId}",
            OverlayCommand = $"inspect runtime-agent-task-overlay {task.TaskId}",
        };
    }

    private static bool IsActiveTask(TaskNode task)
    {
        return task.Status is DomainTaskStatus.Pending
            or DomainTaskStatus.Running
            or DomainTaskStatus.Testing
            or DomainTaskStatus.Review
            or DomainTaskStatus.ApprovalWait
            or DomainTaskStatus.Blocked
            or DomainTaskStatus.Failed;
    }

    private static int PriorityWeight(string priority)
    {
        return priority.ToUpperInvariant() switch
        {
            "P0" => 0,
            "P1" => 1,
            "P2" => 2,
            "P3" => 3,
            _ => 99,
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static AgentBootstrapRepoPosture BuildRepoPosture(RuntimeSessionState? session)
    {
        if (session is null)
        {
            return new AgentBootstrapRepoPosture();
        }

        return new AgentBootstrapRepoPosture
        {
            SessionStatus = ToSnakeCase(session.Status),
            LoopMode = ToSnakeCase(session.LoopMode),
            CurrentActionability = RuntimeActionabilitySemantics.Describe(session.CurrentActionability),
            PlannerState = ToSnakeCase(session.PlannerLifecycleState),
            CurrentTaskId = session.CurrentTaskId ?? "N/A",
        };
    }

    private static string[] ResolveCurrentCardMemoryRefs(RuntimeSessionState? session)
    {
        if (string.IsNullOrWhiteSpace(session?.CurrentTaskId))
        {
            return ["N/A"];
        }

        return
        [
            $".ai/tasks/nodes/{session.CurrentTaskId}.json",
        ];
    }

    private static string[] ResolveInitializationReadSources(AgentGovernanceKernelPolicy policy, IReadOnlyList<string> currentCardMemoryRefs)
    {
        var sources = new List<string> { "docs/guides/AGENT_INITIALIZATION_REPORT_SPEC.md" };
        foreach (var entry in policy.GovernedEntryOrder)
        {
            if (entry.StartsWith("current_card_memory=", StringComparison.Ordinal))
            {
                sources.AddRange(currentCardMemoryRefs.Where(static item => !string.Equals(item, "N/A", StringComparison.Ordinal)));
                continue;
            }

            sources.Add(MapEntryOrderToPath(entry));
        }

        return sources
            .Where(static source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string MapEntryOrderToPath(string entry)
    {
        return entry switch
        {
            "README" => "README.md",
            "AGENTS" => "AGENTS.md",
            "00_AI_ENTRY_PROTOCOL" => ".ai/memory/architecture/00_AI_ENTRY_PROTOCOL.md",
            "04_EXECUTION_RUNBOOK_CONTRACT" => ".ai/memory/architecture/04_EXECUTION_RUNBOOK_CONTRACT.md",
            "05_EXECUTION_OS_METHODOLOGY" => ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md",
            _ when entry.StartsWith(".ai/", StringComparison.Ordinal) && !entry.EndsWith(".md", StringComparison.OrdinalIgnoreCase) => $"{entry}.md",
            _ => entry,
        };
    }

    private static string ToSnakeCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }
}
