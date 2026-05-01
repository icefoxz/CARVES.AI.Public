using System.Globalization;
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

public sealed class RuntimeTokenPhase2ActiveCanaryResultService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
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

    public RuntimeTokenPhase2ActiveCanaryResultService(
        ControlPlanePaths paths,
        string repoId,
        AiProviderConfig workerProviderConfig,
        IGitClient gitClient,
        ITaskGraphRepository taskGraphRepository)
    {
        this.paths = paths;
        this.repoId = string.IsNullOrWhiteSpace(repoId) ? "local-repo" : repoId.Trim();
        this.workerProviderConfig = workerProviderConfig;
        this.gitClient = gitClient;
        this.taskGraphRepository = taskGraphRepository;
    }

    public RuntimeTokenPhase2ActiveCanaryResult Persist(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenBaselineEvidenceResult baselineEvidenceResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult)
    {
        return Persist(
            paths,
            executionApprovalResult,
            baselineEvidenceResult,
            nonInferiorityCohortFreezeResult,
            workerRecollectResult,
            workerProviderConfig,
            repoId,
            gitClient,
            taskGraphRepository,
            executionApprovalResult.ResultDate);
    }

    internal static RuntimeTokenPhase2ActiveCanaryResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenBaselineEvidenceResult baselineEvidenceResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        AiProviderConfig workerProviderConfig,
        string repoId,
        IGitClient gitClient,
        ITaskGraphRepository taskGraphRepository,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(
            executionApprovalResult,
            baselineEvidenceResult,
            nonInferiorityCohortFreezeResult,
            workerRecollectResult,
            resultDate);

        var taskGraph = taskGraphRepository.Load();
        var baseCommit = gitClient.TryGetCurrentCommit(paths.RepoRoot);
        var baselineFactory = CreateWorkerFactory(workerProviderConfig, enabled: false);
        var candidateFactory = CreateWorkerFactory(workerProviderConfig, enabled: true);
        var samples = new List<RuntimeTokenPhase2ActiveCanarySample>(workerRecollectResult.Tasks.Count);
        var baselineWholeTokens = new List<double>(workerRecollectResult.Tasks.Count);
        var candidateWholeTokens = new List<double>(workerRecollectResult.Tasks.Count);
        var baselineInstructionTokens = new List<double>(workerRecollectResult.Tasks.Count);
        var candidateInstructionTokens = new List<double>(workerRecollectResult.Tasks.Count);
        var baselineProviderCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var candidateProviderCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var baselineInternalCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var candidateInternalCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var baselineSectionCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var candidateSectionCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var baselineTrimLoopCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var candidateTrimLoopCapHits = new List<bool>(workerRecollectResult.Tasks.Count);
        var hardFailConditionsTriggered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var record in workerRecollectResult.Tasks)
        {
            if (!taskGraph.Tasks.TryGetValue(record.TaskId, out var task))
            {
                throw new InvalidOperationException($"Phase 2 active canary result requires task '{record.TaskId}' from task graph truth.");
            }

            var packet = LoadRequired<ExecutionPacket>(paths, record.PacketArtifactPath, $"execution packet for task '{record.TaskId}'");
            var contextPack = LoadRequired<ContextPack>(paths, record.ContextPackArtifactPath, $"context pack for task '{record.TaskId}'");
            var selection = BuildWorkerSelection(repoId, workerProviderConfig, task.TaskId);
            var effectiveBaseCommit = string.IsNullOrWhiteSpace(task.BaseCommit) ? baseCommit : task.BaseCommit!;

            var baselineRequest = baselineFactory.Create(
                task,
                contextPack,
                packet,
                record.PacketArtifactPath,
                WorkerExecutionProfile.UntrustedDefault,
                paths.RepoRoot,
                paths.RepoRoot,
                effectiveBaseCommit,
                dryRun: false,
                backendHint: selection.SelectedBackendId ?? workerProviderConfig.Provider,
                validationCommands: task.Validation.Commands,
                selection: selection);
            var candidateRequest = candidateFactory.Create(
                task,
                contextPack,
                packet,
                record.PacketArtifactPath,
                WorkerExecutionProfile.UntrustedDefault,
                paths.RepoRoot,
                paths.RepoRoot,
                effectiveBaseCommit,
                dryRun: false,
                backendHint: selection.SelectedBackendId ?? workerProviderConfig.Provider,
                validationCommands: task.Validation.Commands,
                selection: selection);

            var baselineRequestTokens = EstimateWholeRequestTokens(
                baselineRequest.RequestEnvelopeDraft
                ?? throw new InvalidOperationException($"Phase 2 active canary result requires baseline request envelope draft for task '{task.TaskId}'."));
            var candidateRequestTokens = EstimateWholeRequestTokens(
                candidateRequest.RequestEnvelopeDraft
                ?? throw new InvalidOperationException($"Phase 2 active canary result requires candidate request envelope draft for task '{task.TaskId}'."));
            var baselineInstructionsTokens = ContextBudgetPolicyResolver.EstimateTokens(RuntimeTelemetryHashing.Normalize(baselineRequest.Instructions));
            var candidateInstructionsTokens = ContextBudgetPolicyResolver.EstimateTokens(RuntimeTelemetryHashing.Normalize(candidateRequest.Instructions));

            baselineWholeTokens.Add(baselineRequestTokens);
            candidateWholeTokens.Add(candidateRequestTokens);
            baselineInstructionTokens.Add(baselineInstructionsTokens);
            candidateInstructionTokens.Add(candidateInstructionsTokens);

            var baselineCapTruth = RuntimeTokenCapTruthResolver.FromMetadata(baselineRequest.Metadata);
            var candidateCapTruth = RuntimeTokenCapTruthResolver.FromMetadata(candidateRequest.Metadata);
            baselineProviderCapHits.Add(baselineCapTruth?.ProviderContextCapHit ?? false);
            candidateProviderCapHits.Add(candidateCapTruth?.ProviderContextCapHit ?? false);
            baselineInternalCapHits.Add(baselineCapTruth?.InternalPromptBudgetCapHit ?? false);
            candidateInternalCapHits.Add(candidateCapTruth?.InternalPromptBudgetCapHit ?? false);
            baselineSectionCapHits.Add(baselineCapTruth?.SectionBudgetCapHit ?? false);
            candidateSectionCapHits.Add(candidateCapTruth?.SectionBudgetCapHit ?? false);
            baselineTrimLoopCapHits.Add(baselineCapTruth?.TrimLoopCapHit ?? false);
            candidateTrimLoopCapHits.Add(candidateCapTruth?.TrimLoopCapHit ?? false);

            var candidateApplied = string.Equals(
                candidateRequest.Metadata.GetValueOrDefault("worker_wrapper_canary_candidate_applied"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var candidateDecisionReason = candidateRequest.Metadata.GetValueOrDefault("worker_wrapper_canary_decision_reason") ?? string.Empty;
            if (!candidateApplied || !string.Equals(candidateDecisionReason, "candidate_applied", StringComparison.Ordinal))
            {
                hardFailConditionsTriggered.Add("hard_fail_count_gt_0");
            }

            samples.Add(new RuntimeTokenPhase2ActiveCanarySample
            {
                TaskId = record.TaskId,
                RunId = record.RunId,
                BaselineRequestId = baselineRequest.RequestId,
                CandidateRequestId = candidateRequest.RequestId,
                BaselineDecisionReason = baselineRequest.Metadata.GetValueOrDefault("worker_wrapper_canary_decision_reason") ?? string.Empty,
                CandidateDecisionReason = candidateDecisionReason,
                CandidateApplied = candidateApplied,
                BaselineInstructionsTokens = baselineInstructionsTokens,
                CandidateInstructionsTokens = candidateInstructionsTokens,
                BaselineWholeRequestTokens = baselineRequestTokens,
                CandidateWholeRequestTokens = candidateRequestTokens,
                TargetSurfaceReductionRatio = ComputeReductionRatio(baselineInstructionsTokens, candidateInstructionsTokens),
                WholeRequestReductionRatio = ComputeReductionRatio(baselineRequestTokens, candidateRequestTokens),
            });
        }

        var behaviorEvidence = DeriveBehaviorEvidence(workerRecollectResult.AttemptedTaskCohort, workerRecollectResult.Tasks.Count);
        var successfulTaskCount = behaviorEvidence.SuccessfulTaskCount
                                  ?? baselineEvidenceResult.OutcomeBinding.ContextWindowView.SuccessfulTaskCount;
        if (successfulTaskCount <= 0)
        {
            throw new InvalidOperationException("Phase 2 active canary result requires a trusted successful-task denominator.");
        }

        var baselineWholeRequestP95 = Percentile(baselineWholeTokens, 0.95);
        var candidateWholeRequestP95 = Percentile(candidateWholeTokens, 0.95);
        var baselineBillableP95 = baselineWholeRequestP95;
        var candidateBillableP95 = candidateWholeRequestP95;
        var baselineTokensPerSuccessfulTask = baselineWholeTokens.Sum() / successfulTaskCount;
        var candidateTokensPerSuccessfulTask = candidateWholeTokens.Sum() / successfulTaskCount;
        var deltaTokensPerSuccessfulTask = candidateTokensPerSuccessfulTask - baselineTokensPerSuccessfulTask;
        var relativeChangeTokensPerSuccessfulTask = baselineTokensPerSuccessfulTask <= 0d
            ? 0d
            : deltaTokensPerSuccessfulTask / baselineTokensPerSuccessfulTask;
        var observedWholeRequestReductionP95 = ComputeReductionRatio(baselineWholeRequestP95, candidateWholeRequestP95);
        var targetSurfaceReductionRatioP95 = Percentile(samples.Select(item => item.TargetSurfaceReductionRatio).ToArray(), 0.95);
        var targetSurfaceShareP95 = Percentile(
            samples.Select(item => item.BaselineWholeRequestTokens <= 0
                ? 0d
                : item.BaselineInstructionsTokens / (double)item.BaselineWholeRequestTokens).ToArray(),
            0.95);

        var lowBaseManualReviewRequired = behaviorEvidence.CohortTaskCount < 20;
        var unavailableMetrics = behaviorEvidence.UnavailableMetrics;
        var thresholdEvaluations = BuildThresholdEvaluations(
            nonInferiorityCohortFreezeResult.MetricThresholds,
            baselineTokensPerSuccessfulTask,
            candidateTokensPerSuccessfulTask,
            relativeChangeTokensPerSuccessfulTask,
            ComputePercentagePointDelta(baselineProviderCapHits, candidateProviderCapHits),
            ComputePercentagePointDelta(baselineInternalCapHits, candidateInternalCapHits),
            ComputePercentagePointDelta(baselineSectionCapHits, candidateSectionCapHits),
            ComputePercentagePointDelta(baselineTrimLoopCapHits, candidateTrimLoopCapHits),
            lowBaseManualReviewRequired,
            behaviorEvidence);

        if (samples.Any(sample => sample.CandidateApplied is false))
        {
            hardFailConditionsTriggered.Add("policy_invariant_coverage_below_100pct");
        }

        var hardFailCount = hardFailConditionsTriggered.Count;
        var thresholdFailures = thresholdEvaluations.Count(item => item.Evaluated && !item.Passed);
        var behaviorMetricsUnavailable = unavailableMetrics.Count > 0;
        var manualReviewRequired = lowBaseManualReviewRequired || behaviorMetricsUnavailable;
        var safety = new RuntimeTokenPhase2ActiveCanarySafetyResult
        {
            HardFailCount = hardFailCount,
            RollbackTriggered = hardFailCount > 0,
            ManualReviewRequired = manualReviewRequired,
            HardFailConditionsTriggered = hardFailConditionsTriggered.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
        };
        var nonInferiority = new RuntimeTokenPhase2ActiveCanaryNonInferiorityResult
        {
            TaskSuccessRateDeltaPercentagePoints = behaviorEvidence.TaskSuccessRateDeltaPercentagePoints,
            ReviewAdmissionRateDeltaPercentagePoints = behaviorEvidence.ReviewAdmissionRateDeltaPercentagePoints,
            ConstraintViolationRateDeltaPercentagePoints = behaviorEvidence.ConstraintViolationRateDeltaPercentagePoints,
            RetryCountPerTaskRelativeDelta = behaviorEvidence.RetryCountPerTaskRelativeDelta,
            RepairCountPerTaskRelativeDelta = behaviorEvidence.RepairCountPerTaskRelativeDelta,
            ProviderContextCapHitRateDeltaPercentagePoints = ComputePercentagePointDelta(baselineProviderCapHits, candidateProviderCapHits),
            InternalPromptBudgetCapHitRateDeltaPercentagePoints = ComputePercentagePointDelta(baselineInternalCapHits, candidateInternalCapHits),
            SectionBudgetCapHitRateDeltaPercentagePoints = ComputePercentagePointDelta(baselineSectionCapHits, candidateSectionCapHits),
            TrimLoopCapHitRateDeltaPercentagePoints = ComputePercentagePointDelta(baselineTrimLoopCapHits, candidateTrimLoopCapHits),
            SampleSizeSufficient = !lowBaseManualReviewRequired,
            ManualReviewRequired = manualReviewRequired,
            Passed = hardFailCount == 0 && thresholdFailures == 0 && !manualReviewRequired,
            UnavailableMetrics = unavailableMetrics,
            ThresholdEvaluations = thresholdEvaluations,
        };
        var tokenMetrics = new RuntimeTokenPhase2ActiveCanaryTokenMetrics
        {
            BaselineRequestCount = baselineWholeTokens.Count,
            CandidateRequestCount = candidateWholeTokens.Count,
            TargetSurfaceReductionRatioP95 = targetSurfaceReductionRatioP95,
            TargetSurfaceShareP95 = targetSurfaceShareP95,
            ExpectedWholeRequestReductionP95 = executionApprovalResult.ExpectedWholeRequestReductionP95,
            ObservedWholeRequestReductionP95 = observedWholeRequestReductionP95,
            BaselineTotalTokensPerSuccessfulTask = baselineTokensPerSuccessfulTask,
            CandidateTotalTokensPerSuccessfulTask = candidateTokensPerSuccessfulTask,
            DeltaTotalTokensPerSuccessfulTask = deltaTokensPerSuccessfulTask,
            RelativeChangeTotalTokensPerSuccessfulTask = relativeChangeTokensPerSuccessfulTask,
            BaselineContextWindowInputTokensP95 = baselineWholeRequestP95,
            CandidateContextWindowInputTokensP95 = candidateWholeRequestP95,
            DeltaContextWindowInputTokensP95 = candidateWholeRequestP95 - baselineWholeRequestP95,
            BaselineBillableInputTokensUncachedP95 = baselineBillableP95,
            CandidateBillableInputTokensUncachedP95 = candidateBillableP95,
            DeltaBillableInputTokensUncachedP95 = candidateBillableP95 - baselineBillableP95,
        };

        var blockingReasons = new List<string>();
        if (hardFailCount > 0)
        {
            blockingReasons.Add("hard_fail_conditions_triggered");
        }

        if (thresholdFailures > 0)
        {
            blockingReasons.Add("non_inferiority_threshold_failed");
        }

        if (behaviorMetricsUnavailable)
        {
            blockingReasons.Add("behavioral_non_inferiority_metrics_not_observed");
        }

        if (lowBaseManualReviewRequired)
        {
            blockingReasons.Add("low_base_count_requires_manual_review");
        }

        var decision = hardFailCount > 0 || thresholdFailures > 0
            ? "fail"
            : manualReviewRequired
                ? "inconclusive"
                : "pass";

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2ActiveCanaryResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = executionApprovalResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            ExecutionApprovalMarkdownArtifactPath = executionApprovalResult.MarkdownArtifactPath,
            ExecutionApprovalJsonArtifactPath = executionApprovalResult.JsonArtifactPath,
            BaselineEvidenceMarkdownArtifactPath = baselineEvidenceResult.MarkdownArtifactPath,
            BaselineEvidenceJsonArtifactPath = baselineEvidenceResult.JsonArtifactPath,
            NonInferiorityCohortMarkdownArtifactPath = nonInferiorityCohortFreezeResult.MarkdownArtifactPath,
            NonInferiorityCohortJsonArtifactPath = nonInferiorityCohortFreezeResult.JsonArtifactPath,
            WorkerRecollectMarkdownArtifactPath = workerRecollectResult.MarkdownArtifactPath,
            WorkerRecollectJsonArtifactPath = workerRecollectResult.JsonArtifactPath,
            TargetSurface = executionApprovalResult.TargetSurface,
            CandidateStrategy = executionApprovalResult.CandidateStrategy,
            CandidateVersion = executionApprovalResult.CandidateVersion,
            FallbackVersion = executionApprovalResult.FallbackVersion,
            ObservationMode = behaviorEvidence.ObservationMode,
            CanaryScope = new RuntimeTokenPhase2ActiveCanaryScope
            {
                RequestKinds = executionApprovalResult.CanaryRequestKindAllowlist,
                SurfaceAllowlist = [executionApprovalResult.TargetSurface],
                DefaultEnabled = executionApprovalResult.DefaultEnabled,
                AllowlistMode = "explicit",
            },
            ExecutionTruthScope = new RuntimeTokenPhase2ExecutionTruthScope
            {
                ExecutionMode = "no_provider_agent_mediated",
                WorkerBackend = "null_worker",
                ProviderSdkExecutionRequired = false,
                ProviderModelBehaviorClaim = "not_claimed",
                BehavioralNonInferiorityScope = "current_runtime_mode_only",
                ProviderBilledCostClaim = "not_applicable",
            },
            AttemptedTaskCohort = workerRecollectResult.AttemptedTaskCohort,
            TokenMetrics = tokenMetrics,
            NonInferiority = nonInferiority,
            Safety = safety,
            Decision = decision,
            Samples = samples,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextRequiredActions(decision, manualReviewRequired),
            Notes = BuildNotes(
                decision,
                executionApprovalResult.ExpectedWholeRequestReductionP95,
                observedWholeRequestReductionP95,
                behaviorEvidence,
                successfulTaskCount),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase2ActiveCanaryResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Active Canary Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Decision: `{result.Decision}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Observation mode: `{result.ObservationMode}`");
        builder.AppendLine();
        builder.AppendLine("## Canary Scope");
        builder.AppendLine();
        builder.AppendLine($"- Request kinds: `{string.Join(", ", result.CanaryScope.RequestKinds)}`");
        builder.AppendLine($"- Surface allowlist: `{string.Join(", ", result.CanaryScope.SurfaceAllowlist)}`");
        builder.AppendLine($"- Default enabled: `{(result.CanaryScope.DefaultEnabled ? "yes" : "no")}`");
        builder.AppendLine($"- Allowlist mode: `{result.CanaryScope.AllowlistMode}`");
        builder.AppendLine();
        builder.AppendLine("## Execution Truth Scope");
        builder.AppendLine();
        builder.AppendLine($"- Execution mode: `{result.ExecutionTruthScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.ExecutionTruthScope.WorkerBackend}`");
        builder.AppendLine($"- Provider SDK execution required: `{(result.ExecutionTruthScope.ProviderSdkExecutionRequired ? "yes" : "no")}`");
        builder.AppendLine($"- Provider model behavior claim: `{result.ExecutionTruthScope.ProviderModelBehaviorClaim}`");
        builder.AppendLine($"- Behavioral non-inferiority scope: `{result.ExecutionTruthScope.BehavioralNonInferiorityScope}`");
        builder.AppendLine($"- Provider billed cost claim: `{result.ExecutionTruthScope.ProviderBilledCostClaim}`");
        builder.AppendLine();
        builder.AppendLine("## Attempted Task Cohort");
        builder.AppendLine();
        builder.AppendLine($"- Selection mode: `{result.AttemptedTaskCohort.SelectionMode}`");
        builder.AppendLine($"- Attempted task count: `{result.AttemptedTaskCohort.AttemptedTaskCount}`");
        builder.AppendLine($"- Successful attempted task count: `{result.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`");
        builder.AppendLine($"- Failed attempted task count: `{result.AttemptedTaskCohort.FailedAttemptedTaskCount}`");
        builder.AppendLine($"- Incomplete attempted task count: `{result.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`");
        builder.AppendLine($"- Covers frozen replay task set: `{(result.AttemptedTaskCohort.CoversFrozenReplayTaskSet ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Token Metrics");
        builder.AppendLine();
        builder.AppendLine($"- Target surface reduction ratio p95: `{FormatRatio(result.TokenMetrics.TargetSurfaceReductionRatioP95)}`");
        builder.AppendLine($"- Target surface share p95: `{FormatRatio(result.TokenMetrics.TargetSurfaceShareP95)}`");
        builder.AppendLine($"- Expected whole-request reduction p95: `{FormatRatio(result.TokenMetrics.ExpectedWholeRequestReductionP95)}`");
        builder.AppendLine($"- Observed whole-request reduction p95: `{FormatRatio(result.TokenMetrics.ObservedWholeRequestReductionP95)}`");
        builder.AppendLine($"- Baseline total tokens per successful task: `{FormatNumber(result.TokenMetrics.BaselineTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Candidate total tokens per successful task: `{FormatNumber(result.TokenMetrics.CandidateTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Delta total tokens per successful task: `{FormatSignedNumber(result.TokenMetrics.DeltaTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Relative change total tokens per successful task: `{FormatRatio(result.TokenMetrics.RelativeChangeTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Baseline context-window input tokens p95: `{FormatNumber(result.TokenMetrics.BaselineContextWindowInputTokensP95)}`");
        builder.AppendLine($"- Candidate context-window input tokens p95: `{FormatNumber(result.TokenMetrics.CandidateContextWindowInputTokensP95)}`");
        builder.AppendLine($"- Baseline billable uncached input tokens p95: `{FormatNumber(result.TokenMetrics.BaselineBillableInputTokensUncachedP95)}`");
        builder.AppendLine($"- Candidate billable uncached input tokens p95: `{FormatNumber(result.TokenMetrics.CandidateBillableInputTokensUncachedP95)}`");
        builder.AppendLine();
        builder.AppendLine("## Non-Inferiority");
        builder.AppendLine();
        builder.AppendLine($"- Passed: `{(result.NonInferiority.Passed ? "yes" : "no")}`");
        builder.AppendLine($"- Sample size sufficient: `{(result.NonInferiority.SampleSizeSufficient ? "yes" : "no")}`");
        builder.AppendLine($"- Manual review required: `{(result.NonInferiority.ManualReviewRequired ? "yes" : "no")}`");
        builder.AppendLine($"- Task success rate delta (pp): `{FormatNullableSignedNumber(result.NonInferiority.TaskSuccessRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Review admission rate delta (pp): `{FormatNullableSignedNumber(result.NonInferiority.ReviewAdmissionRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Constraint violation rate delta (pp): `{FormatNullableSignedNumber(result.NonInferiority.ConstraintViolationRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Retry count per task relative delta: `{FormatNullableSignedNumber(result.NonInferiority.RetryCountPerTaskRelativeDelta)}`");
        builder.AppendLine($"- Repair count per task relative delta: `{FormatNullableSignedNumber(result.NonInferiority.RepairCountPerTaskRelativeDelta)}`");
        builder.AppendLine($"- Provider context cap hit delta (pp): `{FormatSignedNumber(result.NonInferiority.ProviderContextCapHitRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Internal prompt budget cap hit delta (pp): `{FormatSignedNumber(result.NonInferiority.InternalPromptBudgetCapHitRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Section budget cap hit delta (pp): `{FormatSignedNumber(result.NonInferiority.SectionBudgetCapHitRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Trim-loop cap hit delta (pp): `{FormatSignedNumber(result.NonInferiority.TrimLoopCapHitRateDeltaPercentagePoints)}`");
        if (result.NonInferiority.UnavailableMetrics.Count > 0)
        {
            builder.AppendLine($"- Unavailable metrics: `{string.Join(", ", result.NonInferiority.UnavailableMetrics)}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Safety");
        builder.AppendLine();
        builder.AppendLine($"- Hard fail count: `{result.Safety.HardFailCount}`");
        builder.AppendLine($"- Rollback triggered: `{(result.Safety.RollbackTriggered ? "yes" : "no")}`");
        builder.AppendLine($"- Manual review required: `{(result.Safety.ManualReviewRequired ? "yes" : "no")}`");
        if (result.Safety.HardFailConditionsTriggered.Count > 0)
        {
            builder.AppendLine($"- Hard fail conditions triggered: `{string.Join(", ", result.Safety.HardFailConditionsTriggered)}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Threshold Evaluations");
        builder.AppendLine();
        foreach (var evaluation in result.NonInferiority.ThresholdEvaluations)
        {
            builder.AppendLine($"- `{evaluation.MetricId}` -> baseline=`{FormatNullableNumber(evaluation.BaselineValue)}` candidate=`{FormatNullableNumber(evaluation.CandidateValue)}` evaluated=`{(evaluation.Evaluated ? "yes" : "no")}` passed=`{(evaluation.Passed ? "yes" : "no")}` delta=`{FormatNullableSignedNumber(evaluation.DeltaValue)}` reason=`{evaluation.Reason ?? "none"}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Execution approval markdown: `{result.ExecutionApprovalMarkdownArtifactPath}`");
        builder.AppendLine($"- Execution approval json: `{result.ExecutionApprovalJsonArtifactPath}`");
        builder.AppendLine($"- Baseline evidence markdown: `{result.BaselineEvidenceMarkdownArtifactPath}`");
        builder.AppendLine($"- Baseline evidence json: `{result.BaselineEvidenceJsonArtifactPath}`");
        builder.AppendLine($"- Non-inferiority cohort markdown: `{result.NonInferiorityCohortMarkdownArtifactPath}`");
        builder.AppendLine($"- Non-inferiority cohort json: `{result.NonInferiorityCohortJsonArtifactPath}`");
        builder.AppendLine($"- Worker recollect markdown: `{result.WorkerRecollectMarkdownArtifactPath}`");
        builder.AppendLine($"- Worker recollect json: `{result.WorkerRecollectJsonArtifactPath}`");

        builder.AppendLine();
        builder.AppendLine("## Sample Replay");
        builder.AppendLine();
        foreach (var sample in result.Samples)
        {
            builder.AppendLine($"- `{sample.TaskId}` baseline=`{sample.BaselineWholeRequestTokens}` candidate=`{sample.CandidateWholeRequestTokens}` candidate_applied=`{(sample.CandidateApplied ? "yes" : "no")}` target_surface_reduction=`{FormatRatio(sample.TargetSurfaceReductionRatio)}` whole_request_reduction=`{FormatRatio(sample.WholeRequestReductionRatio)}`");
        }

        if (result.BlockingReasons.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Blocking Reasons");
            builder.AppendLine();
            foreach (var reason in result.BlockingReasons)
            {
                builder.AppendLine($"- `{reason}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Next Required Actions");
        builder.AppendLine();
        foreach (var action in result.NextRequiredActions)
        {
            builder.AppendLine($"- {action}");
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

    private static RuntimeTokenPhase2ActiveCanaryThresholdEvaluation[] BuildThresholdEvaluations(
        IReadOnlyList<RuntimeTokenPhase2NonInferiorityThreshold> thresholds,
        double baselineTokensPerSuccessfulTask,
        double candidateTokensPerSuccessfulTask,
        double relativeChangeTokensPerSuccessfulTask,
        double providerContextCapHitDelta,
        double internalPromptCapHitDelta,
        double sectionBudgetCapHitDelta,
        double trimLoopCapHitDelta,
        bool lowBaseManualReviewRequired,
        RuntimeTokenPhase2BehaviorEvidenceSnapshot behaviorEvidence)
    {
        return thresholds
            .Select(threshold =>
            {
                var metricId = threshold.MetricId;
                var baselineValue = ResolveBaselineValue(metricId, baselineTokensPerSuccessfulTask, behaviorEvidence);
                var candidateValue = ResolveCandidateValue(metricId, candidateTokensPerSuccessfulTask, behaviorEvidence);
                var delta = ResolveDeltaValue(
                    metricId,
                    relativeChangeTokensPerSuccessfulTask,
                    providerContextCapHitDelta,
                    internalPromptCapHitDelta,
                    sectionBudgetCapHitDelta,
                    trimLoopCapHitDelta,
                    behaviorEvidence);

                if (behaviorEvidence.UnavailableMetrics.Contains(metricId))
                {
                    return new RuntimeTokenPhase2ActiveCanaryThresholdEvaluation
                    {
                        MetricId = metricId,
                        ThresholdKind = threshold.ThresholdKind,
                        Comparator = threshold.Comparator,
                        ThresholdValue = threshold.ThresholdValue,
                        Units = threshold.Units,
                        BaselineValue = baselineValue,
                        CandidateValue = candidateValue,
                        DeltaValue = delta,
                        Evaluated = false,
                        Passed = false,
                        Reason = behaviorEvidence.UnavailableReason,
                    };
                }

                if (lowBaseManualReviewRequired)
                {
                    return new RuntimeTokenPhase2ActiveCanaryThresholdEvaluation
                    {
                        MetricId = metricId,
                        ThresholdKind = threshold.ThresholdKind,
                        Comparator = threshold.Comparator,
                        ThresholdValue = threshold.ThresholdValue,
                        Units = threshold.Units,
                        BaselineValue = baselineValue,
                        CandidateValue = candidateValue,
                        DeltaValue = delta,
                        Evaluated = false,
                        Passed = false,
                        Reason = "low_base_count_requires_manual_review",
                    };
                }

                var passed = delta is not null && delta.Value <= threshold.ThresholdValue;
                return new RuntimeTokenPhase2ActiveCanaryThresholdEvaluation
                {
                    MetricId = metricId,
                    ThresholdKind = threshold.ThresholdKind,
                    Comparator = threshold.Comparator,
                    ThresholdValue = threshold.ThresholdValue,
                    Units = threshold.Units,
                    BaselineValue = baselineValue,
                    CandidateValue = candidateValue,
                    DeltaValue = delta,
                    Evaluated = delta is not null,
                    Passed = passed,
                    Reason = delta is null ? "metric_value_unavailable" : null,
                };
            })
            .ToArray();
    }

    private static double? ResolveBaselineValue(
        string metricId,
        double baselineTokensPerSuccessfulTask,
        RuntimeTokenPhase2BehaviorEvidenceSnapshot behaviorEvidence)
    {
        return metricId switch
        {
            "task_success_rate" => behaviorEvidence.BaselineTaskSuccessRate,
            "review_admission_rate" => behaviorEvidence.BaselineReviewAdmissionRate,
            "constraint_violation_rate" => behaviorEvidence.BaselineConstraintViolationRate,
            "retry_count_per_task" => behaviorEvidence.BaselineRetryCountPerTask,
            "repair_count_per_task" => behaviorEvidence.BaselineRepairCountPerTask,
            "total_tokens_per_successful_task" => baselineTokensPerSuccessfulTask,
            _ => null,
        };
    }

    private static double? ResolveCandidateValue(
        string metricId,
        double candidateTokensPerSuccessfulTask,
        RuntimeTokenPhase2BehaviorEvidenceSnapshot behaviorEvidence)
    {
        return metricId switch
        {
            "task_success_rate" => behaviorEvidence.CandidateTaskSuccessRate,
            "review_admission_rate" => behaviorEvidence.CandidateReviewAdmissionRate,
            "constraint_violation_rate" => behaviorEvidence.CandidateConstraintViolationRate,
            "retry_count_per_task" => behaviorEvidence.CandidateRetryCountPerTask,
            "repair_count_per_task" => behaviorEvidence.CandidateRepairCountPerTask,
            "total_tokens_per_successful_task" => candidateTokensPerSuccessfulTask,
            _ => null,
        };
    }

    private static double? ResolveDeltaValue(
        string metricId,
        double relativeChangeTokensPerSuccessfulTask,
        double providerContextCapHitDelta,
        double internalPromptCapHitDelta,
        double sectionBudgetCapHitDelta,
        double trimLoopCapHitDelta,
        RuntimeTokenPhase2BehaviorEvidenceSnapshot behaviorEvidence)
    {
        return metricId switch
        {
            "task_success_rate" => behaviorEvidence.TaskSuccessRateDeltaPercentagePoints,
            "review_admission_rate" => behaviorEvidence.ReviewAdmissionRateDeltaPercentagePoints,
            "constraint_violation_rate" => behaviorEvidence.ConstraintViolationRateDeltaPercentagePoints,
            "retry_count_per_task" => behaviorEvidence.RetryCountPerTaskRelativeDelta,
            "repair_count_per_task" => behaviorEvidence.RepairCountPerTaskRelativeDelta,
            "provider_context_cap_hit_rate" => providerContextCapHitDelta,
            "internal_prompt_budget_cap_hit_rate" => internalPromptCapHitDelta,
            "section_budget_cap_hit_rate" => sectionBudgetCapHitDelta,
            "trim_loop_cap_hit_rate" => trimLoopCapHitDelta,
            "total_tokens_per_successful_task" => relativeChangeTokensPerSuccessfulTask,
            _ => null,
        };
    }

    private static IReadOnlyList<string> BuildNextRequiredActions(string decision, bool manualReviewRequired)
    {
        if (string.Equals(decision, "pass", StringComparison.Ordinal))
        {
            return
            [
                "submit this result line to phase_2_active_canary_result_review before any rollout discussion",
                "keep the canary scope pinned to worker-only explicit allowlist until a separate widening review is approved"
            ];
        }

        if (string.Equals(decision, "fail", StringComparison.Ordinal))
        {
            return
            [
                "trigger fallback to original_worker_system_instructions for the controlled canary scope",
                "classify the failing threshold or hard-fail signal before any further canary execution"
            ];
        }

        var actions = new List<string>
        {
            "do not treat this result as proof that cost optimization has completed",
            "keep the canary scope worker-only and default-off outside the explicit allowlist"
        };
        if (manualReviewRequired)
        {
            actions.Add("resolve low-base or unavailable behavior metrics through operator review before any rollout decision");
        }

        actions.Add("collect a larger canary result line if execution-grade non-inferiority proof is required");
        return actions;
    }

    private static IReadOnlyList<string> BuildNotes(
        string decision,
        double expectedWholeRequestReductionP95,
        double observedWholeRequestReductionP95,
        RuntimeTokenPhase2BehaviorEvidenceSnapshot behaviorEvidence,
        int successfulTaskCount)
    {
        var notes = new List<string>
        {
            "This result line replays the live worker request path under the approved canary mechanism; it does not enable runtime shadow or main-path replacement.",
            "Execution truth for this line is no_provider_agent_mediated on the formal null_worker backend. It does not claim provider-backed model-behavior non-inferiority.",
            $"Expected whole-request p95 reduction was `{FormatRatio(expectedWholeRequestReductionP95)}` and observed replay reduction was `{FormatRatio(observedWholeRequestReductionP95)}`.",
            $"Successful-task denominator for this line is `{successfulTaskCount}` from the frozen canary cohort, not the earlier phase-0a five-task baseline."
        };

        notes.Add(behaviorEvidence.UnavailableMetrics.Count == 0
            ? "Behavior metrics were derived from task node and execution run report truth on a null_worker-only cohort, so baseline and candidate execution behavior are treated as equivalent only for the current runtime mode and approved worker wrapper surface."
            : "Behavior metrics remain unavailable because the cohort cannot yet prove execution-grade equivalence from task node and run report truth for the current runtime mode.");
        notes.Add($"Attempted-task evidence for this line uses `{behaviorEvidence.ObservationMode}` with successful-task denominator `{successfulTaskCount}`.");

        notes.Add("Provider-billed cost is not applicable in this line, and no real provider SDK/API sample is required for this runtime-mode-only conclusion.");

        if (string.Equals(decision, "pass", StringComparison.Ordinal))
        {
            notes.Add("The controlled replay line passed available thresholds, but rollout still requires a separate post-result governance decision.");
        }
        else if (string.Equals(decision, "fail", StringComparison.Ordinal))
        {
            notes.Add("The controlled replay line observed a failing threshold or hard-fail signal and should remain on fallback.");
        }
        else
        {
            notes.Add("The controlled replay line remains inconclusive because behavior-grade non-inferiority evidence is unavailable or requires manual review.");
        }

        return notes;
    }

    private static WorkerAiRequestFactory CreateWorkerFactory(AiProviderConfig workerProviderConfig, bool enabled)
    {
        return new WorkerAiRequestFactory(
            workerProviderConfig.MaxOutputTokens,
            workerProviderConfig.RequestTimeoutSeconds,
            workerProviderConfig.Model,
            workerProviderConfig.ReasoningEffort,
            CreateCanaryService(enabled));
    }

    private static RuntimeTokenWorkerWrapperCanaryService CreateCanaryService(bool enabled)
    {
        if (!enabled)
        {
            return new RuntimeTokenWorkerWrapperCanaryService(_ => null);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.CanaryEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.RequestKind,
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        return new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);
    }

    private static int EstimateWholeRequestTokens(LlmRequestEnvelopeDraft draft)
    {
        return ContextBudgetPolicyResolver.EstimateTokens(RuntimeTelemetryHashing.Normalize(draft.WholeRequestText));
    }

    private static double ComputeReductionRatio(double baseline, double candidate)
    {
        if (baseline <= 0d)
        {
            return 0d;
        }

        return (baseline - candidate) / baseline;
    }

    private static double ComputePercentagePointDelta(IReadOnlyList<bool> baselineValues, IReadOnlyList<bool> candidateValues)
    {
        return ComputeRate(candidateValues) - ComputeRate(baselineValues);
    }

    private static double ComputeRate(IReadOnlyList<bool> values)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        return (values.Count(item => item) * 100d) / values.Count;
    }

    private static double ComputeRelativeDelta(double baselineValue, double candidateValue)
    {
        if (baselineValue <= 0d)
        {
            return candidateValue <= 0d ? 0d : double.PositiveInfinity;
        }

        return (candidateValue - baselineValue) / baselineValue;
    }

    private static RuntimeTokenPhase2BehaviorEvidenceSnapshot DeriveBehaviorEvidence(
        RuntimeTokenBaselineAttemptedTaskCohort attemptedTaskCohort,
        int expectedTaskCount)
    {
        if (attemptedTaskCohort.Tasks.Count == 0 || attemptedTaskCohort.AttemptedTaskCount == 0)
        {
            return RuntimeTokenPhase2BehaviorEvidenceSnapshot.Unavailable(
                expectedTaskCount,
                "attempted_task_cohort_not_available");
        }

        if (!attemptedTaskCohort.CoversFrozenReplayTaskSet || attemptedTaskCohort.AttemptedTaskCount != expectedTaskCount)
        {
            return RuntimeTokenPhase2BehaviorEvidenceSnapshot.Unavailable(
                attemptedTaskCohort.AttemptedTaskCount,
                "attempted_task_cohort_does_not_cover_replay_task_set");
        }

        if (attemptedTaskCohort.Tasks.Any(item => !item.Attempted || string.IsNullOrWhiteSpace(item.LatestRunStatus)))
        {
            return RuntimeTokenPhase2BehaviorEvidenceSnapshot.Unavailable(
                attemptedTaskCohort.AttemptedTaskCount,
                "execution_run_report_truth_missing_for_canary_cohort");
        }

        if (attemptedTaskCohort.Tasks.Any(item => !string.Equals(item.WorkerBackend, "null_worker", StringComparison.Ordinal)))
        {
            return RuntimeTokenPhase2BehaviorEvidenceSnapshot.Unavailable(
                attemptedTaskCohort.AttemptedTaskCount,
                "behavior_equivalence_requires_null_worker_only_cohort");
        }

        var attemptedTasks = attemptedTaskCohort.Tasks.Where(item => item.Attempted).ToArray();
        var baselineTaskSuccessRate = ComputeRate(attemptedTasks.Select(item => item.SuccessfulAttempted).ToArray());
        var baselineReviewAdmissionRate = ComputeRate(attemptedTasks.Select(item => item.ReviewAdmissionAccepted).ToArray());
        var baselineConstraintViolationRate = ComputeRate(attemptedTasks.Select(item => item.ConstraintViolationObserved).ToArray());
        var baselineRetryCountPerTask = attemptedTasks.Average(item => item.RetryCount);
        var baselineRepairCountPerTask = attemptedTasks.Average(item => item.RepairCount);
        var successfulTaskCount = attemptedTaskCohort.SuccessfulAttemptedTaskCount;

        return new RuntimeTokenPhase2BehaviorEvidenceSnapshot
        {
            ObservationMode = "controlled_worker_request_path_replay_with_null_worker_attempted_task_truth",
            CohortTaskCount = attemptedTaskCohort.AttemptedTaskCount,
            SuccessfulTaskCount = successfulTaskCount,
            BaselineTaskSuccessRate = baselineTaskSuccessRate,
            CandidateTaskSuccessRate = baselineTaskSuccessRate,
            TaskSuccessRateDeltaPercentagePoints = 0d,
            BaselineReviewAdmissionRate = baselineReviewAdmissionRate,
            CandidateReviewAdmissionRate = baselineReviewAdmissionRate,
            ReviewAdmissionRateDeltaPercentagePoints = 0d,
            BaselineConstraintViolationRate = baselineConstraintViolationRate,
            CandidateConstraintViolationRate = baselineConstraintViolationRate,
            ConstraintViolationRateDeltaPercentagePoints = 0d,
            BaselineRetryCountPerTask = baselineRetryCountPerTask,
            CandidateRetryCountPerTask = baselineRetryCountPerTask,
            RetryCountPerTaskRelativeDelta = 0d,
            BaselineRepairCountPerTask = baselineRepairCountPerTask,
            CandidateRepairCountPerTask = baselineRepairCountPerTask,
            RepairCountPerTaskRelativeDelta = 0d,
            UnavailableMetrics = Array.Empty<string>(),
            UnavailableReason = string.Empty,
        };
    }

    private static WorkerSelectionDecision BuildWorkerSelection(string repoId, AiProviderConfig workerProviderConfig, string taskId)
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
            RouteSource = "phase2_active_canary_result",
            RouteReason = "controlled worker canary replay",
            Summary = "controlled worker canary replay",
            ReasonCode = "phase2_active_canary_result",
            Profile = WorkerExecutionProfile.UntrustedDefault,
            SelectedBecause = ["phase2_active_canary_result"],
        };
    }

    private static T LoadRequired<T>(ControlPlanePaths paths, string repoRelativePath, string description)
    {
        var fullPath = Path.IsPathRooted(repoRelativePath)
            ? repoRelativePath
            : Path.Combine(paths.RepoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Phase 2 active canary result requires {description} at '{fullPath}'.");
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), LoadJsonOptions)
               ?? throw new InvalidOperationException($"Phase 2 active canary result could not deserialize {description} at '{fullPath}'.");
    }

    private static void ValidateInputs(
        RuntimeTokenPhase2ActiveCanaryExecutionApprovalResult executionApprovalResult,
        RuntimeTokenBaselineEvidenceResult baselineEvidenceResult,
        RuntimeTokenPhase2NonInferiorityCohortFreezeResult nonInferiorityCohortFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        DateOnly resultDate)
    {
        if (executionApprovalResult.ResultDate != resultDate
            || baselineEvidenceResult.ResultDate != resultDate
            || nonInferiorityCohortFreezeResult.ResultDate != resultDate
            || workerRecollectResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Phase 2 active canary result requires execution approval, baseline evidence, non-inferiority cohort, and worker recollect dates to match the requested result date.");
        }

        if (!executionApprovalResult.ActiveCanaryApproved || !executionApprovalResult.CanaryExecutionAuthorized)
        {
            throw new InvalidOperationException("Phase 2 active canary result requires a separately approved active canary execution line.");
        }

        if (!nonInferiorityCohortFreezeResult.NonInferiorityCohortFrozen)
        {
            throw new InvalidOperationException("Phase 2 active canary result requires a frozen non-inferiority cohort.");
        }

        if (!baselineEvidenceResult.OutcomeBinding.TaskCostViewTrusted
            || !baselineEvidenceResult.OutcomeBinding.ContextWindowView.TokensPerSuccessfulTask.HasValue)
        {
            throw new InvalidOperationException("Phase 2 active canary result requires a trusted baseline task-cost view.");
        }

        if (!string.Equals(executionApprovalResult.CohortId, baselineEvidenceResult.Aggregation.Cohort.CohortId, StringComparison.Ordinal)
            || !string.Equals(executionApprovalResult.CohortId, nonInferiorityCohortFreezeResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(executionApprovalResult.CohortId, workerRecollectResult.Cohort.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Phase 2 active canary result requires execution approval, baseline evidence, non-inferiority cohort, and worker recollect to share the same cohort id.");
        }

        if (!string.Equals(executionApprovalResult.TargetSurface, RuntimeTokenWorkerWrapperCanaryService.TargetSurface, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Phase 2 active canary result only supports the approved worker system wrapper surface.");
        }
    }

    internal static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-active-canary-result-{resultDate:yyyy-MM-dd}.md");
    }

    internal static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"active-canary-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var ordered = values.OrderBy(item => item).ToArray();
        var position = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var fraction = position - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatNullableNumber(double? value)
        => value.HasValue ? FormatNumber(value.Value) : "N/A";

    private static string FormatRatio(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatSignedNumber(double value) => value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);

    private static string FormatNullableSignedNumber(double? value)
        => value.HasValue ? FormatSignedNumber(value.Value) : "N/A";

    private sealed record RuntimeTokenPhase2BehaviorEvidenceSnapshot
    {
        private static readonly string[] BehaviorMetricIds =
        [
            "task_success_rate",
            "review_admission_rate",
            "constraint_violation_rate",
            "retry_count_per_task",
            "repair_count_per_task"
        ];

        public string ObservationMode { get; init; } = "controlled_worker_request_path_replay";

        public int CohortTaskCount { get; init; }

        public int? SuccessfulTaskCount { get; init; }

        public double? BaselineTaskSuccessRate { get; init; }

        public double? CandidateTaskSuccessRate { get; init; }

        public double? TaskSuccessRateDeltaPercentagePoints { get; init; }

        public double? BaselineReviewAdmissionRate { get; init; }

        public double? CandidateReviewAdmissionRate { get; init; }

        public double? ReviewAdmissionRateDeltaPercentagePoints { get; init; }

        public double? BaselineConstraintViolationRate { get; init; }

        public double? CandidateConstraintViolationRate { get; init; }

        public double? ConstraintViolationRateDeltaPercentagePoints { get; init; }

        public double? BaselineRetryCountPerTask { get; init; }

        public double? CandidateRetryCountPerTask { get; init; }

        public double? RetryCountPerTaskRelativeDelta { get; init; }

        public double? BaselineRepairCountPerTask { get; init; }

        public double? CandidateRepairCountPerTask { get; init; }

        public double? RepairCountPerTaskRelativeDelta { get; init; }

        public IReadOnlyList<string> UnavailableMetrics { get; init; } = Array.Empty<string>();

        public string UnavailableReason { get; init; } = string.Empty;

        public static RuntimeTokenPhase2BehaviorEvidenceSnapshot Unavailable(int cohortTaskCount, string reason)
        {
            return new RuntimeTokenPhase2BehaviorEvidenceSnapshot
            {
                CohortTaskCount = cohortTaskCount,
                UnavailableMetrics = BehaviorMetricIds,
                UnavailableReason = reason,
            };
        }
    }
}
