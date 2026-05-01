using Carves.Runtime.Domain.AI;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeAgentShortContext(string? taskId = null)
    {
        return OperatorSurfaceFormatter.RuntimeAgentShortContext(BuildRuntimeAgentShortContext(taskId));
    }

    public OperatorCommandResult ApiRuntimeAgentShortContext(string? taskId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(BuildRuntimeAgentShortContext(taskId)));
    }

    private RuntimeAgentShortContextSurface BuildRuntimeAgentShortContext(string? requestedTaskId)
    {
        var errors = new List<string>();
        var gaps = new List<string>();
        var threadStart = CreateRuntimeAgentThreadStartService().Build();
        var bootstrap = CreateRuntimeAgentBootstrapPacketService().Build();
        errors.AddRange(threadStart.Errors.Select(static error => $"runtime-agent-thread-start:{error}"));

        var resolvedTaskId = ResolveShortContextTaskId(requestedTaskId, bootstrap.Packet.HotPathContext, out var taskResolutionSource);
        RuntimeAgentTaskBootstrapOverlaySurface? overlay = null;
        if (!IsNoTask(resolvedTaskId))
        {
            try
            {
                overlay = CreateRuntimeAgentTaskBootstrapOverlayService().Build(resolvedTaskId);
            }
            catch (IOException exception)
            {
                errors.Add($"runtime-agent-task-overlay:{exception.Message}");
                gaps.Add("task_overlay_unavailable");
            }
            catch (UnauthorizedAccessException exception)
            {
                errors.Add($"runtime-agent-task-overlay:{exception.Message}");
                gaps.Add("task_overlay_unavailable");
            }
            catch (InvalidOperationException exception)
            {
                errors.Add($"runtime-agent-task-overlay:{exception.Message}");
                gaps.Add("task_overlay_unavailable");
            }
        }

        var task = overlay is null
            ? BuildNoTaskSummary(resolvedTaskId, taskResolutionSource)
            : BuildTaskSummary(overlay.Overlay);
        var contextPack = BuildContextPackSummary(resolvedTaskId);
        var markdownBudget = BuildShortContextMarkdownBudget(bootstrap, overlay, resolvedTaskId);
        var isValid = errors.Count == 0 && threadStart.IsValid;
        var hasExplicitMissingTask = !string.IsNullOrWhiteSpace(requestedTaskId) && overlay is null;
        if (hasExplicitMissingTask)
        {
            gaps.Add("requested_task_context_missing");
        }

        var ready = isValid && !hasExplicitMissingTask;
        var distinctGaps = gaps.Distinct(StringComparer.Ordinal).ToArray();
        return new RuntimeAgentShortContextSurface
        {
            RuntimeDocumentRoot = threadStart.RuntimeDocumentRoot,
            RuntimeDocumentRootMode = threadStart.RuntimeDocumentRootMode,
            RepoRoot = repoRoot,
            OverallPosture = ready
                ? (overlay is null ? "short_context_ready_without_task" : "short_context_ready")
                : "short_context_blocked",
            ShortContextReady = ready,
            RequestedTaskId = string.IsNullOrWhiteSpace(requestedTaskId) ? "N/A" : requestedTaskId,
            ResolvedTaskId = resolvedTaskId,
            TaskResolutionSource = taskResolutionSource,
            ThreadStart = new RuntimeAgentShortContextThreadStart
            {
                ThreadStartReady = threadStart.ThreadStartReady,
                NextGovernedCommand = threadStart.NextGovernedCommand,
                NextCommandSource = threadStart.NextCommandSource,
                CurrentStageId = threadStart.CurrentStageId,
                CurrentStageStatus = threadStart.CurrentStageStatus,
                OneCommandForNewThread = threadStart.OneCommandForNewThread,
                Summary = threadStart.Summary,
            },
            Bootstrap = new RuntimeAgentShortContextBootstrap
            {
                StartupMode = bootstrap.Packet.StartupMode,
                RecommendedStartupRoute = bootstrap.Packet.HotPathContext.RecommendedStartupRoute,
                CurrentTaskId = bootstrap.Packet.HotPathContext.CurrentTaskId,
                ActiveTaskCount = bootstrap.Packet.HotPathContext.ActiveTasks.Length,
                MarkdownPostInitializationMode = bootstrap.Packet.HotPathContext.MarkdownReadPolicy.DefaultPostInitializationMode,
                GovernanceBoundary = bootstrap.Packet.HotPathContext.GovernanceBoundary,
                DefaultInspectCommands = bootstrap.Packet.HotPathContext.DefaultInspectCommands,
            },
            Task = task,
            ContextPack = contextPack,
            MarkdownBudget = markdownBudget,
            InitializationReadSources = bootstrap.Packet.HotPathContext.MarkdownReadPolicy.RequiredInitialSources,
            MinimalAgentRules = threadStart.MinimalAgentRules,
            MarkdownEscalationTriggers = bootstrap.Packet.HotPathContext.MarkdownReadPolicy.EscalationTriggers
                .Concat(overlay?.Overlay.MarkdownReadGuidance.EscalationTriggers ?? [])
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            PrimaryCommands = BuildShortContextPrimaryCommands(threadStart, resolvedTaskId, contextPack, markdownBudget).ToArray(),
            DetailRefs = BuildShortContextDetailRefs(resolvedTaskId, overlay is not null).ToArray(),
            Gaps = distinctGaps,
            Summary = ready
                ? BuildShortContextSummary(resolvedTaskId, contextPack)
                : "Short context aggregate is blocked; inspect listed gaps before using it as the default low-context readback.",
            RecommendedNextAction = ready
                ? $"Use `carves agent context --json` for compact orientation, then follow next_governed_command exactly: {threadStart.NextGovernedCommand}"
                : "Resolve the listed gaps, then rerun carves agent context --json.",
            IsValid = isValid,
            Errors = errors,
            NonClaims = BuildShortContextNonClaims(),
        };
    }

    private string ResolveShortContextTaskId(
        string? requestedTaskId,
        AgentBootstrapHotPathContext hotPath,
        out string source)
    {
        if (!string.IsNullOrWhiteSpace(requestedTaskId))
        {
            source = "explicit";
            return requestedTaskId;
        }

        if (!IsNoTask(hotPath.CurrentTaskId))
        {
            source = "bootstrap_current_task";
            return hotPath.CurrentTaskId;
        }

        var activeTask = hotPath.ActiveTasks.FirstOrDefault();
        if (activeTask is not null && !string.IsNullOrWhiteSpace(activeTask.TaskId))
        {
            source = "bootstrap_active_task";
            return activeTask.TaskId;
        }

        try
        {
            var nextReady = taskGraphService.NextReadyTask();
            if (nextReady is not null)
            {
                source = "taskgraph_next_ready";
                return nextReady.TaskId;
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

        source = "none";
        return "N/A";
    }

    private static RuntimeAgentShortContextTask BuildTaskSummary(AgentTaskBootstrapOverlay overlay)
    {
        return new RuntimeAgentShortContextTask
        {
            State = "selected",
            TaskId = overlay.TaskId,
            CardId = overlay.CardId,
            Title = overlay.Title,
            Status = overlay.TaskStatus,
            AcceptanceContractStatus = overlay.AcceptanceContract.Status,
            ScopeFileCount = overlay.ScopeFiles.Length,
            EditableRoots = overlay.EditableRoots,
            ReadOnlyRoots = overlay.ReadOnlyRoots,
            TruthRoots = overlay.TruthRoots,
            SafetyLayerSummary = overlay.SafetyContext.LayerSummary,
            SafetyNonClaims = overlay.SafetyContext.NonClaims,
            RequiredVerification = overlay.RequiredVerification,
            ValidationCommands = overlay.ValidationContext.Commands,
            StopConditions = overlay.StopConditions,
            MarkdownReadMode = overlay.MarkdownReadGuidance.DefaultReadMode,
            TaskScopedMarkdownRefs = overlay.MarkdownReadGuidance.TaskScopedMarkdownRefs,
        };
    }

    private static RuntimeAgentShortContextTask BuildNoTaskSummary(string resolvedTaskId, string source)
    {
        return new RuntimeAgentShortContextTask
        {
            State = IsNoTask(resolvedTaskId) ? "not_selected" : $"unavailable:{source}",
            TaskId = resolvedTaskId,
        };
    }

    private RuntimeAgentShortContextPack BuildContextPackSummary(string taskId)
    {
        if (IsNoTask(taskId))
        {
            return new RuntimeAgentShortContextPack();
        }

        ContextPack? pack = null;
        try
        {
            pack = contextPackService.LoadForTask(taskId);
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

        if (pack is null)
        {
            return new RuntimeAgentShortContextPack
            {
                State = "not_materialized",
                Command = $"carves context estimate {taskId}",
            };
        }

        return new RuntimeAgentShortContextPack
        {
            State = "available",
            Command = $"carves context show {taskId}",
            PackId = pack.PackId,
            ArtifactPath = pack.ArtifactPath ?? "N/A",
            BudgetPosture = pack.Budget.BudgetPosture,
            UsedTokens = pack.Budget.UsedTokens,
            MaxContextTokens = pack.Budget.MaxContextTokens,
            BudgetReasonCodes = pack.Budget.BudgetViolationReasonCodes,
            TopSources = pack.Budget.TopSources.Take(8).ToArray(),
        };
    }

    private RuntimeAgentShortContextMarkdownBudget BuildShortContextMarkdownBudget(
        RuntimeAgentBootstrapPacketSurface bootstrap,
        RuntimeAgentTaskBootstrapOverlaySurface? overlay,
        string taskId)
    {
        var surface = new RuntimeMarkdownReadPathBudgetService(repoRoot, paths).Build(
            bootstrap,
            overlay,
            IsNoTask(taskId) ? null : taskId);
        return new RuntimeAgentShortContextMarkdownBudget
        {
            OverallPosture = surface.OverallPosture,
            WithinBudget = surface.WithinBudget,
            PostInitializationDefaultTokens = surface.PostInitializationDefault.EstimatedDefaultMarkdownTokens,
            PostInitializationMaxTokens = surface.PostInitializationDefault.MaxDefaultMarkdownTokens,
            DeferredMarkdownTokens = surface.PostInitializationDefault.DeferredMarkdownTokens,
            TaskScopedMarkdownTokens = surface.TaskScopedMarkdown.EstimatedDefaultMarkdownTokens,
            TaskScopedMaxTokens = surface.TaskScopedMarkdown.MaxDefaultMarkdownTokens,
            Command = IsNoTask(taskId)
                ? "carves api runtime-markdown-read-path-budget"
                : $"carves api runtime-markdown-read-path-budget {taskId}",
            ReasonCodes = surface.PostInitializationDefault.ReasonCodes
                .Concat(surface.GeneratedMarkdownViews.ReasonCodes)
                .Concat(surface.TaskScopedMarkdown.ReasonCodes)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static IEnumerable<RuntimeAgentShortContextCommandRef> BuildShortContextPrimaryCommands(
        RuntimeAgentThreadStartSurface threadStart,
        string taskId,
        RuntimeAgentShortContextPack contextPack,
        RuntimeAgentShortContextMarkdownBudget markdownBudget)
    {
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = "carves agent context --json",
            SurfaceId = "runtime-agent-short-context",
            Purpose = "single compact readback for thread, bootstrap, task overlay, and context-pack pointers",
            When = "default after required initialization or on warm reorientation",
        };
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = threadStart.NextGovernedCommand,
            SurfaceId = threadStart.NextCommandSource,
            Purpose = "next governed command selected by existing thread-start logic",
            When = "after the aggregate is valid and no listed gap applies",
        };
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = markdownBudget.Command,
            SurfaceId = markdownBudget.SurfaceId,
            Purpose = "read-only budget projection for generated Markdown views, task-scoped Markdown refs, and escalation-only governance docs",
            When = "when deciding whether to open Markdown files after initialization",
        };

        if (!IsNoTask(taskId))
        {
            yield return new RuntimeAgentShortContextCommandRef
            {
                Command = $"carves inspect runtime-agent-task-overlay {taskId}",
                SurfaceId = "runtime-agent-task-overlay",
                Purpose = "task-scoped safety, scope, verification, and Markdown read guidance",
                When = "before editing or when task-specific detail is needed",
            };
            yield return new RuntimeAgentShortContextCommandRef
            {
                Command = contextPack.Command,
                SurfaceId = "context-pack",
                Purpose = contextPack.State == "available"
                    ? "open the existing context pack"
                    : "materialize budget telemetry only when the operator needs a full task context pack",
                When = contextPack.State == "available"
                    ? "when compact summary is insufficient"
                    : "only when a full context pack is worth the extra write-path telemetry",
            };
        }
    }

    private static IEnumerable<RuntimeAgentShortContextCommandRef> BuildShortContextDetailRefs(string taskId, bool taskOverlayAvailable)
    {
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = "carves agent start --json",
            SurfaceId = "runtime-agent-thread-start",
            Purpose = "existing new-thread command and next-governed-command selector",
            When = "when the aggregate thread-start slice needs full detail",
        };
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = "carves api runtime-agent-bootstrap-packet",
            SurfaceId = "runtime-agent-bootstrap-packet",
            Purpose = "full bootstrap packet, initialization source policy, and Markdown read tiers",
            When = "when startup governance detail is needed",
        };
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = "carves api runtime-agent-bootstrap-receipt",
            SurfaceId = "runtime-agent-bootstrap-receipt",
            Purpose = "warm-resume receipt and invalidation guidance",
            When = "when resuming from a prior packet or comparing startup posture",
        };
        yield return new RuntimeAgentShortContextCommandRef
        {
            Command = "carves inspect runtime-agent-model-profile-routing",
            SurfaceId = "runtime-agent-model-profile-routing",
            Purpose = "model-profile and weak-lane routing detail",
            When = "when model capability or worker lane selection is relevant",
        };

        if (!IsNoTask(taskId))
        {
            yield return new RuntimeAgentShortContextCommandRef
            {
                Command = $"carves api runtime-agent-task-overlay {taskId}",
                SurfaceId = "runtime-agent-task-overlay",
                Purpose = taskOverlayAvailable
                    ? "full task overlay JSON"
                    : "task overlay lookup for the unresolved requested task",
                When = "when task-specific safety and scope detail is needed",
            };
            yield return new RuntimeAgentShortContextCommandRef
            {
                Command = $"carves api execution-packet {taskId}",
                SurfaceId = "execution-packet",
                Purpose = "full execution packet for governed dispatch",
                When = "before worker execution or when scope/budget detail is insufficient",
            };
        }
    }

    private static string BuildShortContextSummary(string taskId, RuntimeAgentShortContextPack contextPack)
    {
        if (IsNoTask(taskId))
        {
            return "Short context aggregate is ready without a selected task; use it for compact startup and follow the selected next governed command.";
        }

        return contextPack.State == "available"
            ? $"Short context aggregate is ready for {taskId}; existing context pack is available."
            : $"Short context aggregate is ready for {taskId}; task overlay is summarized and full context pack is not materialized by this read-only aggregate.";
    }

    private static string[] BuildShortContextNonClaims()
    {
        return
        [
            "This aggregate is read-only and does not initialize, plan, approve, execute, stage, commit, mutate task truth, or write context telemetry.",
            "This aggregate does not replace the required first-session initialization report or deep governance reads when escalation triggers apply.",
            "This aggregate does not replace canonical task graph, execution packet, bootstrap packet, receipt, or task overlay truth.",
            "This aggregate does not grant authority to edit protected truth roots or bypass planner/review/managed-workspace gates.",
        ];
    }

    private static bool IsNoTask(string? taskId)
    {
        return string.IsNullOrWhiteSpace(taskId) || string.Equals(taskId, "N/A", StringComparison.OrdinalIgnoreCase);
    }
}
