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

public sealed class RuntimeTokenPhase2ActiveCanaryResultServiceTests
{
    private static readonly JsonSerializerOptions RepoJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public void Persist_BuildsControlledCanaryResultAndMarksItInconclusiveWhenBehaviorMetricsRemainUnavailable()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var resultDate = new DateOnly(2026, 4, 21);
        var task = new TaskNode
        {
            TaskId = "T-PHASE2-CANARY-001",
            Title = "Run controlled worker canary replay",
            Description = "Replay worker request path with canary on and off.",
            Status = DomainTaskStatus.Completed,
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/AI/"],
            Acceptance =
            [
                "candidate preserves worker execution boundaries",
                "candidate references its archived evidence source",
            ],
            Validation = new ValidationPlan
            {
                Commands = [["dotnet", "test", "tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj"]],
            },
        };
        new JsonTaskGraphRepository(paths).Save(new DomainTaskGraph([task]));
        WriteExecutionPacket(paths, task.TaskId, new ExecutionPacket
        {
            PacketId = "EP-T-PHASE2-CANARY-001-v1",
            TaskRef = new ExecutionPacketTaskRef
            {
                CardId = "CARD-PHASE2-CANARY-001",
                TaskId = task.TaskId,
                TaskRevision = 1,
            },
            Goal = "Replay the worker request path with and without the wrapper canary.",
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
            StopConditions =
            [
                "stop if the candidate would weaken governance boundaries",
                "stop if token savings require lossy paraphrase",
            ],
        });
        WriteContextPack(paths, task.TaskId, new ContextPack
        {
            PackId = "task-T-PHASE2-CANARY-001",
            ArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
            TaskId = task.TaskId,
            Goal = "Replay worker wrapper canary.",
            Task = "Compare baseline and candidate worker system instructions.",
            Constraints = ["Stay on the worker system wrapper surface only."],
            PromptInput = "Context Pack\n\nGoal:\nReplay worker wrapper canary.\n\nTask:\nCompare baseline and candidate paths.",
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

        var executionApproval = new RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-execution-approval-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-execution-approval-2026-04-21.json",
            TargetSurface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ApprovalScope = "limited_explicit_allowlist",
            CanaryRequestKindAllowlist = ["worker"],
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            DefaultEnabled = false,
            RollbackPlanFrozen = true,
            NonInferiorityCohortFrozen = true,
            ReviewVerdict = "approved_for_active_canary_execution",
            ActiveCanaryApproved = true,
            CanaryExecutionAuthorized = true,
            ExpectedWholeRequestReductionP95 = 0.091,
        };
        var baselineEvidence = new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-attribution-baseline-evidence-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/attribution-baseline-evidence-result-2026-04-21.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = new RuntimeTokenBaselineCohortFreeze
                {
                    CohortId = executionApproval.CohortId,
                    RequestKinds = ["worker"],
                },
            },
            OutcomeBinding = new RuntimeTokenOutcomeBinding
            {
                TaskCostViewTrusted = true,
                ContextWindowView = new RuntimeTokenTaskCostViewSummary
                {
                    SuccessfulTaskCount = 1,
                    TokensPerSuccessfulTask = 1000,
                },
                BillableCostView = new RuntimeTokenTaskCostViewSummary
                {
                    SuccessfulTaskCount = 1,
                    TokensPerSuccessfulTask = 1000,
                },
            },
        };
        var nonInferiority = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            CohortId = executionApproval.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-non-inferiority-cohort-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/non-inferiority-cohort-2026-04-21.json",
            TargetSurface = executionApproval.TargetSurface,
            CandidateStrategy = executionApproval.CandidateStrategy,
            NonInferiorityCohortFrozen = true,
            MetricThresholds =
            [
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "total_tokens_per_successful_task",
                    ThresholdKind = "no_regression_required",
                    Comparator = "<=",
                    ThresholdValue = 0,
                    Units = "relative_change",
                },
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "internal_prompt_budget_cap_hit_rate",
                    ThresholdKind = "increase_pp_max",
                    Comparator = "<=",
                    ThresholdValue = 1,
                    Units = "percentage_points",
                }
            ],
        };
        var workerRecollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = executionApproval.CohortId,
                RequestKinds = ["worker"],
            },
            TaskIds = [task.TaskId],
            Tasks =
            [
                new RuntimeTokenBaselineWorkerRecollectTaskRecord
                {
                    TaskId = task.TaskId,
                    RunId = "RUN-T-PHASE2-CANARY-001",
                    RequestId = "worker-request-phase2-canary-001",
                    PacketArtifactPath = $".ai/runtime/execution-packets/{task.TaskId}.json",
                    ContextPackArtifactPath = $".ai/runtime/context-packs/tasks/{task.TaskId}.json",
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
                        RunId = "RUN-T-PHASE2-CANARY-001",
                        WorkerBackend = "null_worker",
                        TaskStatus = "Completed",
                        LatestRunStatus = "Completed",
                        Attempted = true,
                        SuccessfulAttempted = true,
                        ReviewAdmissionAccepted = true,
                        ConstraintViolationObserved = false,
                    },
                ],
            },
        };

        var config = AiProviderConfig.CreateProviderDefaults("codex", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 900, reasoningEffort: "low");
        var result = RuntimeTokenPhase2ActiveCanaryResultService.Persist(
            paths,
            executionApproval,
            baselineEvidence,
            nonInferiority,
            workerRecollect,
            config,
            "TestRepo",
            new StubGitClient(),
            new JsonTaskGraphRepository(paths),
            resultDate);

        Assert.Equal("inconclusive", result.Decision);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.TargetSurface, result.TargetSurface);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion, result.CandidateVersion);
        Assert.Equal("no_provider_agent_mediated", result.ExecutionTruthScope.ExecutionMode);
        Assert.Equal("null_worker", result.ExecutionTruthScope.WorkerBackend);
        Assert.False(result.ExecutionTruthScope.ProviderSdkExecutionRequired);
        Assert.Equal("not_claimed", result.ExecutionTruthScope.ProviderModelBehaviorClaim);
        Assert.Equal("current_runtime_mode_only", result.ExecutionTruthScope.BehavioralNonInferiorityScope);
        Assert.Equal("not_applicable", result.ExecutionTruthScope.ProviderBilledCostClaim);
        Assert.True(result.AttemptedTaskCohort.CoversFrozenReplayTaskSet);
        Assert.True(result.TokenMetrics.CandidateTotalTokensPerSuccessfulTask < result.TokenMetrics.BaselineTotalTokensPerSuccessfulTask);
        Assert.True(result.TokenMetrics.ObservedWholeRequestReductionP95 > 0d);
        Assert.True(result.NonInferiority.ManualReviewRequired);
        Assert.Empty(result.NonInferiority.UnavailableMetrics);
        Assert.Contains("low_base_count_requires_manual_review", result.BlockingReasons);
        Assert.Equal(0, result.Safety.HardFailCount);
        var sample = Assert.Single(result.Samples);
        Assert.True(sample.CandidateApplied);
        Assert.Equal("default_off", sample.BaselineDecisionReason);
        Assert.Equal("candidate_applied", sample.CandidateDecisionReason);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, result.JsonArtifactPath.Replace('/', Path.DirectorySeparatorChar))));
        var markdown = File.ReadAllText(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("## Execution Truth Scope", markdown, StringComparison.Ordinal);
        Assert.Contains("Execution mode: `no_provider_agent_mediated`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_PassesWhenNullWorkerCohortProvidesExecutionGradeBehaviorEvidenceAndSampleIsSufficient()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var resultDate = new DateOnly(2026, 4, 21);
        var tasks = new List<TaskNode>();
        var recollectTasks = new List<RuntimeTokenBaselineWorkerRecollectTaskRecord>();

        for (var index = 1; index <= 20; index += 1)
        {
            var taskId = $"T-PHASE2-CANARY-{index:000}";
            var runId = $"RUN-{taskId}-001";
            var task = new TaskNode
            {
                TaskId = taskId,
                Title = $"Controlled canary replay {index}",
                Description = "Replay worker request path with canary on and off.",
                Status = DomainTaskStatus.Completed,
                TaskType = TaskType.Execution,
                Scope = ["src/CARVES.Runtime.Application/AI/"],
                Acceptance = ["candidate preserves worker execution boundaries"],
                Validation = new ValidationPlan
                {
                    Commands = [["dotnet", "test", "tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj"]],
                },
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["execution_run_latest_status"] = "Completed",
                },
                LastWorkerBackend = "null_worker",
                LastWorkerRunId = runId,
                PlannerReview = new PlannerReview
                {
                    Verdict = PlannerVerdict.Complete,
                    Reason = "Null-worker execution completed within boundary.",
                    AcceptanceMet = true,
                    BoundaryPreserved = true,
                    ScopeDriftDetected = false,
                },
            };
            tasks.Add(task);

            WriteExecutionPacket(paths, taskId, new ExecutionPacket
            {
                PacketId = $"EP-{taskId}-v1",
                TaskRef = new ExecutionPacketTaskRef
                {
                    CardId = $"CARD-{taskId}",
                    TaskId = taskId,
                    TaskRevision = 1,
                },
                Goal = $"Replay worker request path for {taskId}.",
                PlannerIntent = PlannerIntent.Execution,
                Scope = task.Scope,
                Context = new ExecutionPacketContext
                {
                    ContextPackRef = $".ai/runtime/context-packs/tasks/{taskId}.json",
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
                StopConditions =
                [
                    "stop if the candidate would weaken governance boundaries",
                    "stop if token savings require lossy paraphrase",
                ],
            });
            WriteContextPack(paths, taskId, new ContextPack
            {
                PackId = $"task-{taskId}",
                ArtifactPath = $".ai/runtime/context-packs/tasks/{taskId}.json",
                TaskId = taskId,
                Goal = $"Replay worker wrapper canary for {taskId}.",
                Task = "Compare baseline and candidate worker system instructions.",
                Constraints = ["Stay on the worker system wrapper surface only."],
                PromptInput = $"Context Pack\n\nGoal:\nReplay worker wrapper canary for {taskId}.\n\nTask:\nCompare baseline and candidate paths for {taskId}.",
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
            WriteExecutionRunReport(paths, new ExecutionRunReport
            {
                RunId = runId,
                TaskId = taskId,
                Goal = $"Replay worker request path for {taskId}.",
                RunStatus = ExecutionRunStatus.Completed,
                BoundaryReason = null,
                FailureType = null,
                ReplanStrategy = null,
                ModulesTouched = [],
                StepKinds = [ExecutionStepKind.Inspect, ExecutionStepKind.Implement, ExecutionStepKind.Verify, ExecutionStepKind.Cleanup],
                FilesChanged = 0,
                CompletedSteps = 4,
                TotalSteps = 4,
                Fingerprint = $"fingerprint-{taskId}",
                RecordedAtUtc = DateTimeOffset.UtcNow.AddMinutes(index),
            });

            recollectTasks.Add(new RuntimeTokenBaselineWorkerRecollectTaskRecord
            {
                TaskId = taskId,
                RunId = runId,
                RequestId = $"worker-request-{taskId}",
                AttributionId = $"ATTR-{taskId}",
                PacketArtifactPath = $".ai/runtime/execution-packets/{taskId}.json",
                ContextPackArtifactPath = $".ai/runtime/context-packs/tasks/{taskId}.json",
                Consumer = "worker:codex:responses_api",
                TokenAccountingSource = "local_estimate",
                RecordedAtUtc = DateTimeOffset.UtcNow.AddMinutes(index),
            });
        }

        new JsonTaskGraphRepository(paths).Save(new DomainTaskGraph(tasks));

        var executionApproval = new RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-active-canary-execution-approval-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/active-canary-execution-approval-2026-04-21.json",
            TargetSurface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            CandidateStrategy = "dedupe_then_request_kind_slice",
            ApprovalScope = "limited_explicit_allowlist",
            CanaryRequestKindAllowlist = ["worker"],
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            DefaultEnabled = false,
            RollbackPlanFrozen = true,
            NonInferiorityCohortFrozen = true,
            ReviewVerdict = "approved_for_active_canary_execution",
            ActiveCanaryApproved = true,
            CanaryExecutionAuthorized = true,
            ExpectedWholeRequestReductionP95 = 0.091,
        };
        var baselineEvidence = new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-attribution-baseline-evidence-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/attribution-baseline-evidence-result-2026-04-21.json",
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = new RuntimeTokenBaselineCohortFreeze
                {
                    CohortId = executionApproval.CohortId,
                    RequestKinds = ["worker"],
                },
            },
            OutcomeBinding = new RuntimeTokenOutcomeBinding
            {
                TaskCostViewTrusted = true,
                ContextWindowView = new RuntimeTokenTaskCostViewSummary
                {
                    SuccessfulTaskCount = 1,
                    TokensPerSuccessfulTask = 1000,
                },
                BillableCostView = new RuntimeTokenTaskCostViewSummary
                {
                    SuccessfulTaskCount = 1,
                    TokensPerSuccessfulTask = 1000,
                },
            },
        };
        var nonInferiority = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            CohortId = executionApproval.CohortId,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-2-non-inferiority-cohort-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-2/non-inferiority-cohort-2026-04-21.json",
            TargetSurface = executionApproval.TargetSurface,
            CandidateStrategy = executionApproval.CandidateStrategy,
            NonInferiorityCohortFrozen = true,
            MetricThresholds =
            [
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "task_success_rate",
                    ThresholdKind = "regression_pp_max",
                    Comparator = "<=",
                    ThresholdValue = 2,
                    Units = "percentage_points",
                },
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "review_admission_rate",
                    ThresholdKind = "regression_pp_max",
                    Comparator = "<=",
                    ThresholdValue = 2,
                    Units = "percentage_points",
                },
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "constraint_violation_rate",
                    ThresholdKind = "increase_pp_max",
                    Comparator = "<=",
                    ThresholdValue = 1,
                    Units = "percentage_points",
                },
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "retry_count_per_task",
                    ThresholdKind = "relative_increase_max",
                    Comparator = "<=",
                    ThresholdValue = 0.2,
                    Units = "ratio",
                },
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "repair_count_per_task",
                    ThresholdKind = "relative_increase_max",
                    Comparator = "<=",
                    ThresholdValue = 0.2,
                    Units = "ratio",
                },
                new RuntimeTokenPhase2NonInferiorityThreshold
                {
                    MetricId = "total_tokens_per_successful_task",
                    ThresholdKind = "no_regression_required",
                    Comparator = "<=",
                    ThresholdValue = 0,
                    Units = "relative_change",
                }
            ],
        };
        var workerRecollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            MarkdownArtifactPath = "docs/runtime/runtime-token-optimization-phase-0a-worker-recollect-result-2026-04-21.md",
            JsonArtifactPath = ".ai/runtime/token-optimization/phase-0a/worker-recollect-result-2026-04-21.json",
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = executionApproval.CohortId,
                RequestKinds = ["worker"],
            },
            TaskIds = recollectTasks.Select(item => item.TaskId).ToArray(),
            Tasks = recollectTasks,
            AttemptedTaskCohort = new RuntimeTokenBaselineAttemptedTaskCohort
            {
                SelectionMode = "frozen_worker_recollect_task_set",
                CoversFrozenReplayTaskSet = true,
                AttemptedTaskCount = recollectTasks.Count,
                SuccessfulAttemptedTaskCount = recollectTasks.Count,
                FailedAttemptedTaskCount = 0,
                IncompleteAttemptedTaskCount = 0,
                AttemptedTaskIds = recollectTasks.Select(item => item.TaskId).ToArray(),
                Tasks = recollectTasks.Select(item => new RuntimeTokenBaselineAttemptedTaskRecord
                {
                    TaskId = item.TaskId,
                    RunId = item.RunId,
                    WorkerBackend = "null_worker",
                    TaskStatus = "Completed",
                    LatestRunStatus = "Completed",
                    Attempted = true,
                    SuccessfulAttempted = true,
                    ReviewAdmissionAccepted = true,
                    ConstraintViolationObserved = false,
                }).ToArray(),
            },
        };

        var config = AiProviderConfig.CreateProviderDefaults("codex", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 900, reasoningEffort: "low");
        var result = RuntimeTokenPhase2ActiveCanaryResultService.Persist(
            paths,
            executionApproval,
            baselineEvidence,
            nonInferiority,
            workerRecollect,
            config,
            "TestRepo",
            new StubGitClient(),
            new JsonTaskGraphRepository(paths),
            resultDate);

        Assert.Equal("pass", result.Decision);
        Assert.Equal("controlled_worker_request_path_replay_with_null_worker_attempted_task_truth", result.ObservationMode);
        Assert.Equal("no_provider_agent_mediated", result.ExecutionTruthScope.ExecutionMode);
        Assert.Equal("null_worker", result.ExecutionTruthScope.WorkerBackend);
        Assert.False(result.ExecutionTruthScope.ProviderSdkExecutionRequired);
        Assert.Equal("not_claimed", result.ExecutionTruthScope.ProviderModelBehaviorClaim);
        Assert.Equal("current_runtime_mode_only", result.ExecutionTruthScope.BehavioralNonInferiorityScope);
        Assert.Equal("not_applicable", result.ExecutionTruthScope.ProviderBilledCostClaim);
        Assert.Empty(result.NonInferiority.UnavailableMetrics);
        Assert.True(result.NonInferiority.SampleSizeSufficient);
        Assert.False(result.NonInferiority.ManualReviewRequired);
        Assert.True(result.NonInferiority.Passed);
        Assert.Equal(0d, result.NonInferiority.TaskSuccessRateDeltaPercentagePoints);
        Assert.Equal(0d, result.NonInferiority.ReviewAdmissionRateDeltaPercentagePoints);
        Assert.Equal(0d, result.NonInferiority.ConstraintViolationRateDeltaPercentagePoints);
        Assert.Equal(0d, result.NonInferiority.RetryCountPerTaskRelativeDelta);
        Assert.Equal(0d, result.NonInferiority.RepairCountPerTaskRelativeDelta);
        Assert.Equal(20, result.AttemptedTaskCohort.AttemptedTaskCount);
        Assert.Equal(20, result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount);
        Assert.Equal(result.Samples.Sum(item => item.BaselineWholeRequestTokens) / 20d, result.TokenMetrics.BaselineTotalTokensPerSuccessfulTask, 3);
        Assert.Equal(result.Samples.Sum(item => item.CandidateWholeRequestTokens) / 20d, result.TokenMetrics.CandidateTotalTokensPerSuccessfulTask, 3);
        Assert.True(result.TokenMetrics.CandidateTotalTokensPerSuccessfulTask < result.TokenMetrics.BaselineTotalTokensPerSuccessfulTask);
        var markdown = File.ReadAllText(Path.Combine(workspace.RootPath, result.MarkdownArtifactPath.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains("## Execution Truth Scope", markdown, StringComparison.Ordinal);
        Assert.Contains("Provider model behavior claim: `not_claimed`", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_RejectsWhenExecutionApprovalIsNotGranted()
    {
        using var workspace = new TemporaryWorkspace();
        var resultDate = new DateOnly(2026, 4, 21);
        var executionApproval = new RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult
        {
            ResultDate = resultDate,
            CohortId = "phase_0a_worker_recollect_2026_04_21_runtime",
            TargetSurface = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            CandidateStrategy = "dedupe_then_request_kind_slice",
            CandidateVersion = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
            FallbackVersion = RuntimeTokenWorkerWrapperCanaryService.FallbackVersion,
            ActiveCanaryApproved = false,
            CanaryExecutionAuthorized = false,
        };
        var baselineEvidence = new RuntimeTokenBaselineEvidenceResult
        {
            ResultDate = resultDate,
            Aggregation = new RuntimeTokenBaselineAggregation
            {
                Cohort = new RuntimeTokenBaselineCohortFreeze
                {
                    CohortId = executionApproval.CohortId,
                    RequestKinds = ["worker"],
                },
            },
            OutcomeBinding = new RuntimeTokenOutcomeBinding
            {
                TaskCostViewTrusted = true,
                ContextWindowView = new RuntimeTokenTaskCostViewSummary
                {
                    SuccessfulTaskCount = 1,
                    TokensPerSuccessfulTask = 1000,
                },
            },
        };
        var nonInferiority = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            CohortId = executionApproval.CohortId,
            NonInferiorityCohortFrozen = true,
        };
        var workerRecollect = new RuntimeTokenBaselineWorkerRecollectResult
        {
            ResultDate = resultDate,
            Cohort = new RuntimeTokenBaselineCohortFreeze
            {
                CohortId = executionApproval.CohortId,
                RequestKinds = ["worker"],
            },
        };
        var config = AiProviderConfig.CreateProviderDefaults("codex", enabled: true, allowFallbackToNull: false, requestTimeoutSeconds: 30, maxOutputTokens: 900, reasoningEffort: "low");

        var error = Assert.Throws<InvalidOperationException>(() => RuntimeTokenPhase2ActiveCanaryResultService.Persist(
            workspace.Paths,
            executionApproval,
            baselineEvidence,
            nonInferiority,
            workerRecollect,
            config,
            "TestRepo",
            new StubGitClient(),
            new JsonTaskGraphRepository(workspace.Paths),
            resultDate));

        Assert.Contains("approved active canary execution line", error.Message, StringComparison.Ordinal);
    }

    private static void WriteExecutionPacket(ControlPlanePaths paths, string taskId, ExecutionPacket packet)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "execution-packets", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(packet, RepoJsonOptions));
    }

    private static void WriteContextPack(ControlPlanePaths paths, string taskId, ContextPack pack)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "context-packs", "tasks", $"{taskId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(pack, RepoJsonOptions));
    }

    private static void WriteExecutionRunReport(ControlPlanePaths paths, ExecutionRunReport report)
    {
        var path = Path.Combine(paths.AiRoot, "runtime", "run-reports", report.TaskId, $"{report.RunId}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, RepoJsonOptions));
    }
}
