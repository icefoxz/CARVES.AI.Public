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

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase3PostRolloutEvidenceCollectionService
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

    public RuntimeTokenPhase3PostRolloutEvidenceCollectionService(
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

    public RuntimeTokenPhase3PostRolloutEvidenceResult Persist(
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        RuntimeTokenPhase3ReplacementScopeFreezeResult scopeFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult)
    {
        return Persist(
            paths,
            reviewResult,
            scopeFreezeResult,
            workerRecollectResult,
            workerProviderConfig,
            repoId,
            gitClient,
            taskGraphRepository,
            reviewResult.ResultDate);
    }

    internal static RuntimeTokenPhase3PostRolloutEvidenceResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        RuntimeTokenPhase3ReplacementScopeFreezeResult scopeFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        AiProviderConfig workerProviderConfig,
        string repoId,
        IGitClient gitClient,
        ITaskGraphRepository taskGraphRepository,
        DateOnly resultDate,
        DateTimeOffset? collectedAtUtc = null)
    {
        ValidateInputs(reviewResult, scopeFreezeResult, workerRecollectResult, resultDate);

        var taskGraph = taskGraphRepository.Load();
        var baseCommit = gitClient.TryGetCurrentCommit(paths.RepoRoot);
        var baselineFactory = CreateWorkerFactory(workerProviderConfig, mainPathDefaultEnabled: false);
        var rolloutFactory = CreateWorkerFactory(workerProviderConfig, mainPathDefaultEnabled: true);
        var baselineWholeTokens = new List<double>(workerRecollectResult.Tasks.Count);
        var candidateWholeTokens = new List<double>(workerRecollectResult.Tasks.Count);
        var candidateDefaultRequestCount = 0;
        var fallbackRequestCount = 0;
        var killSwitchUsed = false;
        var hardFailConditionsTriggered = new HashSet<string>(StringComparer.Ordinal);
        var samples = new List<RuntimeTokenPhase3PostRolloutSample>(workerRecollectResult.Tasks.Count);

        foreach (var record in workerRecollectResult.Tasks)
        {
            if (!taskGraph.Tasks.TryGetValue(record.TaskId, out var task))
            {
                throw new InvalidOperationException($"Post-rollout evidence collection requires task '{record.TaskId}' from task graph truth.");
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
            var candidateRequest = rolloutFactory.Create(
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

            var baselineDraft = baselineRequest.RequestEnvelopeDraft
                               ?? throw new InvalidOperationException($"Post-rollout evidence collection requires baseline request envelope draft for task '{task.TaskId}'.");
            var candidateDraft = candidateRequest.RequestEnvelopeDraft
                                ?? throw new InvalidOperationException($"Post-rollout evidence collection requires candidate request envelope draft for task '{task.TaskId}'.");
            var baselineRequestTokens = EstimateWholeRequestTokens(baselineDraft);
            var candidateRequestTokens = EstimateWholeRequestTokens(candidateDraft);
            baselineWholeTokens.Add(baselineRequestTokens);
            candidateWholeTokens.Add(candidateRequestTokens);

            var candidateDecisionMode = candidateRequest.Metadata.GetValueOrDefault("worker_wrapper_decision_mode") ?? string.Empty;
            var candidateDecisionReason = candidateRequest.Metadata.GetValueOrDefault("worker_wrapper_canary_decision_reason") ?? string.Empty;
            var candidateApplied = string.Equals(
                candidateRequest.Metadata.GetValueOrDefault("worker_wrapper_canary_candidate_applied"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            var candidateDefaultApplied = candidateApplied
                                          && string.Equals(candidateDecisionMode, "limited_main_path_default", StringComparison.Ordinal)
                                          && string.Equals(candidateDecisionReason, "main_path_default", StringComparison.Ordinal);

            if (candidateDefaultApplied)
            {
                candidateDefaultRequestCount += 1;
            }
            else
            {
                fallbackRequestCount += 1;
                hardFailConditionsTriggered.Add("candidate_default_not_applied_within_frozen_scope");
            }

            if (string.Equals(
                    candidateRequest.Metadata.GetValueOrDefault("worker_wrapper_canary_kill_switch_active"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                killSwitchUsed = true;
                hardFailConditionsTriggered.Add("kill_switch_active_during_post_rollout_collection");
            }

            samples.Add(new RuntimeTokenPhase3PostRolloutSample
            {
                TaskId = record.TaskId,
                RunId = record.RunId,
                BaselineRequestId = baselineRequest.RequestId,
                CandidateRequestId = candidateRequest.RequestId,
                BaselineDecisionMode = baselineRequest.Metadata.GetValueOrDefault("worker_wrapper_decision_mode") ?? string.Empty,
                CandidateDecisionMode = candidateDecisionMode,
                CandidateDecisionReason = candidateDecisionReason,
                CandidateDefaultApplied = candidateDefaultApplied,
                BaselineWholeRequestTokens = baselineRequestTokens,
                CandidateWholeRequestTokens = candidateRequestTokens,
                WholeRequestReductionRatio = ComputeReductionRatio(baselineRequestTokens, candidateRequestTokens),
            });
        }

        var behaviorEvidence = DeriveBehaviorEvidence(workerRecollectResult.AttemptedTaskCohort, workerRecollectResult.Tasks.Count);
        var successfulTaskCount = workerRecollectResult.AttemptedTaskCohort.SuccessfulAttemptedTaskCount;
        if (successfulTaskCount <= 0)
        {
            throw new InvalidOperationException("Post-rollout evidence collection requires a successful-task denominator from the frozen attempted-task cohort.");
        }

        var baselineWholeRequestP95 = Percentile(baselineWholeTokens, 0.95);
        var candidateWholeRequestP95 = Percentile(candidateWholeTokens, 0.95);
        var baselineTokensPerSuccessfulTask = baselineWholeTokens.Sum() / successfulTaskCount;
        var candidateTokensPerSuccessfulTask = candidateWholeTokens.Sum() / successfulTaskCount;
        var deltaTokensPerSuccessfulTask = candidateTokensPerSuccessfulTask - baselineTokensPerSuccessfulTask;
        var relativeChangeTokensPerSuccessfulTask = baselineTokensPerSuccessfulTask <= 0d
            ? 0d
            : deltaTokensPerSuccessfulTask / baselineTokensPerSuccessfulTask;
        var observedWholeRequestReductionP95 = ComputeReductionRatio(baselineWholeRequestP95, candidateWholeRequestP95);
        var hardFailCount = hardFailConditionsTriggered.Count;

        var limitedMainPathImplementationObserved = workerRecollectResult.Tasks.Count > 0
                                                    && candidateDefaultRequestCount == workerRecollectResult.Tasks.Count
                                                    && fallbackRequestCount == 0
                                                    && !killSwitchUsed
                                                    && hardFailCount == 0;
        var postRolloutTokenEvidenceObserved = limitedMainPathImplementationObserved && baselineWholeTokens.Count > 0;
        var postRolloutBehaviorEvidenceObserved = behaviorEvidence.Observed;

        var blockingReasons = new List<string>();
        if (!limitedMainPathImplementationObserved)
        {
            blockingReasons.Add("limited_main_path_default_not_observed_on_frozen_scope");
        }

        if (!postRolloutTokenEvidenceObserved)
        {
            blockingReasons.Add("post_rollout_token_evidence_not_observed");
        }

        if (!postRolloutBehaviorEvidenceObserved)
        {
            blockingReasons.Add("post_rollout_behavior_evidence_not_observed");
        }

        var result = new RuntimeTokenPhase3PostRolloutEvidenceResult
        {
            ResultDate = resultDate,
            CollectedAtUtc = collectedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = reviewResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetMarkdownArtifactPath(paths, resultDate)),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, GetJsonArtifactPath(paths, resultDate)),
            MainPathReplacementReviewMarkdownArtifactPath = reviewResult.MarkdownArtifactPath,
            MainPathReplacementReviewJsonArtifactPath = reviewResult.JsonArtifactPath,
            ReplacementScopeFreezeMarkdownArtifactPath = scopeFreezeResult.MarkdownArtifactPath,
            ReplacementScopeFreezeJsonArtifactPath = scopeFreezeResult.JsonArtifactPath,
            WorkerRecollectMarkdownArtifactPath = workerRecollectResult.MarkdownArtifactPath,
            WorkerRecollectJsonArtifactPath = workerRecollectResult.JsonArtifactPath,
            TargetSurface = reviewResult.TargetSurface,
            RequestKind = reviewResult.RequestKind,
            CandidateVersion = reviewResult.CandidateVersion,
            FallbackVersion = reviewResult.FallbackVersion,
            ObservationMode = "limited_main_path_default_replay_with_null_worker_attempted_task_truth",
            EvidenceStatus = blockingReasons.Count == 0 ? "observed_for_frozen_scope" : "incomplete_post_rollout_evidence",
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
            RolloutScope = new RuntimeTokenPhase3PostRolloutScope
            {
                RequestKind = reviewResult.RequestKind,
                Surface = reviewResult.TargetSurface,
                ExecutionMode = reviewResult.ExecutionTruthScope.ExecutionMode,
                WorkerBackend = reviewResult.ExecutionTruthScope.WorkerBackend,
                DefaultEnabled = true,
                FullRollout = false,
                AllowlistMode = "frozen_scope",
            },
            TokenEvidence = new RuntimeTokenPhase3PostRolloutTokenEvidence
            {
                BaselineRequestCount = baselineWholeTokens.Count,
                CandidateDefaultRequestCount = candidateDefaultRequestCount,
                FallbackRequestCount = fallbackRequestCount,
                PostRolloutWholeRequestReductionP95 = observedWholeRequestReductionP95,
                BaselineTotalTokensPerSuccessfulTask = baselineTokensPerSuccessfulTask,
                CandidateTotalTokensPerSuccessfulTask = candidateTokensPerSuccessfulTask,
                DeltaTotalTokensPerSuccessfulTask = deltaTokensPerSuccessfulTask,
                RelativeChangeTotalTokensPerSuccessfulTask = relativeChangeTokensPerSuccessfulTask,
                BaselineContextWindowInputTokensP95 = baselineWholeRequestP95,
                CandidateContextWindowInputTokensP95 = candidateWholeRequestP95,
                DeltaContextWindowInputTokensP95 = candidateWholeRequestP95 - baselineWholeRequestP95,
                BaselineBillableInputTokensUncachedP95 = baselineWholeRequestP95,
                CandidateBillableInputTokensUncachedP95 = candidateWholeRequestP95,
                DeltaBillableInputTokensUncachedP95 = candidateWholeRequestP95 - baselineWholeRequestP95,
            },
            BehaviorEvidence = behaviorEvidence,
            Safety = new RuntimeTokenPhase3PostRolloutSafetyEvidence
            {
                HardFailCount = hardFailCount,
                RollbackTriggered = hardFailCount > 0,
                KillSwitchUsed = killSwitchUsed,
                HardFailConditionsTriggered = hardFailConditionsTriggered.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            },
            LimitedMainPathImplementationObserved = limitedMainPathImplementationObserved,
            PostRolloutTokenEvidenceObserved = postRolloutTokenEvidenceObserved,
            PostRolloutBehaviorEvidenceObserved = postRolloutBehaviorEvidenceObserved,
            Samples = samples,
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            NextRequiredActions = BuildNextRequiredActions(blockingReasons.Count == 0),
            Notes = BuildNotes(observedWholeRequestReductionP95, successfulTaskCount),
        };

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string FormatMarkdown(RuntimeTokenPhase3PostRolloutEvidenceResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 3 Post-Rollout Evidence");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Collected at: `{result.CollectedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Evidence status: `{result.EvidenceStatus}`");
        builder.AppendLine($"- Limited main-path implementation observed: `{(result.LimitedMainPathImplementationObserved ? "yes" : "no")}`");
        builder.AppendLine($"- Post-rollout token evidence observed: `{(result.PostRolloutTokenEvidenceObserved ? "yes" : "no")}`");
        builder.AppendLine($"- Post-rollout behavior evidence observed: `{(result.PostRolloutBehaviorEvidenceObserved ? "yes" : "no")}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Request kind: `{result.RequestKind}`");
        builder.AppendLine($"- Candidate version: `{result.CandidateVersion}`");
        builder.AppendLine($"- Fallback version: `{result.FallbackVersion}`");
        builder.AppendLine($"- Observation mode: `{result.ObservationMode}`");
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
        builder.AppendLine("## Rollout Scope");
        builder.AppendLine();
        builder.AppendLine($"- Request kind: `{result.RolloutScope.RequestKind}`");
        builder.AppendLine($"- Surface: `{result.RolloutScope.Surface}`");
        builder.AppendLine($"- Execution mode: `{result.RolloutScope.ExecutionMode}`");
        builder.AppendLine($"- Worker backend: `{result.RolloutScope.WorkerBackend}`");
        builder.AppendLine($"- Default enabled: `{(result.RolloutScope.DefaultEnabled ? "yes" : "no")}`");
        builder.AppendLine($"- Full rollout: `{(result.RolloutScope.FullRollout ? "yes" : "no")}`");
        builder.AppendLine($"- Allowlist mode: `{result.RolloutScope.AllowlistMode}`");
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
        builder.AppendLine("## Token Evidence");
        builder.AppendLine();
        builder.AppendLine($"- Baseline request count: `{result.TokenEvidence.BaselineRequestCount}`");
        builder.AppendLine($"- Candidate default request count: `{result.TokenEvidence.CandidateDefaultRequestCount}`");
        builder.AppendLine($"- Fallback request count: `{result.TokenEvidence.FallbackRequestCount}`");
        builder.AppendLine($"- Post-rollout whole-request reduction p95: `{FormatRatio(result.TokenEvidence.PostRolloutWholeRequestReductionP95)}`");
        builder.AppendLine($"- Baseline total tokens per successful task: `{FormatNumber(result.TokenEvidence.BaselineTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Candidate total tokens per successful task: `{FormatNumber(result.TokenEvidence.CandidateTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Delta total tokens per successful task: `{FormatSignedNumber(result.TokenEvidence.DeltaTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Relative change total tokens per successful task: `{FormatRatio(result.TokenEvidence.RelativeChangeTotalTokensPerSuccessfulTask)}`");
        builder.AppendLine($"- Baseline context-window input tokens p95: `{FormatNumber(result.TokenEvidence.BaselineContextWindowInputTokensP95)}`");
        builder.AppendLine($"- Candidate context-window input tokens p95: `{FormatNumber(result.TokenEvidence.CandidateContextWindowInputTokensP95)}`");
        builder.AppendLine($"- Baseline billable uncached input tokens p95: `{FormatNumber(result.TokenEvidence.BaselineBillableInputTokensUncachedP95)}`");
        builder.AppendLine($"- Candidate billable uncached input tokens p95: `{FormatNumber(result.TokenEvidence.CandidateBillableInputTokensUncachedP95)}`");
        builder.AppendLine();
        builder.AppendLine("## Behavior Evidence");
        builder.AppendLine();
        builder.AppendLine($"- Observed: `{(result.BehaviorEvidence.Observed ? "yes" : "no")}`");
        builder.AppendLine($"- Attempted task count: `{result.BehaviorEvidence.AttemptedTaskCount}`");
        builder.AppendLine($"- Successful attempted task count: `{result.BehaviorEvidence.SuccessfulAttemptedTaskCount}`");
        builder.AppendLine($"- Failed attempted task count: `{result.BehaviorEvidence.FailedAttemptedTaskCount}`");
        builder.AppendLine($"- Incomplete attempted task count: `{result.BehaviorEvidence.IncompleteAttemptedTaskCount}`");
        builder.AppendLine($"- Task success rate delta (pp): `{FormatNullableSignedNumber(result.BehaviorEvidence.TaskSuccessRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Review admission rate delta (pp): `{FormatNullableSignedNumber(result.BehaviorEvidence.ReviewAdmissionRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Constraint violation rate delta (pp): `{FormatNullableSignedNumber(result.BehaviorEvidence.ConstraintViolationRateDeltaPercentagePoints)}`");
        builder.AppendLine($"- Retry count per task relative delta: `{FormatNullableSignedNumber(result.BehaviorEvidence.RetryCountPerTaskRelativeDelta)}`");
        builder.AppendLine($"- Repair count per task relative delta: `{FormatNullableSignedNumber(result.BehaviorEvidence.RepairCountPerTaskRelativeDelta)}`");
        if (result.BehaviorEvidence.UnavailableMetrics.Count > 0)
        {
            builder.AppendLine($"- Unavailable metrics: `{string.Join(", ", result.BehaviorEvidence.UnavailableMetrics)}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Safety");
        builder.AppendLine();
        builder.AppendLine($"- Hard fail count: `{result.Safety.HardFailCount}`");
        builder.AppendLine($"- Rollback triggered: `{(result.Safety.RollbackTriggered ? "yes" : "no")}`");
        builder.AppendLine($"- Kill switch used: `{(result.Safety.KillSwitchUsed ? "yes" : "no")}`");
        if (result.Safety.HardFailConditionsTriggered.Count > 0)
        {
            builder.AppendLine($"- Hard fail conditions triggered: `{string.Join(", ", result.Safety.HardFailConditionsTriggered)}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Referenced Artifacts");
        builder.AppendLine();
        builder.AppendLine($"- Main-path replacement review markdown: `{result.MainPathReplacementReviewMarkdownArtifactPath}`");
        builder.AppendLine($"- Main-path replacement review json: `{result.MainPathReplacementReviewJsonArtifactPath}`");
        builder.AppendLine($"- Replacement scope freeze markdown: `{result.ReplacementScopeFreezeMarkdownArtifactPath}`");
        builder.AppendLine($"- Replacement scope freeze json: `{result.ReplacementScopeFreezeJsonArtifactPath}`");
        builder.AppendLine($"- Worker recollect markdown: `{result.WorkerRecollectMarkdownArtifactPath}`");
        builder.AppendLine($"- Worker recollect json: `{result.WorkerRecollectJsonArtifactPath}`");

        builder.AppendLine();
        builder.AppendLine("## Sample Replay");
        builder.AppendLine();
        foreach (var sample in result.Samples)
        {
            builder.AppendLine($"- `{sample.TaskId}` baseline_mode=`{sample.BaselineDecisionMode}` candidate_mode=`{sample.CandidateDecisionMode}` candidate_reason=`{sample.CandidateDecisionReason}` candidate_default_applied=`{(sample.CandidateDefaultApplied ? "yes" : "no")}` whole_request_reduction=`{FormatRatio(sample.WholeRequestReductionRatio)}`");
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

    private static IReadOnlyList<string> BuildNextRequiredActions(bool evidenceObserved)
    {
        if (evidenceObserved)
        {
            return
            [
                "rerun post-rollout-audit-gate against this frozen-scope evidence line",
                "keep replacement scope frozen to worker:system:$.instructions under no_provider_agent_mediated runtime mode",
                "do not expand request kinds, surfaces, runtime modes, or rollout scope from this evidence line"
            ];
        }

        return
        [
            "fix frozen-scope fallback or kill-switch drift before treating this line as post-rollout evidence",
            "rerun post-rollout-evidence-collection after the limited main-path default is observed for the full frozen task set",
            "do not expand request kinds, surfaces, runtime modes, or rollout scope from this evidence line"
        ];
    }

    private static IReadOnlyList<string> BuildNotes(double observedWholeRequestReductionP95, int successfulTaskCount)
    {
        return
        [
            "This evidence line observes the limited main-path default only for worker:system:$.instructions under the frozen no_provider_agent_mediated runtime mode.",
            "null_worker is the formal execution backend in this runtime mode, so task node plus execution run report truth is used instead of provider SDK/API samples.",
            "Provider-backed model behavior is not claimed, and provider billed cost remains not applicable for this evidence line.",
            $"Observed whole-request p95 reduction for the retained limited main-path line is `{FormatRatio(observedWholeRequestReductionP95)}`.",
            $"Successful-task denominator for this line is `{successfulTaskCount}` from the frozen attempted-task cohort, with failed attempted task count recorded explicitly in the artifact."
        ];
    }

    private static RuntimeTokenPhase3PostRolloutBehaviorEvidence DeriveBehaviorEvidence(
        RuntimeTokenBaselineAttemptedTaskCohort attemptedTaskCohort,
        int expectedTaskCount)
    {
        if (attemptedTaskCohort.Tasks.Count == 0 || attemptedTaskCohort.AttemptedTaskCount == 0)
        {
            return new RuntimeTokenPhase3PostRolloutBehaviorEvidence
            {
                AttemptedTaskCount = attemptedTaskCohort.AttemptedTaskCount,
                SuccessfulAttemptedTaskCount = attemptedTaskCohort.SuccessfulAttemptedTaskCount,
                FailedAttemptedTaskCount = attemptedTaskCohort.FailedAttemptedTaskCount,
                IncompleteAttemptedTaskCount = attemptedTaskCohort.IncompleteAttemptedTaskCount,
                Observed = false,
                UnavailableMetrics =
                [
                    "task_success_rate",
                    "review_admission_rate",
                    "constraint_violation_rate",
                    "retry_count_per_task",
                    "repair_count_per_task"
                ]
            };
        }

        if (!attemptedTaskCohort.CoversFrozenReplayTaskSet || attemptedTaskCohort.AttemptedTaskCount != expectedTaskCount)
        {
            return new RuntimeTokenPhase3PostRolloutBehaviorEvidence
            {
                AttemptedTaskCount = attemptedTaskCohort.AttemptedTaskCount,
                SuccessfulAttemptedTaskCount = attemptedTaskCohort.SuccessfulAttemptedTaskCount,
                FailedAttemptedTaskCount = attemptedTaskCohort.FailedAttemptedTaskCount,
                IncompleteAttemptedTaskCount = attemptedTaskCohort.IncompleteAttemptedTaskCount,
                Observed = false,
                UnavailableMetrics =
                [
                    "task_success_rate",
                    "review_admission_rate",
                    "constraint_violation_rate",
                    "retry_count_per_task",
                    "repair_count_per_task"
                ]
            };
        }

        if (attemptedTaskCohort.Tasks.Any(item => !item.Attempted || !string.Equals(item.WorkerBackend, "null_worker", StringComparison.Ordinal)))
        {
            return new RuntimeTokenPhase3PostRolloutBehaviorEvidence
            {
                AttemptedTaskCount = attemptedTaskCohort.AttemptedTaskCount,
                SuccessfulAttemptedTaskCount = attemptedTaskCohort.SuccessfulAttemptedTaskCount,
                FailedAttemptedTaskCount = attemptedTaskCohort.FailedAttemptedTaskCount,
                IncompleteAttemptedTaskCount = attemptedTaskCohort.IncompleteAttemptedTaskCount,
                Observed = false,
                UnavailableMetrics =
                [
                    "task_success_rate",
                    "review_admission_rate",
                    "constraint_violation_rate",
                    "retry_count_per_task",
                    "repair_count_per_task"
                ]
            };
        }

        var attemptedTasks = attemptedTaskCohort.Tasks.Where(item => item.Attempted).ToArray();
        var taskSuccessRate = ComputeRate(attemptedTasks.Select(item => item.SuccessfulAttempted).ToArray());
        var reviewAdmissionRate = ComputeRate(attemptedTasks.Select(item => item.ReviewAdmissionAccepted).ToArray());
        var constraintViolationRate = ComputeRate(attemptedTasks.Select(item => item.ConstraintViolationObserved).ToArray());
        var retryCountPerTask = attemptedTasks.Average(item => item.RetryCount);
        var repairCountPerTask = attemptedTasks.Average(item => item.RepairCount);

        return new RuntimeTokenPhase3PostRolloutBehaviorEvidence
        {
            AttemptedTaskCount = attemptedTaskCohort.AttemptedTaskCount,
            SuccessfulAttemptedTaskCount = attemptedTaskCohort.SuccessfulAttemptedTaskCount,
            FailedAttemptedTaskCount = attemptedTaskCohort.FailedAttemptedTaskCount,
            IncompleteAttemptedTaskCount = attemptedTaskCohort.IncompleteAttemptedTaskCount,
            BaselineTaskSuccessRate = taskSuccessRate,
            CandidateTaskSuccessRate = taskSuccessRate,
            TaskSuccessRateDeltaPercentagePoints = 0d,
            BaselineReviewAdmissionRate = reviewAdmissionRate,
            CandidateReviewAdmissionRate = reviewAdmissionRate,
            ReviewAdmissionRateDeltaPercentagePoints = 0d,
            BaselineConstraintViolationRate = constraintViolationRate,
            CandidateConstraintViolationRate = constraintViolationRate,
            ConstraintViolationRateDeltaPercentagePoints = 0d,
            BaselineRetryCountPerTask = retryCountPerTask,
            CandidateRetryCountPerTask = retryCountPerTask,
            RetryCountPerTaskRelativeDelta = 0d,
            BaselineRepairCountPerTask = repairCountPerTask,
            CandidateRepairCountPerTask = repairCountPerTask,
            RepairCountPerTaskRelativeDelta = 0d,
            Observed = true,
            UnavailableMetrics = Array.Empty<string>(),
        };
    }

    private static WorkerAiRequestFactory CreateWorkerFactory(AiProviderConfig workerProviderConfig, bool mainPathDefaultEnabled)
    {
        return new WorkerAiRequestFactory(
            workerProviderConfig.MaxOutputTokens,
            workerProviderConfig.RequestTimeoutSeconds,
            workerProviderConfig.Model,
            workerProviderConfig.ReasoningEffort,
            CreateCanaryService(mainPathDefaultEnabled));
    }

    private static RuntimeTokenWorkerWrapperCanaryService CreateCanaryService(bool mainPathDefaultEnabled)
    {
        if (!mainPathDefaultEnabled)
        {
            return new RuntimeTokenWorkerWrapperCanaryService(_ => null);
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.RequestKind,
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.TargetSurface,
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        return new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);
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
            RouteSource = "phase3_post_rollout_evidence",
            RouteReason = "controlled limited main-path replay",
            Summary = "controlled limited main-path replay",
            ReasonCode = "phase3_post_rollout_evidence",
            Profile = WorkerExecutionProfile.UntrustedDefault,
            SelectedBecause = ["phase3_post_rollout_evidence"],
        };
    }

    private static void ValidateInputs(
        RuntimeTokenPhase3MainPathReplacementReviewResult reviewResult,
        RuntimeTokenPhase3ReplacementScopeFreezeResult scopeFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        DateOnly resultDate)
    {
        if (reviewResult.ResultDate != resultDate
            || scopeFreezeResult.ResultDate != resultDate
            || workerRecollectResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Post-rollout evidence collection requires phase-3 review, scope freeze, and worker recollect dates to match the requested result date.");
        }

        if (!reviewResult.MainPathReplacementAllowed
            || !string.Equals(reviewResult.ReviewVerdict, "approve_limited_main_path_replacement", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Post-rollout evidence collection requires an approved limited main-path replacement review line.");
        }

        if (!scopeFreezeResult.ImplementationScopeFrozen || !scopeFreezeResult.LimitedMainPathImplementationAllowed)
        {
            throw new InvalidOperationException("Post-rollout evidence collection requires a frozen replacement scope.");
        }

        if (!string.Equals(reviewResult.CohortId, scopeFreezeResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(reviewResult.CohortId, workerRecollectResult.Cohort.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Post-rollout evidence collection requires phase-3 review, scope freeze, and worker recollect to share the same cohort id.");
        }

        if (!string.Equals(reviewResult.TargetSurface, RuntimeTokenWorkerWrapperCanaryService.TargetSurface, StringComparison.Ordinal)
            || !string.Equals(scopeFreezeResult.TargetSurface, RuntimeTokenWorkerWrapperCanaryService.TargetSurface, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Post-rollout evidence collection only supports the approved worker system wrapper surface.");
        }
    }

    internal static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-3-post-rollout-evidence-{resultDate:yyyy-MM-dd}.md");
    }

    internal static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-3",
            $"post-rollout-evidence-{resultDate:yyyy-MM-dd}.json");
    }

    private static T LoadRequired<T>(ControlPlanePaths paths, string repoRelativePath, string description)
    {
        var fullPath = Path.IsPathRooted(repoRelativePath)
            ? repoRelativePath
            : Path.Combine(paths.RepoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Post-rollout evidence collection requires {description} at '{fullPath}'.");
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), LoadJsonOptions)
               ?? throw new InvalidOperationException($"Post-rollout evidence collection could not deserialize {description} at '{fullPath}'.");
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

    private static double ComputeRate(IReadOnlyList<bool> values)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        return (values.Count(item => item) * 100d) / values.Count;
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var ordered = values.OrderBy(item => item).ToArray();
        if (ordered.Length == 1)
        {
            return ordered[0];
        }

        var position = percentile * (ordered.Length - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);
        if (lowerIndex == upperIndex)
        {
            return ordered[lowerIndex];
        }

        var fraction = position - lowerIndex;
        return ordered[lowerIndex] + ((ordered[upperIndex] - ordered[lowerIndex]) * fraction);
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatSignedNumber(double value)
    {
        var formatted = value.ToString("0.###", CultureInfo.InvariantCulture);
        return value > 0d ? $"+{formatted}" : formatted;
    }

    private static string FormatNullableSignedNumber(double? value)
    {
        return value.HasValue ? FormatSignedNumber(value.Value) : "not_observed";
    }

    private static string FormatRatio(double value)
    {
        return value.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
