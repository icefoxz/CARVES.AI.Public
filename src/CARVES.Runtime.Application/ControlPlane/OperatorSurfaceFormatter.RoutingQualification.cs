using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RoutingValidationSuite(RoutingValidationCatalog catalog, RoutingValidationSummary? latestSummary)
    {
        var lines = new List<string>
        {
            $"Validation catalog: {catalog.CatalogId}",
            $"Schema: {catalog.SchemaVersion}",
            $"Created at: {catalog.CreatedAt:O}",
            $"Tasks: {catalog.Tasks.Length}",
        };
        lines.AddRange(catalog.Tasks.Length == 0
            ? ["(none)"]
            : catalog.Tasks.Select(task =>
                $"- {task.TaskId}: type={task.TaskType}; intent={task.RoutingIntent}; module={task.ModuleId ?? "(none)"}; baseline={task.BaselineLaneId}; format={task.ExpectedFormat}; risk={task.RiskLevel}"));

        if (latestSummary is null)
        {
            lines.Add("Latest summary: (none)");
        }
        else
        {
            lines.Add($"Latest summary: {latestSummary.RunId}");
            lines.Add($"- mode={latestSummary.ExecutionMode}");
            lines.Add($"- routing_profile={latestSummary.RoutingProfileId ?? "(none)"}");
            lines.Add($"- success_rate={latestSummary.SuccessRate:P0}");
            lines.Add($"- schema_validity_rate={latestSummary.SchemaValidityRate:P0}");
            lines.Add($"- fallback_rate={latestSummary.FallbackRate:P0}");
            lines.Add($"- build_pass_rate={latestSummary.BuildPassRate:P0}");
            lines.Add($"- test_pass_rate={latestSummary.TestPassRate:P0}");
            lines.Add($"- safety_pass_rate={latestSummary.SafetyPassRate:P0}");
            lines.Add($"- average_latency_ms={latestSummary.AverageLatencyMs:F0}");
            lines.Add($"- total_estimated_cost_usd={latestSummary.TotalEstimatedCostUsd:F6}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RoutingValidationTrace(RoutingValidationTrace? trace)
    {
        if (trace is null)
        {
            return new OperatorCommandResult(1, ["Validation trace: (not found)"]);
        }

        var lines = new List<string>
        {
            $"Validation trace: {trace.TraceId}",
            $"Run: {trace.RunId}",
            $"Task: {trace.TaskId}",
            $"Type: {trace.TaskType}",
            $"Intent: {trace.RoutingIntent}",
            $"Module: {trace.ModuleId ?? "(none)"}",
            $"Mode: {trace.ExecutionMode}",
            $"Routing profile: {trace.RoutingProfileId ?? "(none)"}",
            $"Route source: {trace.RouteSource}",
            $"Selected provider: {trace.SelectedProvider ?? "(none)"}",
            $"Selected lane: {trace.SelectedLane ?? "(none)"}",
            $"Selected backend: {trace.SelectedBackend ?? "(none)"}",
            $"Selected model: {trace.SelectedModel ?? "(none)"}",
            $"Selected routing profile: {trace.SelectedRoutingProfileId ?? "(none)"}",
            $"Applied rule: {trace.AppliedRoutingRuleId ?? "(none)"}",
            $"Codex thread id: {trace.CodexThreadId ?? "(none)"}",
            $"Codex thread continuity: {trace.CodexThreadContinuity}",
            $"Fallback configured: {trace.FallbackConfigured}",
            $"Fallback triggered: {trace.FallbackTriggered}",
            $"Preferred eligibility: {trace.PreferredRouteEligibility?.ToString() ?? "(none)"}",
            $"Preferred ineligibility reason: {trace.PreferredIneligibilityReason ?? "(none)"}",
            $"Request succeeded: {trace.RequestSucceeded}",
            $"Task succeeded: {trace.TaskSucceeded}",
            $"Schema valid: {trace.SchemaValid}",
            $"Build outcome: {trace.BuildOutcome}",
            $"Test outcome: {trace.TestOutcome}",
            $"Safety outcome: {trace.SafetyOutcome}",
            $"Retry count: {trace.RetryCount}",
            $"Patch accepted: {trace.PatchAccepted}",
            $"Latency ms: {trace.LatencyMs}",
            $"Prompt tokens: {trace.PromptTokens?.ToString() ?? "(none)"}",
            $"Completion tokens: {trace.CompletionTokens?.ToString() ?? "(none)"}",
            $"Estimated cost usd: {trace.EstimatedCostUsd?.ToString("F6") ?? "(none)"}",
            $"Failure category: {trace.FailureCategory ?? "(none)"}",
        };

        lines.Add("Selected because:");
        lines.AddRange(trace.SelectedBecause.Length == 0
            ? ["(none)"]
            : trace.SelectedBecause.Select(reason => $"- {reason}"));

        lines.Add("Reason codes:");
        lines.AddRange(trace.ReasonCodes.Length == 0
            ? ["(none)"]
            : trace.ReasonCodes.Select(reason => $"- {reason}"));

        lines.Add("Artifacts:");
        lines.AddRange(trace.ArtifactPaths.Length == 0
            ? ["(none)"]
            : trace.ArtifactPaths.Select(path => $"- {path}"));

        lines.Add("Candidates:");
        lines.AddRange(trace.Candidates.Length == 0
            ? ["(none)"]
            : trace.Candidates.Select(candidate =>
                $"- {candidate.BackendId}: selected={candidate.Selected}; provider={candidate.ProviderId}; routing={candidate.RoutingProfileId ?? "(none)"}; rule={candidate.RoutingRuleId ?? "(none)"}; route={candidate.RouteDisposition}; eligibility={candidate.Eligibility}; quota={candidate.Signals.QuotaState}; token_fit={candidate.Signals.TokenBudgetFit}; latency_ms={candidate.Signals.RecentLatencyMs?.ToString() ?? "(none)"}; failures={candidate.Signals.RecentFailureCount}; {candidate.Reason}"));

        return new OperatorCommandResult(trace.TaskSucceeded ? 0 : 1, lines);
    }

    public static OperatorCommandResult RoutingValidationSummary(RoutingValidationSummary? summary)
    {
        if (summary is null)
        {
            return new OperatorCommandResult(1, ["Validation summary: (none)"]);
        }

        var lines = new List<string>
        {
            $"Validation summary: {summary.SummaryId}",
            $"Run: {summary.RunId}",
            $"Mode: {summary.ExecutionMode}",
            $"Routing profile: {summary.RoutingProfileId ?? "(none)"}",
            $"Tasks: {summary.Tasks}",
            $"Success rate: {summary.SuccessRate:P0}",
            $"Schema validity rate: {summary.SchemaValidityRate:P0}",
            $"Fallback rate: {summary.FallbackRate:P0}",
            $"Build pass rate: {summary.BuildPassRate:P0}",
            $"Test pass rate: {summary.TestPassRate:P0}",
            $"Safety pass rate: {summary.SafetyPassRate:P0}",
            $"Average latency ms: {summary.AverageLatencyMs:F0}",
            $"Total estimated cost usd: {summary.TotalEstimatedCostUsd:F6}",
        };
        lines.Add("Route breakdown:");
        lines.AddRange(summary.RouteBreakdown.Length == 0
            ? ["(none)"]
            : summary.RouteBreakdown.Select(item =>
                $"- {item.TaskFamily} -> {item.ProviderId}/{item.BackendId}/{item.SelectedLane ?? "(none)"}/{item.SelectedModel ?? "(none)"}; samples={item.Samples}; success={item.SuccessRate:P0}; patch_accept={item.PatchAcceptanceRate:P0}; avg_retry={item.AverageRetryCount:F1}; avg_latency_ms={item.AverageLatencyMs:F0}"));
        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RoutingValidationHistory(RoutingValidationHistory history)
    {
        var lines = new List<string>
        {
            $"Validation history: {history.HistoryId}",
            $"Generated at: {history.GeneratedAt:O}",
            $"Batch count: {history.BatchCount}",
            $"Latest run: {history.LatestRunId ?? "(none)"}",
        };
        lines.Add("Batches:");
        lines.AddRange(history.Batches.Length == 0
            ? ["(none)"]
            : history.Batches.Select(batch =>
                $"- {batch.RunId}: mode={batch.ExecutionMode}; routing_profile={batch.RoutingProfileId ?? "(none)"}; tasks={batch.Tasks}; success={batch.SuccessRate:P0}; schema={batch.SchemaValidityRate:P0}; fallback={batch.FallbackRate:P0}; latency_ms={batch.AverageLatencyMs:F0}; cost_usd={batch.TotalEstimatedCostUsd:F6}"));
        if (history.Batches.Length > 0)
        {
            lines.Add("Latest route breakdown:");
            lines.AddRange(history.Batches[0].RouteBreakdown.Length == 0
                ? ["(none)"]
                : history.Batches[0].RouteBreakdown.Select(item =>
                    $"- {item.TaskFamily} -> {item.ProviderId}/{item.BackendId}/{item.SelectedLane ?? "(none)"}/{item.SelectedModel ?? "(none)"}; samples={item.Samples}; success={item.SuccessRate:P0}; patch_accept={item.PatchAcceptanceRate:P0}; avg_retry={item.AverageRetryCount:F1}; avg_latency_ms={item.AverageLatencyMs:F0}"));
        }
        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult ValidationCoverageMatrix(ValidationCoverageMatrix matrix)
    {
        var lines = new List<string>
        {
            $"Validation coverage matrix: {matrix.MatrixId}",
            $"Candidate: {matrix.CandidateId}",
            $"Profile id: {matrix.ProfileId}",
            $"Generated at: {matrix.GeneratedAt:O}",
            $"Validation batches: {matrix.ValidationBatchCount}",
            $"Families: {matrix.Families.Length}",
        };
        lines.Add("Families:");
        lines.AddRange(matrix.Families.Length == 0
            ? ["(none)"]
            : matrix.Families.Select(family =>
                $"- {family.TaskFamily}: intent={family.RoutingIntent}; module={family.ModuleId ?? "(none)"}; baseline={family.BaselineTraceCount}; routing={family.RoutingTraceCount}; fallback={(family.FallbackRequired ? family.FallbackTraceCount.ToString() : "n/a")}; missing={(family.MissingEvidence.Length == 0 ? "(none)" : string.Join(", ", family.MissingEvidence.Select(gap => gap.ReasonCode)))}"));
        lines.Add("Missing evidence:");
        lines.AddRange(matrix.MissingEvidence.Length == 0
            ? ["(none)"]
            : matrix.MissingEvidence.Select(gap =>
                $"- {gap.RequiredMode}: family={gap.TaskFamily}; intent={gap.RoutingIntent}; module={gap.ModuleId ?? "(none)"}; reason_code={gap.ReasonCode}; tasks={(gap.TaskIds.Length == 0 ? "(none)" : string.Join(", ", gap.TaskIds))}; {gap.Summary}"));
        return OperatorCommandResult.Success(lines.ToArray());
    }
}
