using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenPhase2NonInferiorityCohortFreezeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly RuntimeTokenPhase2NonInferiorityThreshold[] Thresholds =
    [
        new()
        {
            MetricId = "task_success_rate",
            ThresholdKind = "regression_pp_max",
            Comparator = "<=",
            ThresholdValue = 2.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "review_admission_rate",
            ThresholdKind = "regression_pp_max",
            Comparator = "<=",
            ThresholdValue = 2.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "constraint_violation_rate",
            ThresholdKind = "increase_pp_max",
            Comparator = "<=",
            ThresholdValue = 1.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "retry_count_per_task",
            ThresholdKind = "relative_increase_max",
            Comparator = "<=",
            ThresholdValue = 0.20,
            Units = "ratio",
        },
        new()
        {
            MetricId = "repair_count_per_task",
            ThresholdKind = "relative_increase_max",
            Comparator = "<=",
            ThresholdValue = 0.20,
            Units = "ratio",
        },
        new()
        {
            MetricId = "provider_context_cap_hit_rate",
            ThresholdKind = "increase_pp_max",
            Comparator = "<=",
            ThresholdValue = 1.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "internal_prompt_budget_cap_hit_rate",
            ThresholdKind = "increase_pp_max",
            Comparator = "<=",
            ThresholdValue = 1.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "section_budget_cap_hit_rate",
            ThresholdKind = "increase_pp_max",
            Comparator = "<=",
            ThresholdValue = 1.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "trim_loop_cap_hit_rate",
            ThresholdKind = "increase_pp_max",
            Comparator = "<=",
            ThresholdValue = 1.0,
            Units = "percentage_points",
        },
        new()
        {
            MetricId = "total_tokens_per_successful_task",
            ThresholdKind = "no_regression_required",
            Comparator = "<=",
            ThresholdValue = 0.0,
            Units = "relative_change",
        },
    ];

    private static readonly string[] SuccessCriteria =
    [
        "same worker-only task set, provider, model, tokenizer, and token accounting mode",
        "same request-kind mix and same tool availability for worker execution",
        "same retrieval snapshot through frozen worker recollect context-pack artifacts",
        "same manual-review protocol and pre-registered non-inferiority thresholds",
    ];

    private static readonly string[] HardFailConditions =
    [
        "hard_fail_count_gt_0",
        "policy_invariant_coverage_below_100pct",
        "semantic_preservation_fail_count_gt_0",
        "salience_preservation_fail_count_gt_0",
        "priority_preservation_fail_count_gt_0",
        "request_kind_slice_removed_policy_critical_fragment_gt_0",
    ];

    private readonly ControlPlanePaths paths;

    public RuntimeTokenPhase2NonInferiorityCohortFreezeService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenPhase2NonInferiorityCohortFreezeResult Persist(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> attributionRecords)
    {
        return Persist(
            paths,
            candidateResult,
            requestKindSliceProofResult,
            rollbackPlanFreezeResult,
            workerRecollectResult,
            trustLineResult,
            attributionRecords,
            candidateResult.ResultDate);
    }

    internal static RuntimeTokenPhase2NonInferiorityCohortFreezeResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> attributionRecords,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(candidateResult, requestKindSliceProofResult, rollbackPlanFreezeResult, workerRecollectResult, trustLineResult, attributionRecords, resultDate);

        var requestKindMix = workerRecollectResult.Cohort.RequestKinds
            .Select(requestKind =>
            {
                var count = attributionRecords.Count(item => string.Equals(item.RequestKind, requestKind, StringComparison.Ordinal));
                var ratio = attributionRecords.Count == 0 ? 0d : (double)count / attributionRecords.Count;
                return new RuntimeTokenPhase2RequestKindMixEntry
                {
                    RequestKind = requestKind,
                    RequestCount = count,
                    RequestRatio = ratio,
                };
            })
            .OrderBy(item => item.RequestKind, StringComparer.Ordinal)
            .ToArray();

        var provider = SelectSingle(attributionRecords.Select(item => item.Provider), "provider");
        var providerApiVersion = SelectSingle(attributionRecords.Select(item => item.ProviderApiVersion), "provider_api_version");
        var model = SelectSingle(attributionRecords.Select(item => item.Model), "model");
        var tokenizer = SelectSingle(attributionRecords.Select(item => item.Tokenizer), "tokenizer");

        var blockingReasons = new List<string>();
        if (!requestKindSliceProofResult.CrossKindProofAvailable)
        {
            blockingReasons.Add("request_kind_slice_cross_kind_proof_not_available");
        }

        if (!rollbackPlanFreezeResult.RollbackPlanReviewed)
        {
            blockingReasons.Add("rollback_plan_not_reviewed");
        }

        if (!string.Equals(trustLineResult.TrustLineClassification, "recomputed_trusted_for_phase_1_target_decision", StringComparison.Ordinal))
        {
            blockingReasons.Add("trusted_baseline_line_not_available");
        }

        if (!workerRecollectResult.AttemptedTaskCohort.CoversFrozenReplayTaskSet
            || workerRecollectResult.AttemptedTaskCohort.AttemptedTaskCount == 0)
        {
            blockingReasons.Add("attempted_task_cohort_not_frozen");
        }

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenPhase2NonInferiorityCohortFreezeResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = candidateResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            CandidateMarkdownArtifactPath = candidateResult.MarkdownArtifactPath,
            CandidateJsonArtifactPath = candidateResult.JsonArtifactPath,
            RequestKindSliceProofMarkdownArtifactPath = requestKindSliceProofResult.MarkdownArtifactPath,
            RequestKindSliceProofJsonArtifactPath = requestKindSliceProofResult.JsonArtifactPath,
            RollbackPlanMarkdownArtifactPath = rollbackPlanFreezeResult.MarkdownArtifactPath,
            RollbackPlanJsonArtifactPath = rollbackPlanFreezeResult.JsonArtifactPath,
            WorkerRecollectMarkdownArtifactPath = workerRecollectResult.MarkdownArtifactPath,
            WorkerRecollectJsonArtifactPath = workerRecollectResult.JsonArtifactPath,
            TrustMarkdownArtifactPath = trustLineResult.EvidenceMarkdownArtifactPath,
            TrustJsonArtifactPath = trustLineResult.EvidenceJsonArtifactPath,
            TrustLineClassification = trustLineResult.TrustLineClassification,
            TargetSurface = candidateResult.CandidateSurfaceId,
            CandidateStrategy = candidateResult.CandidateStrategy,
            NonInferiorityCohortFrozen = blockingReasons.Count == 0,
            TaskIds = workerRecollectResult.TaskIds,
            AttemptedTaskCohort = workerRecollectResult.AttemptedTaskCohort,
            RequestKindMix = requestKindMix,
            Provider = provider,
            ProviderApiVersion = providerApiVersion,
            Model = model,
            Tokenizer = tokenizer,
            TokenAccountingSourcePolicy = workerRecollectResult.Cohort.TokenAccountingSourcePolicy,
            ContextWindowView = workerRecollectResult.Cohort.ContextWindowView,
            BillableCostView = workerRecollectResult.Cohort.BillableCostView,
            ToolAvailability =
            [
                SelectSingle(workerRecollectResult.Tasks.Select(item => item.Consumer), "consumer"),
                "windows_powershell",
                "apply_patch",
                "rg",
            ],
            RetrievalSnapshot = string.Join(", ", workerRecollectResult.Tasks.Select(item => item.ContextPackArtifactPath).OrderBy(item => item, StringComparer.Ordinal)),
            SuccessCriteria = SuccessCriteria,
            HardFailConditions = HardFailConditions,
            MetricThresholds = Thresholds,
            LowBaseCountRule = "if baseline event count < 20, require manual review instead of pure percentage-threshold evaluation",
            ManualReviewProtocol = "worker-only canary evaluation requires operator review for any low-base metric, any hard-fail signal, and any policy-invariant drift",
            BlockingReasons = blockingReasons
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray(),
            Notes = BuildNotes(workerRecollectResult, requestKindSliceProofResult, rollbackPlanFreezeResult),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenPhase2NonInferiorityCohortFreezeResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 2 Non-inferiority Cohort");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Target surface: `{result.TargetSurface}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Non-inferiority cohort frozen: `{(result.NonInferiorityCohortFrozen ? "yes" : "no")}`");
        builder.AppendLine($"- Provider/model/tokenizer: `{result.Provider}` / `{result.Model}` / `{result.Tokenizer}`");
        builder.AppendLine($"- Provider API version: `{result.ProviderApiVersion}`");
        builder.AppendLine($"- Token accounting source policy: `{result.TokenAccountingSourcePolicy}`");
        builder.AppendLine($"- Context window view: `{result.ContextWindowView}`");
        builder.AppendLine($"- Billable cost view: `{result.BillableCostView}`");
        builder.AppendLine();

        builder.AppendLine("## Task Set");
        builder.AppendLine();
        foreach (var taskId in result.TaskIds)
        {
            builder.AppendLine($"- `{taskId}`");
        }

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
        builder.AppendLine("## Request-kind Mix");
        builder.AppendLine();
        foreach (var entry in result.RequestKindMix)
        {
            builder.AppendLine($"- `{entry.RequestKind}`: count=`{entry.RequestCount}`, ratio=`{entry.RequestRatio:0.000}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Tool Availability");
        builder.AppendLine();
        foreach (var item in result.ToolAvailability)
        {
            builder.AppendLine($"- `{item}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Success Criteria");
        builder.AppendLine();
        foreach (var item in result.SuccessCriteria)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
        builder.AppendLine("## Hard Fail Conditions");
        builder.AppendLine();
        foreach (var item in result.HardFailConditions)
        {
            builder.AppendLine($"- `{item}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Metric Thresholds");
        builder.AppendLine();
        foreach (var threshold in result.MetricThresholds)
        {
            builder.AppendLine($"- `{threshold.MetricId}`: `{threshold.ThresholdKind}` `{threshold.Comparator}` `{threshold.ThresholdValue:0.###}` `{threshold.Units}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Review Protocol");
        builder.AppendLine();
        builder.AppendLine($"- Low-base-count rule: {result.LowBaseCountRule}");
        builder.AppendLine($"- Manual review protocol: {result.ManualReviewProtocol}");
        builder.AppendLine($"- Retrieval snapshot: `{result.RetrievalSnapshot}`");

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
        builder.AppendLine("## Notes");
        builder.AppendLine();
        foreach (var note in result.Notes)
        {
            builder.AppendLine($"- {note}");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult)
    {
        return
        [
            $"This cohort freezes `{workerRecollectResult.TaskIds.Count}` worker task(s) from the trusted worker recollect line.",
            $"Attempted-task cohort for this line is `{workerRecollectResult.AttemptedTaskCohort.SelectionMode}` with attempted=`{workerRecollectResult.AttemptedTaskCohort.AttemptedTaskCount}`, successful=`{workerRecollectResult.AttemptedTaskCohort.SuccessfulAttemptedTaskCount}`, failed=`{workerRecollectResult.AttemptedTaskCohort.FailedAttemptedTaskCount}`, incomplete=`{workerRecollectResult.AttemptedTaskCohort.IncompleteAttemptedTaskCount}`.",
            $"Canary scope remains `{string.Join(", ", requestKindSliceProofResult.CanaryRequestKindAllowlist)}` only; non-worker request kinds remain out of scope.",
            $"Rollback fallback version remains `{rollbackPlanFreezeResult.FallbackVersion}` with default-off posture.",
            "This artifact freezes evaluation inputs only. It does not approve runtime shadow, active canary, or main-path replacement."
        ];
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperCandidateResult candidateResult,
        RuntimeTokenPhase2RequestKindSliceProofResult requestKindSliceProofResult,
        RuntimeTokenPhase2RollbackPlanFreezeResult rollbackPlanFreezeResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        RuntimeTokenBaselineTrustLineResult trustLineResult,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> attributionRecords,
        DateOnly resultDate)
    {
        if (candidateResult.ResultDate != resultDate
            || requestKindSliceProofResult.ResultDate != resultDate
            || rollbackPlanFreezeResult.ResultDate != resultDate
            || workerRecollectResult.ResultDate != resultDate
            || trustLineResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Non-inferiority cohort freeze requires all inputs to share the same result date.");
        }

        if (!string.Equals(candidateResult.CohortId, requestKindSliceProofResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(candidateResult.CohortId, rollbackPlanFreezeResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(candidateResult.CohortId, workerRecollectResult.Cohort.CohortId, StringComparison.Ordinal)
            || !string.Equals(candidateResult.CohortId, trustLineResult.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Non-inferiority cohort freeze requires all inputs to point at the same frozen cohort.");
        }

        if (!string.Equals(candidateResult.CandidateSurfaceId, requestKindSliceProofResult.TargetSurface, StringComparison.Ordinal)
            || !string.Equals(candidateResult.CandidateSurfaceId, rollbackPlanFreezeResult.TargetSurface, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Non-inferiority cohort freeze requires proof and rollback plan to point at the same wrapper surface.");
        }

        if (attributionRecords.Count == 0)
        {
            throw new InvalidOperationException("Non-inferiority cohort freeze requires at least one attribution record.");
        }

        if (!workerRecollectResult.AttributionIds.All(attributionId => attributionRecords.Any(item => string.Equals(item.AttributionId, attributionId, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException("Non-inferiority cohort freeze requires attribution records for every worker recollect attribution id.");
        }
    }

    private static string SelectSingle(IEnumerable<string?> values, string label)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return distinct.Length switch
        {
            1 => distinct[0],
            0 => throw new InvalidOperationException($"Non-inferiority cohort freeze requires one non-empty `{label}` value."),
            _ => throw new InvalidOperationException($"Non-inferiority cohort freeze requires a single `{label}` across the frozen cohort."),
        };
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-2-non-inferiority-cohort-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-2",
            $"non-inferiority-cohort-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }
}
