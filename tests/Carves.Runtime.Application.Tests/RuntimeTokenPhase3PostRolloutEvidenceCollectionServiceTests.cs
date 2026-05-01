using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenPhase3PostRolloutEvidenceCollectionServiceTests
{
    private static readonly JsonSerializerOptions RepoJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Persist_CollectsFrozenScopePostRolloutEvidence()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var resultDate = new DateOnly(2026, 4, 21);
        var task = new TaskNode
        {
            TaskId = "T-PHASE3-ROLLOUT-001",
            Title = "Observe limited main-path default",
            Description = "Replay the frozen worker wrapper path with main-path default enabled.",
            Status = DomainTaskStatus.Completed,
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/AI/"],
            Acceptance = ["candidate stays on worker system wrapper only"],
            Validation = new ValidationPlan
            {
                Commands = [["dotnet", "test", "tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj"]],
            },
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["execution_run_latest_status"] = "Completed",
            },
            LastWorkerBackend = "null_worker",
            LastWorkerRunId = "RUN-T-PHASE3-ROLLOUT-001",
            PlannerReview = new PlannerReview
            {
                Verdict = PlannerVerdict.Complete,
                Reason = "Null-worker execution completed within boundary.",
                AcceptanceMet = true,
                BoundaryPreserved = true,
                ScopeDriftDetected = false,
            },
        };
        new JsonTaskGraphRepository(paths).Save(new DomainTaskGraph([task]));
        WriteExecutionPacket(paths, task.TaskId, new ExecutionPacket
        {
            PacketId = "EP-T-PHASE3-ROLLOUT-001-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-PHASE3-ROLLOUT-001",
                TaskId = task.TaskId,
                TaskRevision = 1,
            },
            Goal = "Replay limited main-path default.",
            PlannerIntent = PlannerIntent.Execution,
            Scope = task.Scope,
            Context = new ExecutionPacketContext
            {
                ContextPackRef = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            },
            Permissions = new ExecutionPacketPermissions
            {
                EditableRoots = task.Scope,
            },
            Budgets = new ExecutionPacketBudgets
            {
                MaxFilesChanged = 4,
                MaxLinesChanged = 120,
                MaxShellCommands = 4,
            },
        });
        WriteContextPack(paths, task.TaskId, new ContextPack
        {
            PackId = "task-T-PHASE3-ROLLOUT-001",
            ArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            TaskId = task.TaskId,
            Goal = "Replay worker wrapper main-path default.",
            Task = "Compare original and limited main-path default worker instructions.",
            Constraints = ["Stay on worker:system:$.instructions only."],
            PromptInput = "Context Pack\n\nGoal:\nReplay worker wrapper main-path default.\n\nTask:\nCompare original and candidate paths.",
            Budget = new ContextPackBudget
            {
                ProfileId = "worker",
                Model = "gpt-5-codex",
                EstimatorVersion = ContextBudgetPolicyResolver.EstimatorVersion,
                ModelLimitTokens = 16000,
                TargetTokens = 900,
                AdvisoryTokens = 1000,
                HardSafetyTokens = 1300,
                MaxContextTokens = 1000,
                ReservedHeadroomTokens = 300,
                CoreBudgetTokens = 550,
                RelevantBudgetTokens = 450,
                UsedTokens = 120,
                TrimmedTokens = 0,
                FixedTokensEstimate = 120,
                DynamicTokensEstimate = 0,
                TotalContextTokensEstimate = 120,
                BudgetPosture = "balanced",
            },
        });

        var review = CreateReview(resultDate);
        var scopeFreeze = CreateScopeFreeze(resultDate);
        var workerRecollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = review.CohortId,
                RequestKinds = ["worker"],
            },
            TaskIds = [task.TaskId],
            Tasks =
            [
                new RuntimeTokenBaselineWorkerRecollectTaskRecord
                {
                    TaskId = task.TaskId,
                    RunId = "RUN-T-PHASE3-ROLLOUT-001",
                    RequestId = "worker-request-phase3-rollout-001",
                    PacketArtifactPath = $".ai/runtime/execution-packets/{task.TaskId}.json",
                    ContextPackArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
                    Consumer = "worker:codex:responses_api",
                    TokenAccountingSource = "local_estimate",
                    RecordedAtUtc = DateTimeOffset.UtcNow,
                }
            ],
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 1,
                SuccessfulAttemptedTaskCount = 1,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0,
                AttemptedTaskIds = [task.TaskId],
                Tasks =
                [
                    new RuntimeTokenBaselineAttemptedTaskRecord
                    {
                        TaskId = task.TaskId,
                        RunId = "RUN-T-PHASE3-ROLLOUT-001",
                        WorkerBackend = "null_worker",
                        TaskStatus = "Completed",
                        LatestRunStatus = "Completed",
                        Attempted = true,
                        SuccessfulAttempted = true,
                        ReviewAdmissionAccepted = true,
                        ConstraintViolationObserved = false,
                        RetryCount = 0,
                        RepairCount = 0,
                    }
                ],
            },
        };

        var config = AiProviderConfig.CreateProviderDefaults("codex", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 900, reasoningEffort: "low");
        var result = RuntimeTokenPhase3PostRolloutEvidenceCollectionService.Persist(
            paths,
            review,
            scopeFreeze,
            workerRecollect,
            config,
            "TestRepo",
            new StubGitClient(),
            new JsonTaskGraphRepository(paths),
            resultDate);

        Assert.Equal("observed_for_frozen_scope", result.EvidenceStatus);
        Assert.True(result.LimitedMainPathImplementationObserved);
        Assert.True(result.PostRolloutTokenEvidenceObserved);
        Assert.True(result.PostRolloutBehaviorEvidenceObserved);
        Assert.Equal("no_provider_agent_mediated", result.ExecutionTruthScope.ExecutionMode);
        Assert.Equal("null_worker", result.ExecutionTruthScope.WorkerBackend);
        Assert.False(result.ExecutionTruthScope.ProviderSdkExecutionRequired);
        Assert.Equal("not_claimed", result.ExecutionTruthScope.ProviderModelBehaviorClaim);
        Assert.Equal("current_runtime_mode_only", result.ExecutionTruthScope.BehavioralNonInferiorityScope);
        Assert.Equal("not_applicable", result.ExecutionTruthScope.ProviderBilledCostClaim);
        Assert.True(result.RolloutScope.DefaultEnabled);
        Assert.False(result.RolloutScope.FullRollout);
        Assert.Equal(1, result.TokenEvidence.CandidateDefaultRequestCount);
        Assert.Equal(0, result.TokenEvidence.FallbackRequestCount);
        Assert.Equal(1, result.BehaviorEvidence.AttemptedTaskCount);
        Assert.Equal(0d, result.BehaviorEvidence.TaskSuccessRateDeltaPercentagePoints);
        Assert.Empty(result.BlockingReasons);
        var sample = Assert.Single(result.Samples);
        Assert.Equal("limited_main_path_default", sample.CandidateDecisionMode);
        Assert.True(sample.CandidateDefaultApplied);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Persist_RejectsWhenMainPathReplacementReviewIsNotApproved()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var config = AiProviderConfig.CreateProviderDefaults("codex", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 900, reasoningEffort: "low");

        var review = CreateReview(resultDate) with
        {
            ReviewVerdict = "require_more_evidence",
            MainPathReplacementAllowed = false,
        };

        var error = Assert.Throws<InvalidOperationException>(() => RuntimeTokenPhase3PostRolloutEvidenceCollectionService.Persist(
            workspace.Paths,
            review,
            CreateScopeFreeze(resultDate),
            CreateWorkerRecollect(review.CohortId, resultDate),
            config,
            "TestRepo",
            new StubGitClient(),
            new JsonTaskGraphRepository(workspace.Paths),
            resultDate));

        Assert.Contains("approved limited main-path replacement review", error.Message, StringComparison.Ordinal);
    }

    private static RuntimeTokenPhase3MainPathReplacementReviewResult CreateReview(DateOnly resultDate)
    {
        return new RuntimeTokenPhase3MainPathReplacementReviewResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-main-path-replacement-review-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-3/main-path-replacement-review-2026-04-21.json",
            MainPathReplacementAllowed = true,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false,
            ReviewVerdict = "approve_limited_main_path_replacement",
            TargetSurface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            RequestKind = "worker",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable"
            },
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 1,
                SuccessfulAttemptedTaskCount = 1,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0
            },
            ReplacementScope = new RuntimeTokenPhase3MainPathReplacementScope
            {
                RequestKind = "worker",
                Surface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkMode = "not_applicable"
            },
            Controls = new RuntimeTokenPhase3MainPathReplacementControls
            {
                GlobalKillSwitchRetained = true,
                PerRequestKindFallbackRetained = true,
                PerSurfaceFallbackRetained = true,
                CandidateVersionPinned = true,
                PostRolloutAuditRequired = true,
                DefaultEnabledToday = false,
                FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion
            }
        };
    }

    private static RuntimeTokenPhase3ReplacementScopeFreezeResult CreateScopeFreeze(DateOnly resultDate)
    {
        return new RuntimeTokenPhase3ReplacementScopeFreezeResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-replacement-scope-freeze-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-3/replacement-scope-freeze-2026-04-21.json",
            MainPathReplacementReviewMarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-3-main-path-replacement-review-2026-04-21.md",
            MainPathReplacementReviewJsonArtifactPath = ".ai/runtime/token-optimization/phase-3/main-path-replacement-review-2026-04-21.json",
            TargetSurface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            RequestKind = "worker",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable"
            },
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 1,
                SuccessfulAttemptedTaskCount = 1,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0
            },
            ReplacementScope = new RuntimeTokenPhase3MainPathReplacementScope
            {
                RequestKind = "worker",
                Surface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkMode = "not_applicable"
            },
            Controls = new RuntimeTokenPhase3MainPathReplacementControls
            {
                GlobalKillSwitchRetained = true,
                PerRequestKindFallbackRetained = true,
                PerSurfaceFallbackRetained = true,
                CandidateVersionPinned = true,
                PostRolloutAuditRequired = true,
                DefaultEnabledToday = false,
                FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion
            },
            FreezeVerdict = "limited_scope_frozen",
            ImplementationScopeFrozen = true,
            LimitedMainPathImplementationAllowed = true,
            ScopeExpansionAllowed = false,
            MainRendererReplacementAllowed = false,
            RuntimeShadowExecutionAllowed = false,
            FullRolloutAllowed = false
        };
    }

    private static RuntimeTokenBaselineWorkerRecollectResult CreateWorkerRecollect(string cohortId, DateOnly resultDate)
    {
        return new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = cohortId,
                RequestKinds = ["worker"],
            },
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = 1,
                SuccessfulAttemptedTaskCount = 1,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0,
            },
        };
    }

    private static void WriteExecutionPacket(ControlPlanePaths paths, string taskId, ExecutionPacket packet)
    {
        var fullPath = Path.Combine(paths.AiRoot, "runtime", "execution-packets", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(packet, RepoJsonOptions));
    }

    private static void WriteContextPack(ControlPlanePaths paths, string taskId, ContextPack contextPack)
    {
        var fullPath = Path.Combine(paths.AiRoot, "runtime", "context-packs", "tasks", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, JsonSerializer.Serialize(contextPack, RepoJsonOptions));
    }
}
