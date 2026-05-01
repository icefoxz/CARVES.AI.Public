using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenOutcomeBinderServiceTests
{
    [Fact]
    public void Bind_ComputesSuccessfulTaskDenominatorAndKeepsFailedTaskSpendInNumerator()
    {
        var cohort = CreateCohort(["worker", "planner"]);
        var graph = new DomainTaskGraph(
        [
            new TaskNode { TaskId = "T-SUCCESS", Status = DomainTaskStatus.Completed },
            new TaskNode { TaskId = "T-FAILED", Status = DomainTaskStatus.Failed },
        ]);
        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-001",
                requestId: "request-1",
                requestKind: "planner",
                taskId: "T-SUCCESS",
                runId: null,
                contextWindowInputTokensTotal: 40,
                billableInputTokensUncached: 40,
                outputTokens: 0,
                totalContextTokensPerRequest: 40,
                totalBillableTokensPerRequest: 40,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 1, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-002",
                requestId: "request-2",
                requestKind: "worker",
                taskId: "T-SUCCESS",
                runId: "RUN-SUCCESS-001",
                contextWindowInputTokensTotal: 100,
                billableInputTokensUncached: 80,
                outputTokens: 20,
                totalContextTokensPerRequest: 120,
                totalBillableTokensPerRequest: 100,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 2, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-003",
                requestId: "request-3",
                requestKind: "worker",
                taskId: "T-FAILED",
                runId: "RUN-FAILED-001",
                contextWindowInputTokensTotal: 60,
                billableInputTokensUncached: 50,
                outputTokens: 10,
                totalContextTokensPerRequest: 70,
                totalBillableTokensPerRequest: 60,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 3, 0, 0, TimeSpan.Zero)),
        };
        var reports = new Dictionary<string, IReadOnlyList<ExecutionRunReport>>(StringComparer.Ordinal)
        {
            ["T-SUCCESS"] =
            [
                new ExecutionRunReport
                {
                    RunId = "RUN-SUCCESS-001",
                    TaskId = "T-SUCCESS",
                    RunStatus = ExecutionRunStatus.Completed,
                    RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 4, 0, 0, TimeSpan.Zero),
                },
            ],
            ["T-FAILED"] =
            [
                new ExecutionRunReport
                {
                    RunId = "RUN-FAILED-001",
                    TaskId = "T-FAILED",
                    RunStatus = ExecutionRunStatus.Failed,
                    RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 5, 0, 0, TimeSpan.Zero),
                },
            ],
        };

        var binding = RuntimeTokenOutcomeBinderService.Bind(cohort, records, graph, reports);

        Assert.Equal(3, binding.IncludedRequestCount);
        Assert.Equal(0, binding.ExcludedRequestCount);
        Assert.Equal(0, binding.UnboundIncludedRequestCount);
        Assert.Equal(2, binding.AttemptedTaskCount);
        Assert.Equal(1, binding.SuccessfulTaskCount);
        Assert.True(binding.TaskCostViewTrusted);
        Assert.Equal(230, binding.ContextWindowView.TotalTokens);
        Assert.Equal(230d, binding.ContextWindowView.TokensPerSuccessfulTask);
        Assert.Equal(200, binding.BillableCostView.TotalTokens);
        Assert.Equal(200d, binding.BillableCostView.TokensPerSuccessfulTask);
        Assert.Equal(2, binding.RunReportCoverage.IncludedRequestsWithRunId);
        Assert.Equal(2, binding.RunReportCoverage.IncludedRequestsWithMatchingRunReport);
        Assert.Equal(0, binding.RunReportCoverage.IncludedRequestsMissingMatchingRunReport);

        var outcomes = binding.TaskOutcomeBreakdown.ToDictionary(item => item.TaskStatus);
        Assert.True(outcomes[DomainTaskStatus.Completed].Successful);
        Assert.Equal(1, outcomes[DomainTaskStatus.Completed].TaskCount);
        Assert.False(outcomes[DomainTaskStatus.Failed].Successful);
        Assert.Equal(1, outcomes[DomainTaskStatus.Failed].TaskCount);
    }

    [Fact]
    public void Bind_ExcludesConditionalRequestKindsUntilTheirRulesResolve()
    {
        var cohort = CreateCohort(["bootstrap", "operator_readback", "manual_operator_inspect_only"]);
        var graph = new DomainTaskGraph(
        [
            new TaskNode { TaskId = "T-BOOT", Status = DomainTaskStatus.Pending },
        ]);
        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-010",
                requestId: "request-bootstrap",
                requestKind: "bootstrap",
                taskId: "T-BOOT",
                runId: null,
                contextWindowInputTokensTotal: 30,
                billableInputTokensUncached: 30,
                outputTokens: 0,
                totalContextTokensPerRequest: 30,
                totalBillableTokensPerRequest: 30,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 6, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-011",
                requestId: "request-readback",
                requestKind: "operator_readback",
                taskId: "T-BOOT",
                runId: "RUN-BOOT-001",
                parentRequestId: "missing-parent",
                contextWindowInputTokensTotal: 20,
                billableInputTokensUncached: 20,
                outputTokens: 0,
                totalContextTokensPerRequest: 20,
                totalBillableTokensPerRequest: 20,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 7, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-012",
                requestId: "request-inspect",
                requestKind: "manual_operator_inspect_only",
                taskId: null,
                runId: null,
                contextWindowInputTokensTotal: 10,
                billableInputTokensUncached: 10,
                outputTokens: 0,
                totalContextTokensPerRequest: 10,
                totalBillableTokensPerRequest: 10,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 8, 0, 0, TimeSpan.Zero)),
        };

        var binding = RuntimeTokenOutcomeBinderService.Bind(
            cohort,
            records,
            graph,
            new Dictionary<string, IReadOnlyList<ExecutionRunReport>>(StringComparer.Ordinal));

        Assert.Equal(0, binding.IncludedRequestCount);
        Assert.Equal(3, binding.ExcludedRequestCount);
        Assert.Equal(0, binding.AttemptedTaskCount);
        Assert.False(binding.TaskCostViewTrusted);
        Assert.Contains("successful_task_denominator_missing", binding.TaskCostViewBlockingReasons);

        var requestKinds = binding.RequestKindInclusion.ToDictionary(item => item.RequestKind, StringComparer.Ordinal);
        Assert.Equal("conditionally_included", requestKinds["bootstrap"].Policy);
        Assert.Contains(requestKinds["bootstrap"].ExclusionReasons, item => item.Key == "bootstrap_missing_run_scope");
        Assert.Equal("conditionally_included", requestKinds["operator_readback"].Policy);
        Assert.Contains(requestKinds["operator_readback"].ExclusionReasons, item => item.Key == "operator_readback_reinjection_not_observed");
        Assert.Equal("excluded_by_default", requestKinds["manual_operator_inspect_only"].Policy);

        var operatorReadback = Assert.Single(binding.OperatorReadbackInclusions);
        Assert.False(operatorReadback.Included);
        Assert.Equal("operator_readback_reinjection_not_observed", operatorReadback.ExclusionReason);
        Assert.Null(operatorReadback.ReinjectionRequestId);
        Assert.Equal(0, operatorReadback.LlmReinjectionCount);
    }

    [Fact]
    public void Bind_ExcludesOperatorReadbackWhenParentMatchesButNoReinjectionTruth()
    {
        var cohort = CreateCohort(["worker", "operator_readback"]);
        var graph = new DomainTaskGraph(
        [
            new TaskNode { TaskId = "T-READBACK", Status = DomainTaskStatus.Completed },
        ]);
        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-030",
                requestId: "request-parent",
                requestKind: "worker",
                taskId: "T-READBACK",
                runId: "RUN-READBACK-001",
                contextWindowInputTokensTotal: 80,
                billableInputTokensUncached: 70,
                outputTokens: 10,
                totalContextTokensPerRequest: 90,
                totalBillableTokensPerRequest: 80,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-031",
                requestId: "request-readback",
                requestKind: "operator_readback",
                taskId: "T-READBACK",
                runId: "RUN-READBACK-001",
                parentRequestId: "request-parent",
                contextWindowInputTokensTotal: 15,
                billableInputTokensUncached: 15,
                outputTokens: 0,
                totalContextTokensPerRequest: 15,
                totalBillableTokensPerRequest: 15,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 5, 0, TimeSpan.Zero)),
        };
        var reports = new Dictionary<string, IReadOnlyList<ExecutionRunReport>>(StringComparer.Ordinal)
        {
            ["T-READBACK"] =
            [
                new ExecutionRunReport
                {
                    RunId = "RUN-READBACK-001",
                    TaskId = "T-READBACK",
                    RunStatus = ExecutionRunStatus.Completed,
                    RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 12, 30, 0, TimeSpan.Zero),
                },
            ],
        };

        var binding = RuntimeTokenOutcomeBinderService.Bind(cohort, records, graph, reports);

        Assert.Equal(1, binding.IncludedRequestCount);
        var operatorReadback = Assert.Single(binding.OperatorReadbackInclusions);
        Assert.False(operatorReadback.Included);
        Assert.Equal("operator_readback_reinjection_not_observed", operatorReadback.ExclusionReason);
    }

    [Fact]
    public void Bind_IncludesOperatorReadbackWhenChildRequestProvesReinjectionTruth()
    {
        var cohort = CreateCohort(["worker", "operator_readback"]);
        var graph = new DomainTaskGraph(
        [
            new TaskNode { TaskId = "T-READBACK", Status = DomainTaskStatus.Completed },
        ]);
        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-040",
                requestId: "request-parent",
                requestKind: "worker",
                taskId: "T-READBACK",
                runId: "RUN-READBACK-001",
                contextWindowInputTokensTotal: 80,
                billableInputTokensUncached: 70,
                outputTokens: 10,
                totalContextTokensPerRequest: 90,
                totalBillableTokensPerRequest: 80,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-041",
                requestId: "request-readback",
                requestKind: "operator_readback",
                taskId: "T-READBACK",
                runId: "RUN-READBACK-001",
                parentRequestId: "request-parent",
                contextWindowInputTokensTotal: 15,
                billableInputTokensUncached: 15,
                outputTokens: 0,
                totalContextTokensPerRequest: 15,
                totalBillableTokensPerRequest: 15,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 5, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-042",
                requestId: "request-child",
                requestKind: "worker",
                taskId: "T-READBACK",
                runId: "RUN-READBACK-001",
                parentRequestId: "request-readback",
                contextWindowInputTokensTotal: 20,
                billableInputTokensUncached: 18,
                outputTokens: 2,
                totalContextTokensPerRequest: 22,
                totalBillableTokensPerRequest: 20,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 12, 10, 0, TimeSpan.Zero)),
        };
        var reports = new Dictionary<string, IReadOnlyList<ExecutionRunReport>>(StringComparer.Ordinal)
        {
            ["T-READBACK"] =
            [
                new ExecutionRunReport
                {
                    RunId = "RUN-READBACK-001",
                    TaskId = "T-READBACK",
                    RunStatus = ExecutionRunStatus.Completed,
                    RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 12, 30, 0, TimeSpan.Zero),
                },
            ],
        };

        var binding = RuntimeTokenOutcomeBinderService.Bind(cohort, records, graph, reports);

        Assert.Equal(3, binding.IncludedRequestCount);
        var operatorReadback = Assert.Single(binding.OperatorReadbackInclusions);
        Assert.True(operatorReadback.Included);
        Assert.Equal("child_llm_request", operatorReadback.ReinjectionEvidenceType);
        Assert.Equal("request-child", operatorReadback.ReinjectionRequestId);
        Assert.Equal(1, operatorReadback.LlmReinjectionCount);

        var requestKinds = binding.RequestKindInclusion.ToDictionary(item => item.RequestKind, StringComparer.Ordinal);
        Assert.Equal(1, requestKinds["operator_readback"].IncludedRequestCount);
    }

    [Fact]
    public void Bind_PreservesUnboundIncludedRequestsAndMarksTaskCostViewUntrusted()
    {
        var cohort = CreateCohort(["worker"]);
        var graph = new DomainTaskGraph(
        [
            new TaskNode { TaskId = "T-OK", Status = DomainTaskStatus.Completed },
        ]);
        var records = new[]
        {
            CreateRecord(
                attributionId: "REQENV-020",
                requestId: "request-missing-task",
                requestKind: "worker",
                taskId: null,
                runId: null,
                contextWindowInputTokensTotal: 50,
                billableInputTokensUncached: 40,
                outputTokens: 10,
                totalContextTokensPerRequest: 60,
                totalBillableTokensPerRequest: 50,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 9, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-021",
                requestId: "request-unknown-task",
                requestKind: "worker",
                taskId: "T-MISSING",
                runId: null,
                contextWindowInputTokensTotal: 80,
                billableInputTokensUncached: 60,
                outputTokens: 10,
                totalContextTokensPerRequest: 90,
                totalBillableTokensPerRequest: 70,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 10, 0, 0, TimeSpan.Zero)),
            CreateRecord(
                attributionId: "REQENV-022",
                requestId: "request-bound",
                requestKind: "worker",
                taskId: "T-OK",
                runId: "RUN-OK-001",
                contextWindowInputTokensTotal: 20,
                billableInputTokensUncached: 15,
                outputTokens: 10,
                totalContextTokensPerRequest: 30,
                totalBillableTokensPerRequest: 25,
                recordedAtUtc: new DateTimeOffset(2026, 4, 21, 11, 0, 0, TimeSpan.Zero)),
        };
        var reports = new Dictionary<string, IReadOnlyList<ExecutionRunReport>>(StringComparer.Ordinal)
        {
            ["T-OK"] =
            [
                new ExecutionRunReport
                {
                    RunId = "RUN-OK-001",
                    TaskId = "T-OK",
                    RunStatus = ExecutionRunStatus.Completed,
                    RecordedAtUtc = new DateTimeOffset(2026, 4, 21, 12, 0, 0, TimeSpan.Zero),
                },
            ],
        };

        var binding = RuntimeTokenOutcomeBinderService.Bind(cohort, records, graph, reports);

        Assert.Equal(3, binding.IncludedRequestCount);
        Assert.Equal(2, binding.UnboundIncludedRequestCount);
        Assert.Equal(2, binding.UnboundIncludedMandatoryRequestCount);
        Assert.Equal(0, binding.UnboundIncludedOptionalRequestCount);
        Assert.Equal(150, binding.UnboundIncludedContextTokens);
        Assert.Equal(120, binding.UnboundIncludedBillableTokens);
        Assert.Equal(1, binding.AttemptedTaskCount);
        Assert.Equal(1, binding.SuccessfulTaskCount);
        Assert.False(binding.TaskCostViewTrusted);
        Assert.Contains("mandatory_included_requests_unbound", binding.TaskCostViewBlockingReasons);
        Assert.Equal(180, binding.ContextWindowView.TotalTokens);
        Assert.Null(binding.ContextWindowView.TokensPerSuccessfulTask);
        Assert.Equal(145, binding.BillableCostView.TotalTokens);
        Assert.Null(binding.BillableCostView.TokensPerSuccessfulTask);
        Assert.Equal(2, binding.BindingGaps.Sum(item => item.RequestCount));
        Assert.Contains(binding.BindingGaps, item => item.Reason == "missing_task_id" && item.Mandatory && item.RequestCount == 1 && item.ContextTokens == 60 && item.BillableTokens == 50);
        Assert.Contains(binding.BindingGaps, item => item.Reason == "task_not_found" && item.Mandatory && item.RequestCount == 1 && item.ContextTokens == 90 && item.BillableTokens == 70);

        var dispositions = binding.RequestBindingDispositionBreakdown.ToDictionary(item => item.Disposition, StringComparer.Ordinal);
        Assert.Equal(2, dispositions["unbound_included_mandatory"].RequestCount);
        Assert.Equal(150, dispositions["unbound_included_mandatory"].ContextTokens);
        Assert.Equal(120, dispositions["unbound_included_mandatory"].BillableTokens);
        Assert.Equal(1, dispositions["bound_to_successful_task"].RequestCount);
    }

    private static RuntimeTokenBaselineCohortFreeze CreateCohort(IReadOnlyList<string> requestKinds)
    {
        return new RuntimeTokenBaselineCohortFreeze
        {
            CohortId = "phase_0a_baseline",
            WindowStartUtc = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero),
            WindowEndUtc = new DateTimeOffset(2026, 4, 30, 23, 59, 59, TimeSpan.Zero),
            RequestKinds = requestKinds,
            TokenAccountingSourcePolicy = "mixed_with_reconciliation",
        };
    }

    private static LlmRequestEnvelopeTelemetryRecord CreateRecord(
        string attributionId,
        string requestId,
        string requestKind,
        string? taskId,
        string? runId,
        int contextWindowInputTokensTotal,
        int billableInputTokensUncached,
        int outputTokens,
        int totalContextTokensPerRequest,
        int totalBillableTokensPerRequest,
        DateTimeOffset recordedAtUtc,
        string? parentRequestId = null)
    {
        return new LlmRequestEnvelopeTelemetryRecord
        {
            AttributionId = attributionId,
            RequestId = requestId,
            RequestKind = requestKind,
            RequestKindEnumVersion = "runtime_request_kind.v1",
            Model = "gpt-5-mini",
            Provider = "openai",
            ProviderApiVersion = "responses_v1",
            Tokenizer = "local_estimator_v1",
            RequestSerializerVersion = "runtime_request_serializer.v1",
            TokenAccountingSource = "provider_actual",
            TaskId = taskId,
            RunId = runId,
            ParentRequestId = parentRequestId,
            WholeRequestTokensEst = contextWindowInputTokensTotal,
            SumSegmentTokensEst = contextWindowInputTokensTotal,
            UnattributedTokensEst = 0,
            ContextWindowInputTokensTotal = contextWindowInputTokensTotal,
            BillableInputTokensUncached = billableInputTokensUncached,
            CachedInputTokens = Math.Max(0, contextWindowInputTokensTotal - billableInputTokensUncached),
            OutputTokens = outputTokens,
            TotalContextTokensPerRequest = totalContextTokensPerRequest,
            TotalBillableTokensPerRequest = totalBillableTokensPerRequest,
            ProviderReportedInputTokens = contextWindowInputTokensTotal,
            ProviderReportedUncachedInputTokens = billableInputTokensUncached,
            ProviderReportedOutputTokens = outputTokens,
            ProviderReportedTotalTokens = totalContextTokensPerRequest,
            PricingVersion = "test",
            PricingSource = "test",
            CostEstimationVersion = "test",
            RecordedAtUtc = recordedAtUtc,
        };
    }
}
