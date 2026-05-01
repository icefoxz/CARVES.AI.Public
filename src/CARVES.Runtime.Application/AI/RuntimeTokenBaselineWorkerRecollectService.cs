using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenBaselineWorkerRecollectService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions LoadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ControlPlanePaths paths;
    private readonly string repoId;
    private readonly AiProviderConfig workerProviderConfig;
    private readonly IGitClient gitClient;
    private readonly ITaskGraphRepository taskGraphRepository;
    private readonly WorkerAiRequestFactory workerAiRequestFactory;
    private readonly LlmRequestEnvelopeAttributionService attributionService;
    private readonly RuntimeSurfaceRouteGraphService routeGraphService;

    public RuntimeTokenBaselineWorkerRecollectService(
        ControlPlanePaths paths,
        string repoId,
        AiProviderConfig workerProviderConfig,
        IGitClient gitClient,
        ITaskGraphRepository taskGraphRepository,
        WorkerAiRequestFactory workerAiRequestFactory,
        LlmRequestEnvelopeAttributionService attributionService,
        RuntimeSurfaceRouteGraphService routeGraphService)
    {
        this.paths = paths;
        this.repoId = string.IsNullOrWhiteSpace(repoId) ? "local-repo" : repoId.Trim();
        this.workerProviderConfig = workerProviderConfig;
        this.gitClient = gitClient;
        this.taskGraphRepository = taskGraphRepository;
        this.workerAiRequestFactory = workerAiRequestFactory;
        this.attributionService = attributionService;
        this.routeGraphService = routeGraphService;
    }

    public RuntimeTokenBaselineWorkerRecollectResult Persist(
        IReadOnlyList<string> taskIds,
        string cohortId,
        DateOnly resultDate)
    {
        var normalizedTaskIds = taskIds
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Select(taskId => taskId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedTaskIds.Length == 0)
        {
            throw new InvalidOperationException("Worker baseline recollect requires at least one task id.");
        }

        if (string.IsNullOrWhiteSpace(cohortId))
        {
            throw new InvalidOperationException("Worker baseline recollect requires a non-empty cohort id.");
        }

        var taskGraph = taskGraphRepository.Load();
        var tasks = normalizedTaskIds
            .Select(taskId => taskGraph.Tasks.TryGetValue(taskId, out var task)
                ? task
                : throw new InvalidOperationException($"Worker baseline recollect requires task '{taskId}' to exist in task graph truth."))
            .ToArray();
        var baseCommit = gitClient.TryGetCurrentCommit(paths.RepoRoot);
        var runReportService = new ExecutionRunReportService(paths);
        var recollectedTasks = new List<RuntimeTokenBaselineWorkerRecollectTaskRecord>(tasks.Length);
        var attemptedTasks = new List<RuntimeTokenBaselineAttemptedTaskRecord>(tasks.Length);
        var directToLlmRouteEdgeCount = 0;

        foreach (var task in tasks)
        {
            if (!task.CanExecuteInWorker)
            {
                throw new InvalidOperationException($"Worker baseline recollect requires worker-executable task '{task.TaskId}'.");
            }

            var reports = runReportService.ListReports(task.TaskId);
            var packetPath = GetExecutionPacketPath(task.TaskId);
            var packet = LoadRequired<ExecutionPacket>(packetPath, $"execution packet for task '{task.TaskId}'");
            var contextPackPath = ResolveContextPackPath(task.TaskId, packet);
            var contextPack = LoadRequired<ContextPack>(contextPackPath, $"context pack for task '{task.TaskId}'");
            var selection = BuildWorkerSelection(task.TaskId);
            var request = workerAiRequestFactory.Create(
                task,
                contextPack,
                packet,
                ToRepoRelativePath(packetPath),
                WorkerExecutionProfile.UntrustedDefault,
                paths.RepoRoot,
                paths.RepoRoot,
                string.IsNullOrWhiteSpace(task.BaseCommit) ? baseCommit : task.BaseCommit!,
                dryRun: false,
                backendHint: selection.SelectedBackendId ?? workerProviderConfig.Provider,
                validationCommands: task.Validation.Commands,
                selection: selection);
            var originalDraft = request.RequestEnvelopeDraft
                                ?? throw new InvalidOperationException($"Worker baseline recollect could not build request envelope draft for task '{task.TaskId}'.");
            var recollectDraft = AugmentWithSyntheticTrimmedSegments(originalDraft, contextPack);
            var runId = ResolveRunId(task);
            var draft = recollectDraft with
            {
                RunId = runId,
                TaskId = task.TaskId,
                Model = string.IsNullOrWhiteSpace(recollectDraft.Model) ? workerProviderConfig.Model : recollectDraft.Model,
                Provider = string.IsNullOrWhiteSpace(recollectDraft.Provider) ? workerProviderConfig.Provider : recollectDraft.Provider,
                ProviderApiVersion = string.IsNullOrWhiteSpace(selection.SelectedRequestFamily)
                    ? recollectDraft.ProviderApiVersion
                    : selection.SelectedRequestFamily,
            };
            var usage = RuntimeTokenCapTruthResolver.Apply(
                new LlmRequestEnvelopeUsage
                {
                    TokenAccountingSource = "local_estimate",
                    PricingVersion = "offline_worker_recollect.v1",
                    PricingSource = "offline_worker_recollect",
                    CostEstimationVersion = ContextBudgetPolicyResolver.EstimatorVersion,
                },
                RuntimeTokenCapTruthResolver.FromMetadata(request.Metadata));
            var record = attributionService.Record(draft, usage);
            if (RecordContextPackRouting(contextPack, selection, request.RequestId, record.RecordedAtUtc))
            {
                directToLlmRouteEdgeCount += 1;
            }

            attemptedTasks.Add(BuildAttemptedTaskRecord(task, reports, runId));

            recollectedTasks.Add(new RuntimeTokenBaselineWorkerRecollectTaskRecord
            {
                TaskId = task.TaskId,
                RunId = runId,
                RequestId = request.RequestId,
                AttributionId = record.AttributionId,
                PacketArtifactPath = ToRepoRelativePath(packetPath),
                ContextPackArtifactPath = ToRepoRelativePath(contextPackPath),
                Consumer = BuildWorkerConsumer(selection),
                TokenAccountingSource = usage.TokenAccountingSource,
                RecordedAtUtc = record.RecordedAtUtc,
            });
        }

        var windowStartUtc = recollectedTasks.Min(item => item.RecordedAtUtc);
        var windowEndUtc = recollectedTasks.Max(item => item.RecordedAtUtc);
        var cohort = new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = cohortId.Trim(),
            WindowStartUtc = windowStartUtc,
            WindowEndUtc = windowEndUtc,
            RequestKinds = ["worker"],
            TokenAccountingSourcePolicy = "local_estimate_only",
            ContextWindowView = "context_window_input_tokens_total",
            BillableCostView = "billable_input_tokens_uncached",
        };
        var attemptedTaskCohort = BuildAttemptedTaskCohort(tasks.Length, attemptedTasks);
        var result = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            RecollectedAtUtc = DateTimeOffset.UtcNow,
            CohortJsonArtifactPath = ToRepoRelativePath(GetCohortJsonArtifactPath(resultDate)),
            MarkdownArtifactPath = ToRepoRelativePath(GetMarkdownArtifactPath(resultDate)),
            JsonArtifactPath = ToRepoRelativePath(GetJsonArtifactPath(resultDate)),
            Cohort = cohort,
            RequestedTaskCount = normalizedTaskIds.Length,
            RecollectedTaskCount = recollectedTasks.Count,
            AttributionRecordCount = recollectedTasks.Count,
            DirectToLlmRouteEdgeCount = directToLlmRouteEdgeCount,
            TaskIds = recollectedTasks.Select(item => item.TaskId).ToArray(),
            AttributionIds = recollectedTasks.Select(item => item.AttributionId).ToArray(),
            Tasks = recollectedTasks,
            AttemptedTaskCohort = attemptedTaskCohort,
            Notes =
            [
                "Offline worker recollect rebuilds canonical worker request envelopes from execution packet, context pack, and task graph truth.",
                "This recollect line is local_estimate_only and does not claim provider-actual usage.",
                "Attempted-task truth for this line is derived from task node plus execution run report truth on the frozen worker recollect task set.",
                $"Attempted-task cohort counts are attempted=`{attemptedTaskCohort.AttemptedTaskCount}`, successful=`{attemptedTaskCohort.SuccessfulAttemptedTaskCount}`, failed=`{attemptedTaskCohort.FailedAttemptedTaskCount}`, incomplete=`{attemptedTaskCohort.IncompleteAttemptedTaskCount}`.",
                "This recollect path does not touch live runtime request streams, runtime shadow execution, or active canary."
            ],
        };
        PersistArtifacts(cohort, result);
        return result;
    }

    private bool RecordContextPackRouting(
        ContextPack contextPack,
        WorkerSelectionDecision selection,
        string requestId,
        DateTimeOffset recordedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(contextPack.ArtifactPath))
        {
            return false;
        }

        routeGraphService.RecordSurface(
            contextPack.ArtifactPath,
            "offline_worker_recollect",
            "candidate_context_surface",
            contextPack.PromptInput);
        routeGraphService.RecordRouteEdge(new RuntimeConsumerRouteEdgeRecord
        {
            SurfaceId = contextPack.ArtifactPath,
            Consumer = BuildWorkerConsumer(selection),
            DeclaredRouteKind = "direct_to_llm",
            ObservedRouteKind = "direct_to_llm",
            ObservedCount = 1,
            SampleCount = 1,
            FrequencyWindow = "7d",
            RetrievalHitCount = 0,
            LlmReinjectionCount = 1,
            AverageFanout = 1,
            EvidenceSource = requestId,
            LastSeen = recordedAtUtc,
        });
        return true;
    }

    private WorkerSelectionDecision BuildWorkerSelection(string taskId)
    {
        return new WorkerSelectionDecision
        {
            RepoId = repoId,
            TaskId = taskId,
            Allowed = true,
            RequestedTrustProfileId = WorkerExecutionProfile.UntrustedDefault.ProfileId,
            SelectedBackendId = workerProviderConfig.Provider,
            SelectedProviderId = workerProviderConfig.Provider,
            SelectedModelId = workerProviderConfig.Model,
            SelectedRequestFamily = workerProviderConfig.RequestFamily,
            SelectedBaseUrl = workerProviderConfig.BaseUrl,
            SelectedApiKeyEnvironmentVariable = workerProviderConfig.ApiKeyEnvironmentVariable,
            SelectedProviderTimeoutSeconds = workerProviderConfig.RequestTimeoutSeconds,
            RouteSource = "offline_recollect",
            RouteReason = "replay execution packet and context pack into canonical worker request envelope",
            Summary = "offline worker envelope recollect",
            ReasonCode = "offline_worker_recollect",
            Profile = WorkerExecutionProfile.UntrustedDefault,
            SelectedBecause = ["offline_recollect"],
        };
    }

    private static string BuildWorkerConsumer(WorkerSelectionDecision selection)
    {
        return $"worker:{selection.SelectedProviderId ?? selection.SelectedBackendId ?? "unknown"}:{selection.SelectedRequestFamily ?? "unknown"}";
    }

    private static string ResolveRunId(TaskNode task)
    {
        if (!string.IsNullOrWhiteSpace(task.LastWorkerRunId))
        {
            return task.LastWorkerRunId!;
        }

        if (task.Metadata.TryGetValue("execution_run_latest_id", out var runId)
            && !string.IsNullOrWhiteSpace(runId))
        {
            return runId;
        }

        return $"recollect-run-{task.TaskId}";
    }

    private string ResolveContextPackPath(string taskId, ExecutionPacket packet)
    {
        var contextPackRef = packet.Context.ContextPackRef;
        if (!string.IsNullOrWhiteSpace(contextPackRef))
        {
            return ResolveRepoPath(contextPackRef);
        }

        return Path.Combine(paths.AiRoot, "runtime", "context-packs", "tasks", $"{taskId}.json");
    }

    private string GetExecutionPacketPath(string taskId)
    {
        return Path.Combine(paths.AiRoot, "runtime", "execution-packets", $"{taskId}.json");
    }

    private string ResolveRepoPath(string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(paths.RepoRoot, path));
    }

    private static LlmRequestEnvelopeDraft AugmentWithSyntheticTrimmedSegments(
        LlmRequestEnvelopeDraft draft,
        ContextPack contextPack)
    {
        if (contextPack.Trimmed.Count == 0)
        {
            return draft;
        }

        var nextOrder = draft.Segments.Count == 0
            ? 0
            : draft.Segments.Max(segment => segment.SegmentOrder) + 1;
        var syntheticSegments = contextPack.Trimmed
            .Select(item => new
            {
                SegmentKind = InferTrimmedSegmentKind(item.Key),
                item.EstimatedTokens,
            })
            .Where(item => item.EstimatedTokens > 0)
            .GroupBy(item => item.SegmentKind, StringComparer.Ordinal)
            .Select((group, index) => new LlmRequestEnvelopeSegmentDraft
            {
                SegmentId = $"context_pack:trimmed:{group.Key}",
                SegmentKind = group.Key,
                SegmentParentId = "context_pack",
                SegmentOrder = nextOrder + index,
                MessageIndex = 1,
                Role = "user",
                PayloadPath = $"$.input.context_pack.trimmed[{group.Key}]",
                SerializationKind = "context_pack_text",
                Content = string.Empty,
                Included = false,
                Trimmed = true,
                TrimBeforeTokensEst = group.Sum(item => item.EstimatedTokens),
                TrimAfterTokensEst = 0,
                SourceItemId = contextPack.PackId,
                RendererVersion = "offline_worker_recollect.v1",
            })
            .ToArray();

        if (syntheticSegments.Length == 0)
        {
            return draft;
        }

        return draft with
        {
            Segments = draft.Segments.Concat(syntheticSegments).ToArray(),
        };
    }

    private static string InferTrimmedSegmentKind(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "context_compaction";
        }

        if (string.Equals(key, "goal", StringComparison.Ordinal))
        {
            return "goal";
        }

        if (string.Equals(key, "task", StringComparison.Ordinal))
        {
            return "task";
        }

        if (string.Equals(key, "constraint", StringComparison.Ordinal))
        {
            return "constraints";
        }

        if (string.Equals(key, "code_hint", StringComparison.Ordinal))
        {
            return "code_hints";
        }

        if (string.Equals(key, "last_failure_summary", StringComparison.Ordinal))
        {
            return "last_failure";
        }

        if (string.Equals(key, "last_run_summary", StringComparison.Ordinal))
        {
            return "last_run";
        }

        if (key.StartsWith("recall:", StringComparison.Ordinal))
        {
            return "recall";
        }

        if (key.StartsWith("windowed_read:", StringComparison.Ordinal))
        {
            return "windowed_reads";
        }

        if (key.StartsWith("module:", StringComparison.Ordinal) || key.StartsWith("module_files:", StringComparison.Ordinal))
        {
            return "relevant_modules";
        }

        if (key.StartsWith("blocker:", StringComparison.Ordinal) || key.StartsWith("dependency:", StringComparison.Ordinal))
        {
            return "local_task_graph";
        }

        return "context_compaction";
    }

    private static RuntimeTokenBaselineAttemptedTaskRecord BuildAttemptedTaskRecord(
        TaskNode task,
        IReadOnlyList<ExecutionRunReport> reports,
        string runId)
    {
        var latestReport = reports
            .OrderByDescending(report => report.RecordedAtUtc)
            .ThenByDescending(report => report.RunId, StringComparer.Ordinal)
            .FirstOrDefault();
        var latestRunStatus = latestReport?.RunStatus.ToString()
                              ?? task.Metadata.GetValueOrDefault("execution_run_latest_status")
                              ?? string.Empty;
        var attempted = latestReport is not null
                        || !string.IsNullOrWhiteSpace(task.LastWorkerRunId)
                        || !string.IsNullOrWhiteSpace(latestRunStatus);
        var successfulAttempted = attempted
                                  && task.Status == DomainTaskStatus.Completed
                                  && string.Equals(latestRunStatus, nameof(ExecutionRunStatus.Completed), StringComparison.OrdinalIgnoreCase);
        var failedAttempted = attempted
                              && !successfulAttempted
                              && (task.Status == DomainTaskStatus.Failed
                                  || task.Status == DomainTaskStatus.Blocked
                                  || string.Equals(latestRunStatus, nameof(ExecutionRunStatus.Failed), StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(latestRunStatus, nameof(ExecutionRunStatus.Stopped), StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(latestRunStatus, nameof(ExecutionRunStatus.Abandoned), StringComparison.OrdinalIgnoreCase));
        var reviewAdmissionAccepted = successfulAttempted
                                      && task.PlannerReview.AcceptanceMet
                                      && task.PlannerReview.BoundaryPreserved
                                      && !task.PlannerReview.ScopeDriftDetected;
        var constraintViolationObserved = reports.Any(report => report.BoundaryReason.HasValue)
                                          || !task.PlannerReview.BoundaryPreserved
                                          || task.PlannerReview.ScopeDriftDetected;

        return new RuntimeTokenBaselineAttemptedTaskRecord
        {
            TaskId = task.TaskId,
            RunId = latestReport?.RunId ?? runId,
            WorkerBackend = task.LastWorkerBackend ?? string.Empty,
            TaskStatus = task.Status.ToString(),
            LatestRunStatus = latestRunStatus,
            Attempted = attempted,
            SuccessfulAttempted = successfulAttempted,
            ReviewAdmissionAccepted = reviewAdmissionAccepted,
            ConstraintViolationObserved = constraintViolationObserved,
            RetryCount = task.RetryCount,
            RepairCount = task.LastRecoveryAction == WorkerRecoveryAction.None ? 0d : 1d,
        };
    }

    private static RuntimeTokenBaselineAttemptedTaskCohort BuildAttemptedTaskCohort(
        int expectedTaskCount,
        IReadOnlyList<RuntimeTokenBaselineAttemptedTaskRecord> attemptedTasks)
    {
        var attemptedTaskIds = attemptedTasks
            .Where(item => item.Attempted)
            .Select(item => item.TaskId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var successfulAttemptedTaskCount = attemptedTasks.Count(item => item.Attempted && item.SuccessfulAttempted);
        var failedAttemptedTaskCount = attemptedTasks.Count(item => item.Attempted
            && !item.SuccessfulAttempted
            && (string.Equals(item.TaskStatus, nameof(DomainTaskStatus.Failed), StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.TaskStatus, nameof(DomainTaskStatus.Blocked), StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.LatestRunStatus, nameof(ExecutionRunStatus.Failed), StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.LatestRunStatus, nameof(ExecutionRunStatus.Stopped), StringComparison.OrdinalIgnoreCase)
                || string.Equals(item.LatestRunStatus, nameof(ExecutionRunStatus.Abandoned), StringComparison.OrdinalIgnoreCase)));
        var incompleteAttemptedTaskCount = attemptedTasks.Count(item => !item.Attempted)
                                           + attemptedTasks.Count(item => item.Attempted && !item.SuccessfulAttempted)
                                           - failedAttemptedTaskCount;

        return new RuntimeTokenBaselineAttemptedTaskCohort
        {
            SelectionMode = "frozen_worker_recollect_task_set",
            CoversFrozenReplayTaskSet = attemptedTaskIds.Length == expectedTaskCount,
            AttemptedTaskCount = attemptedTaskIds.Length,
            SuccessfulAttemptedTaskCount = successfulAttemptedTaskCount,
            FailedAttemptedTaskCount = failedAttemptedTaskCount,
            IncompleteAttemptedTaskCount = incompleteAttemptedTaskCount,
            AttemptedTaskIds = attemptedTaskIds,
            Tasks = attemptedTasks,
        };
    }

    private T LoadRequired<T>(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Worker baseline recollect requires {description} at '{path}'.");
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), LoadJsonOptions)
               ?? throw new InvalidOperationException($"Worker baseline recollect could not deserialize {description} at '{path}'.");
    }

    private void PersistArtifacts(
        RuntimeTokenBaselineCohortFreeze cohort,
        RuntimeTokenBaselineWorkerRecollectResult result)
    {
        var cohortJsonPath = Path.Combine(paths.RepoRoot, result.CohortJsonArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var markdownPath = Path.Combine(paths.RepoRoot, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        var jsonPath = Path.Combine(paths.RepoRoot, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(cohortJsonPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(cohortJsonPath, JsonSerializer.Serialize(cohort, JsonOptions));
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
    }

    private static string FormatMarkdown(RuntimeTokenBaselineWorkerRecollectResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 0A Worker Recollect Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Recollected at: `{result.RecollectedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.Cohort.CohortId}`");
        builder.AppendLine($"- Requested task count: `{result.RequestedTaskCount}`");
        builder.AppendLine($"- Recollected task count: `{result.RecollectedTaskCount}`");
        builder.AppendLine($"- Attribution record count: `{result.AttributionRecordCount}`");
        builder.AppendLine($"- Direct-to-LLM route edge count: `{result.DirectToLlmRouteEdgeCount}`");
        builder.AppendLine($"- Attempted task selection mode: `{result.AttemptedTaskCohort.SelectionMode}`");
        builder.AppendLine($"- Attempted task count: `{result.AttemptedTaskCohort.AttemptedTaskCount}`");
        builder.AppendLine($"- Successful attempted task count: `{result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`");
        builder.AppendLine($"- Failed attempted task count: `{result.AttemptedTaskCohort.FailedAttemptedTaskCount}`");
        builder.AppendLine($"- Incomplete attempted task count: `{result.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`");
        builder.AppendLine($"- Attempted cohort covers frozen replay task set: `{(result.AttemptedTaskCohort.CoversFrozenReplayTaskSet ? "yes" : "no")}`");
        builder.AppendLine($"- Cohort json artifact: `{result.CohortJsonArtifactPath}`");
        builder.AppendLine($"- Result json artifact: `{result.JsonArtifactPath}`");
        builder.AppendLine();
        builder.AppendLine("## Attempted Task Cohort");
        builder.AppendLine();
        builder.AppendLine("| Task | Run | Backend | Task Status | Latest Run Status | Attempted | Successful | Review Admission | Constraint Violation |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var task in result.AttemptedTaskCohort.Tasks)
        {
            builder.AppendLine($"| `{task.TaskId}` | `{task.RunId}` | `{task.WorkerBackend}` | `{task.TaskStatus}` | `{task.LatestRunStatus}` | `{(task.Attempted ? "yes" : "no")}` | `{(task.SuccessfulAttempted ? "yes" : "no")}` | `{(task.ReviewAdmissionAccepted ? "yes" : "no")}` | `{(task.ConstraintViolationObserved ? "yes" : "no")}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Tasks");
        builder.AppendLine();
        builder.AppendLine("| Task | Run | Request | Attribution | Token Source |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var task in result.Tasks)
        {
            builder.AppendLine($"| `{task.TaskId}` | `{task.RunId}` | `{task.RequestId}` | `{task.AttributionId}` | `{task.TokenAccountingSource}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Notes");
        builder.AppendLine();
        foreach (var note in result.Notes)
        {
            builder.AppendLine($"- {note}");
        }

        return builder.ToString();
    }

    private string GetCohortJsonArtifactPath(DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"worker-recollect-cohort-{resultDate:yyyy-MM-dd}.json");
    }

    private string GetMarkdownArtifactPath(DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-0a-worker-recollect-result-{resultDate:yyyy-MM-dd}.md");
    }

    private string GetJsonArtifactPath(DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-0a",
            $"worker-recollect-result-{resultDate:yyyy-MM-dd}.json");
    }

    private string ToRepoRelativePath(string fullPath)
    {
        return Path.GetRelativePath(paths.RepoRoot, fullPath)
            .Replace('\\', '/');
    }
}
