using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenOutcomeBinderService
{
    private static readonly HashSet<string> IncludedByDefaultRequestKinds = new(StringComparer.Ordinal)
    {
        "worker",
        "planner",
        "decomposer",
        "reviewer",
        "repair",
        "retry",
        "failure_recovery",
    };

    private static readonly Dictionary<string, string> ConditionallyIncludedRequestKinds = new(StringComparer.Ordinal)
    {
        ["bootstrap"] = "include_if_run_scoped_and_required_for_task_execution",
        ["operator_readback"] = "include_if_part_of_agentic_task_loop_and_reinjected_to_llm_for_task_execution_with_run_and_task_binding",
    };

    private static readonly HashSet<string> ExcludedByDefaultRequestKinds = new(StringComparer.Ordinal)
    {
        "manual_operator_inspect_only",
        "offline_shadow_analysis_only",
    };

    private readonly LlmRequestEnvelopeAttributionService attributionService;
    private readonly ITaskGraphRepository taskGraphRepository;
    private readonly ExecutionRunReportService runReportService;

    public RuntimeTokenOutcomeBinderService(
        LlmRequestEnvelopeAttributionService attributionService,
        ITaskGraphRepository taskGraphRepository,
        ExecutionRunReportService runReportService)
    {
        this.attributionService = attributionService;
        this.taskGraphRepository = taskGraphRepository;
        this.runReportService = runReportService;
    }

    public RuntimeTokenOutcomeBinding Bind(RuntimeTokenBaselineCohortFreeze cohort)
    {
        var filteredRecords = RuntimeTokenBaselineAggregatorService.FilterRecords(cohort, attributionService.ListAll());
        var taskGraph = taskGraphRepository.Load();
        var reportsByTaskId = filteredRecords
            .Select(record => record.TaskId)
            .Where(taskId => !string.IsNullOrWhiteSpace(taskId))
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                taskId => taskId!,
                taskId => runReportService.ListReports(taskId!),
                StringComparer.Ordinal);

        return Bind(cohort, filteredRecords, taskGraph, reportsByTaskId);
    }

    internal static RuntimeTokenOutcomeBinding Bind(
        RuntimeTokenBaselineCohortFreeze cohort,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> filteredRecords,
        DomainTaskGraph taskGraph,
        IReadOnlyDictionary<string, IReadOnlyList<ExecutionRunReport>> reportsByTaskId)
    {
        RuntimeTokenBaselineAggregatorService.Validate(cohort);

        var childRequestsByParentId = filteredRecords
            .Where(record => !string.IsNullOrWhiteSpace(record.ParentRequestId))
            .GroupBy(record => record.ParentRequestId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LlmRequestEnvelopeTelemetryRecord>)group.ToArray(), StringComparer.Ordinal);

        var requestDecisions = new List<RequestDecision>(filteredRecords.Count);
        var dispositionEntries = new List<RequestDispositionEntry>(filteredRecords.Count);
        var includedRecords = new List<LlmRequestEnvelopeTelemetryRecord>(filteredRecords.Count);
        var boundRecords = new List<LlmRequestEnvelopeTelemetryRecord>(filteredRecords.Count);
        var bindingGaps = new List<BindingGapEntry>();
        var operatorReadbackInclusions = new List<RuntimeTokenOperatorReadbackInclusionRecord>();
        var includedRequestsWithRunId = 0;
        var includedRequestsWithMatchingRunReport = 0;
        var includedRequestsMissingMatchingRunReport = 0;

        foreach (var record in filteredRecords)
        {
            var decision = EvaluateInclusion(record, childRequestsByParentId, reportsByTaskId);
            requestDecisions.Add(decision);
            if (string.Equals(record.RequestKind, "operator_readback", StringComparison.Ordinal))
            {
                operatorReadbackInclusions.Add(new RuntimeTokenOperatorReadbackInclusionRecord
                {
                    RequestId = record.RequestId,
                    RunId = record.RunId,
                    TaskId = record.TaskId,
                    ParentRequestId = record.ParentRequestId,
                    Included = decision.Included,
                    ReinjectionEvidenceType = decision.ReinjectionEvidenceType,
                    ReinjectionRequestId = decision.ReinjectionRequestId,
                    LlmReinjectionCount = decision.LlmReinjectionCount,
                    ExclusionReason = decision.ExclusionReason,
                });
            }
            if (!decision.Included)
            {
                dispositionEntries.Add(new RequestDispositionEntry(
                    "excluded_by_policy",
                    Mandatory: false,
                    record));
                continue;
            }

            includedRecords.Add(record);
            var mandatoryIfIncluded = IsMandatoryIfIncluded(record.RequestKind);

            if (!string.IsNullOrWhiteSpace(record.RunId))
            {
                includedRequestsWithRunId += 1;
            }

            if (string.IsNullOrWhiteSpace(record.TaskId))
            {
                bindingGaps.Add(new BindingGapEntry("missing_task_id", mandatoryIfIncluded, record));
                dispositionEntries.Add(new RequestDispositionEntry(
                    mandatoryIfIncluded ? "unbound_included_mandatory" : "unbound_included_optional",
                    mandatoryIfIncluded,
                    record));
                continue;
            }

            if (!taskGraph.Tasks.ContainsKey(record.TaskId))
            {
                bindingGaps.Add(new BindingGapEntry("task_not_found", mandatoryIfIncluded, record));
                dispositionEntries.Add(new RequestDispositionEntry(
                    mandatoryIfIncluded ? "unbound_included_mandatory" : "unbound_included_optional",
                    mandatoryIfIncluded,
                    record));
                continue;
            }

            boundRecords.Add(record);
            var task = taskGraph.Tasks[record.TaskId];
            dispositionEntries.Add(new RequestDispositionEntry(
                ResolveBoundDisposition(task.Status),
                mandatoryIfIncluded,
                record));

            if (!string.IsNullOrWhiteSpace(record.RunId))
            {
                var hasMatchingRunReport = reportsByTaskId.TryGetValue(record.TaskId, out var reports)
                                          && reports.Any(report => string.Equals(report.RunId, record.RunId, StringComparison.Ordinal));
                if (hasMatchingRunReport)
                {
                    includedRequestsWithMatchingRunReport += 1;
                }
                else
                {
                    includedRequestsMissingMatchingRunReport += 1;
                }
            }
        }

        var boundTaskGroups = boundRecords
            .GroupBy(record => record.TaskId!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        var taskCosts = boundTaskGroups
            .Select(group => BuildTaskCostRecord(group.Key, group.ToArray(), taskGraph.Tasks[group.Key], reportsByTaskId))
            .ToArray();

        var successfulTaskCount = taskCosts.Count(item => item.Successful);
        var attemptedTaskCount = taskCosts.Length;
        var taskOutcomeBreakdown = taskCosts
            .GroupBy(item => item.TaskStatus)
            .OrderBy(group => group.Key)
            .Select(group => new RuntimeTokenTaskOutcomeBreakdown
            {
                TaskStatus = group.Key,
                Successful = IsSuccessful(group.Key),
                TaskCount = group.Count(),
            })
            .ToArray();

        var requestKindInclusion = requestDecisions
            .GroupBy(item => item.RequestKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RuntimeTokenRequestKindInclusionSummary
            {
                RequestKind = group.Key,
                Policy = group.First().Policy,
                IncludedRequestCount = group.Count(item => item.Included),
                ExcludedRequestCount = group.Count(item => !item.Included),
                ExclusionReasons = group
                    .Where(item => !item.Included && !string.IsNullOrWhiteSpace(item.ExclusionReason))
                    .GroupBy(item => item.ExclusionReason!, StringComparer.Ordinal)
                    .OrderBy(reason => reason.Key, StringComparer.Ordinal)
                    .Select(reason => new RuntimeTokenCountBreakdown
                    {
                        Key = reason.Key,
                        Count = reason.Count(),
                    })
                    .ToArray(),
            })
            .ToArray();

        var taskCostViewBlockingReasons = BuildTaskCostViewBlockingReasons(successfulTaskCount, bindingGaps);
        var taskCostViewTrusted = taskCostViewBlockingReasons.Count == 0;
        var contextWindowView = BuildViewSummary(
            viewId: cohort.ContextWindowView,
            includedRecords,
            attemptedTaskCount,
            successfulTaskCount,
            RuntimeTokenBaselineAggregatorService.ResolveContextWindowTokens,
            ResolveTotalContextTokens,
            taskCostViewTrusted);
        var billableCostView = BuildViewSummary(
            viewId: cohort.BillableCostView,
            includedRecords,
            attemptedTaskCount,
            successfulTaskCount,
            RuntimeTokenBaselineAggregatorService.ResolveBillableTokens,
            ResolveTotalBillableTokens,
            taskCostViewTrusted);

        return new RuntimeTokenOutcomeBinding
        {
            Cohort = cohort,
            TaskCostScope = new RuntimeTokenTaskCostScopeSummary
            {
                IncludedByDefault = IncludedByDefaultRequestKinds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                ConditionallyIncluded = new Dictionary<string, string>(ConditionallyIncludedRequestKinds, StringComparer.Ordinal),
                ExcludedByDefault = ExcludedByDefaultRequestKinds.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
            },
            IncludedRequestCount = requestDecisions.Count(item => item.Included),
            ExcludedRequestCount = requestDecisions.Count(item => !item.Included),
            UnboundIncludedRequestCount = bindingGaps.Count,
            UnboundIncludedMandatoryRequestCount = bindingGaps.Count(item => item.Mandatory),
            UnboundIncludedOptionalRequestCount = bindingGaps.Count(item => !item.Mandatory),
            UnboundIncludedContextTokens = bindingGaps.Sum(item => (long)ResolveTotalContextTokens(item.Record)),
            UnboundIncludedBillableTokens = bindingGaps.Sum(item => (long)ResolveTotalBillableTokens(item.Record)),
            AttemptedTaskCount = attemptedTaskCount,
            SuccessfulTaskCount = successfulTaskCount,
            TaskCostViewTrusted = taskCostViewTrusted,
            TaskCostViewBlockingReasons = taskCostViewBlockingReasons,
            TaskOutcomeBreakdown = taskOutcomeBreakdown,
            RequestKindInclusion = requestKindInclusion,
            RequestBindingDispositionBreakdown = dispositionEntries
                .GroupBy(item => new { item.Disposition, item.Mandatory })
                .OrderBy(group => group.Key.Disposition, StringComparer.Ordinal)
                .ThenByDescending(group => group.Key.Mandatory)
                .Select(group => new RuntimeTokenRequestBindingDispositionSummary
                {
                    Disposition = group.Key.Disposition,
                    Mandatory = group.Key.Mandatory,
                    RequestCount = group.Count(),
                    ContextTokens = group.Sum(item => (long)ResolveTotalContextTokens(item.Record)),
                    BillableTokens = group.Sum(item => (long)ResolveTotalBillableTokens(item.Record)),
                })
                .ToArray(),
            OperatorReadbackInclusions = operatorReadbackInclusions
                .OrderBy(item => item.RequestId, StringComparer.Ordinal)
                .ToArray(),
            BindingGaps = bindingGaps
                .GroupBy(item => new { item.Reason, item.Mandatory })
                .OrderBy(group => group.Key.Reason, StringComparer.Ordinal)
                .ThenByDescending(group => group.Key.Mandatory)
                .Select(group => new RuntimeTokenBindingGapSummary
                {
                    Reason = group.Key.Reason,
                    Mandatory = group.Key.Mandatory,
                    RequestCount = group.Count(),
                    ContextTokens = group.Sum(item => (long)ResolveTotalContextTokens(item.Record)),
                    BillableTokens = group.Sum(item => (long)ResolveTotalBillableTokens(item.Record)),
                })
                .ToArray(),
            RunReportCoverage = new RuntimeTokenRunReportCoverageSummary
            {
                IncludedRequestsWithRunId = includedRequestsWithRunId,
                IncludedRequestsWithMatchingRunReport = includedRequestsWithMatchingRunReport,
                IncludedRequestsMissingMatchingRunReport = includedRequestsMissingMatchingRunReport,
            },
            ContextWindowView = contextWindowView,
            BillableCostView = billableCostView,
            TaskCosts = taskCosts,
        };
    }

    private static bool IsMandatoryIfIncluded(string requestKind)
    {
        return IncludedByDefaultRequestKinds.Contains(requestKind)
               || string.Equals(requestKind, "bootstrap", StringComparison.Ordinal);
    }

    private static string ResolveBoundDisposition(DomainTaskStatus status)
    {
        if (IsSuccessful(status))
        {
            return "bound_to_successful_task";
        }

        return status is DomainTaskStatus.Suggested
            or DomainTaskStatus.Pending
            or DomainTaskStatus.Deferred
            or DomainTaskStatus.Running
            or DomainTaskStatus.Testing
            or DomainTaskStatus.Review
            or DomainTaskStatus.ApprovalWait
            ? "bound_to_attempted_unknown_outcome"
            : "bound_to_unsuccessful_task";
    }

    private static IReadOnlyList<string> BuildTaskCostViewBlockingReasons(
        int successfulTaskCount,
        IReadOnlyList<BindingGapEntry> bindingGaps)
    {
        var reasons = new List<string>();
        if (successfulTaskCount <= 0)
        {
            reasons.Add("successful_task_denominator_missing");
        }

        if (bindingGaps.Any(item => item.Mandatory))
        {
            reasons.Add("mandatory_included_requests_unbound");
        }

        return reasons;
    }

    private static RequestDecision EvaluateInclusion(
        LlmRequestEnvelopeTelemetryRecord record,
        IReadOnlyDictionary<string, IReadOnlyList<LlmRequestEnvelopeTelemetryRecord>> childRequestsByParentId,
        IReadOnlyDictionary<string, IReadOnlyList<ExecutionRunReport>> reportsByTaskId)
    {
        if (IncludedByDefaultRequestKinds.Contains(record.RequestKind))
        {
            return new RequestDecision(record.RequestKind, "included_by_default", Included: true, ExclusionReason: null);
        }

        if (ExcludedByDefaultRequestKinds.Contains(record.RequestKind))
        {
            return new RequestDecision(record.RequestKind, "excluded_by_default", Included: false, ExclusionReason: "request_kind_excluded_by_default");
        }

        if (string.Equals(record.RequestKind, "bootstrap", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(record.TaskId) || string.IsNullOrWhiteSpace(record.RunId))
            {
                return new RequestDecision(record.RequestKind, "conditionally_included", Included: false, ExclusionReason: "bootstrap_missing_run_scope");
            }

            var hasMatchingRunReport = reportsByTaskId.TryGetValue(record.TaskId, out var reports)
                                      && reports.Any(report => string.Equals(report.RunId, record.RunId, StringComparison.Ordinal));
            return hasMatchingRunReport
                ? new RequestDecision(record.RequestKind, "conditionally_included", Included: true, ExclusionReason: null)
                : new RequestDecision(record.RequestKind, "conditionally_included", Included: false, ExclusionReason: "bootstrap_missing_matching_run_report");
        }

        if (string.Equals(record.RequestKind, "operator_readback", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(record.TaskId)
                || string.IsNullOrWhiteSpace(record.RunId)
                || string.IsNullOrWhiteSpace(record.RequestId))
            {
                return new RequestDecision(record.RequestKind, "conditionally_included", Included: false, ExclusionReason: "operator_readback_missing_task_binding");
            }

            if (!childRequestsByParentId.TryGetValue(record.RequestId, out var childRequests))
            {
                return new RequestDecision(record.RequestKind, "conditionally_included", Included: false, ExclusionReason: "operator_readback_reinjection_not_observed");
            }

            var reinjectionChild = childRequests
                .Where(child => !string.Equals(child.RequestKind, "operator_readback", StringComparison.Ordinal))
                .Where(child => !ExcludedByDefaultRequestKinds.Contains(child.RequestKind))
                .OrderBy(child => child.RecordedAtUtc)
                .ThenBy(child => child.AttributionId, StringComparer.Ordinal)
                .FirstOrDefault(child =>
                    string.Equals(child.TaskId, record.TaskId, StringComparison.Ordinal)
                    && string.Equals(child.RunId, record.RunId, StringComparison.Ordinal));

            return reinjectionChild is not null
                ? new RequestDecision(
                    record.RequestKind,
                    "conditionally_included",
                    Included: true,
                    ExclusionReason: null,
                    ReinjectionEvidenceType: "child_llm_request",
                    ReinjectionRequestId: reinjectionChild.RequestId,
                    LlmReinjectionCount: childRequests.Count(child =>
                        !string.Equals(child.RequestKind, "operator_readback", StringComparison.Ordinal)
                        && !ExcludedByDefaultRequestKinds.Contains(child.RequestKind)
                        && string.Equals(child.TaskId, record.TaskId, StringComparison.Ordinal)
                        && string.Equals(child.RunId, record.RunId, StringComparison.Ordinal)))
                : new RequestDecision(record.RequestKind, "conditionally_included", Included: false, ExclusionReason: "operator_readback_reinjection_not_observed");
        }

        return new RequestDecision(record.RequestKind, "excluded_by_default", Included: false, ExclusionReason: "request_kind_not_included_policy");
    }

    private static RuntimeTokenTaskOutcomeCostRecord BuildTaskCostRecord(
        string taskId,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> records,
        TaskNode task,
        IReadOnlyDictionary<string, IReadOnlyList<ExecutionRunReport>> reportsByTaskId)
    {
        reportsByTaskId.TryGetValue(taskId, out var reports);
        reports ??= Array.Empty<ExecutionRunReport>();

        var includedRunIds = records
            .Select(record => record.RunId)
            .Where(runId => !string.IsNullOrWhiteSpace(runId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var matchingRunReportCount = reports
            .Count(report => includedRunIds.Contains(report.RunId, StringComparer.Ordinal));
        var latestRunStatus = reports
            .OrderBy(report => report.RecordedAtUtc)
            .ThenBy(report => report.RunId, StringComparer.Ordinal)
            .LastOrDefault()
            ?.RunStatus;

        return new RuntimeTokenTaskOutcomeCostRecord
        {
            TaskId = taskId,
            TaskStatus = task.Status,
            Successful = IsSuccessful(task.Status),
            IncludedRequestCount = records.Count,
            IncludedRunCount = includedRunIds.Length,
            MatchingRunReportCount = matchingRunReportCount,
            LatestRunStatus = latestRunStatus,
            ContextWindowTotalTokens = records.Sum(record => (long)ResolveTotalContextTokens(record)),
            BillableTotalTokens = records.Sum(record => (long)ResolveTotalBillableTokens(record)),
        };
    }

    private static RuntimeTokenTaskCostViewSummary BuildViewSummary(
        string viewId,
        IReadOnlyList<LlmRequestEnvelopeTelemetryRecord> includedRecords,
        int attemptedTaskCount,
        int successfulTaskCount,
        Func<LlmRequestEnvelopeTelemetryRecord, int> inputResolver,
        Func<LlmRequestEnvelopeTelemetryRecord, int> totalResolver,
        bool taskCostViewTrusted)
    {
        var totalInputTokens = includedRecords.Sum(record => (long)inputResolver(record));
        var totalCachedInputTokens = includedRecords.Sum(record => (long)(record.CachedInputTokens ?? 0));
        var totalOutputTokens = includedRecords.Sum(record => (long)(record.OutputTokens ?? 0));
        var totalReasoningTokens = includedRecords.Sum(record => (long)(record.ReasoningTokens ?? 0));
        var totalTokens = includedRecords.Sum(record => (long)totalResolver(record));
        return new RuntimeTokenTaskCostViewSummary
        {
            ViewId = viewId,
            IncludedRequestCount = includedRecords.Count,
            AttemptedTaskCount = attemptedTaskCount,
            SuccessfulTaskCount = successfulTaskCount,
            TotalInputTokens = totalInputTokens,
            TotalCachedInputTokens = totalCachedInputTokens,
            TotalOutputTokens = totalOutputTokens,
            TotalReasoningTokens = totalReasoningTokens,
            TotalTokens = totalTokens,
            TokensPerSuccessfulTask = taskCostViewTrusted && successfulTaskCount > 0 ? totalTokens / (double)successfulTaskCount : null,
        };
    }

    private static int ResolveTotalContextTokens(LlmRequestEnvelopeTelemetryRecord record)
    {
        if (record.TotalContextTokensPerRequest.HasValue)
        {
            return record.TotalContextTokensPerRequest.Value;
        }

        if (record.ProviderReportedTotalTokens.HasValue)
        {
            return record.ProviderReportedTotalTokens.Value;
        }

        return RuntimeTokenBaselineAggregatorService.ResolveContextWindowTokens(record)
               + (record.OutputTokens ?? 0)
               + ResolveReasoningIncrement(record);
    }

    private static int ResolveTotalBillableTokens(LlmRequestEnvelopeTelemetryRecord record)
    {
        if (record.TotalBillableTokensPerRequest.HasValue)
        {
            return record.TotalBillableTokensPerRequest.Value;
        }

        return RuntimeTokenBaselineAggregatorService.ResolveBillableTokens(record)
               + (record.OutputTokens ?? 0)
               + ResolveReasoningIncrement(record);
    }

    private static int ResolveReasoningIncrement(LlmRequestEnvelopeTelemetryRecord record)
    {
        if (!record.ReasoningTokens.HasValue)
        {
            return 0;
        }

        return record.ReasoningTokensReportedSeparately
               && !record.ReasoningTokensIncludedInOutput
               && !record.ProviderTotalIncludesReasoning
            ? record.ReasoningTokens.Value
            : 0;
    }

    private static bool IsSuccessful(DomainTaskStatus status)
    {
        return status is DomainTaskStatus.Completed or DomainTaskStatus.Merged;
    }

    private sealed record RequestDecision(
        string RequestKind,
        string Policy,
        bool Included,
        string? ExclusionReason,
        string? ReinjectionEvidenceType = null,
        string? ReinjectionRequestId = null,
        int LlmReinjectionCount = 0);

    private sealed record BindingGapEntry(
        string Reason,
        bool Mandatory,
        LlmRequestEnvelopeTelemetryRecord Record);

    private sealed record RequestDispositionEntry(
        string Disposition,
        bool Mandatory,
        LlmRequestEnvelopeTelemetryRecord Record);
}
