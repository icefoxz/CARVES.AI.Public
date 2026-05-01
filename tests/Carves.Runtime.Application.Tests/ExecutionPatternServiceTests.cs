using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionPatternServiceTests
{
    [Fact]
    public void Analyze_RepeatedBoundaryStops_ReturnsBoundaryLoop()
    {
        var service = new ExecutionPatternService();
        ExecutionRunReport[] reports =
        [
            CreateReport("RUN-001", ExecutionRunStatus.Stopped, "FP-1", ExecutionBoundaryStopReason.Timeout, ExecutionBoundaryReplanStrategy.SplitTask, filesChanged: 6, completedSteps: 3, modules: ["src/a"]),
            CreateReport("RUN-002", ExecutionRunStatus.Stopped, "FP-1", ExecutionBoundaryStopReason.Timeout, ExecutionBoundaryReplanStrategy.SplitTask, filesChanged: 6, completedSteps: 3, modules: ["src/a"]),
            CreateReport("RUN-003", ExecutionRunStatus.Stopped, "FP-1", ExecutionBoundaryStopReason.Timeout, ExecutionBoundaryReplanStrategy.SplitTask, filesChanged: 6, completedSteps: 3, modules: ["src/a"]),
        ];

        var pattern = service.Analyze("T-CARD-189-001", reports);

        Assert.Equal(ExecutionPatternType.BoundaryLoop, pattern.Type);
        Assert.Equal(ExecutionPatternSeverity.High, pattern.Severity);
        Assert.Equal(ExecutionPatternSuggestion.NarrowScope, pattern.Suggestion);
    }

    [Fact]
    public void Analyze_ExpandingModulesWithoutCompletion_ReturnsScopeDrift()
    {
        var service = new ExecutionPatternService();
        ExecutionRunReport[] reports =
        [
            CreateReport("RUN-001", ExecutionRunStatus.Failed, "FP-2", null, null, filesChanged: 2, completedSteps: 2, modules: ["src/a"]),
            CreateReport("RUN-002", ExecutionRunStatus.Failed, "FP-3", null, null, filesChanged: 3, completedSteps: 2, modules: ["src/a", "src/b"]),
            CreateReport("RUN-003", ExecutionRunStatus.Failed, "FP-4", null, null, filesChanged: 4, completedSteps: 2, modules: ["src/a", "src/b", "src/c"]),
        ];

        var pattern = service.Analyze("T-CARD-189-002", reports);

        Assert.Equal(ExecutionPatternType.ScopeDrift, pattern.Type);
        Assert.Equal(ExecutionPatternSuggestion.NarrowScope, pattern.Suggestion);
    }

    [Fact]
    public void Analyze_ManyIncompleteRuns_ReturnsOverExecution()
    {
        var service = new ExecutionPatternService();
        var reports = Enumerable.Range(1, 5)
            .Select(index => CreateReport($"RUN-{index:000}", ExecutionRunStatus.Failed, $"FP-{index}", null, null, filesChanged: 2, completedSteps: 3, modules: ["src/a"]))
            .ToArray();

        var pattern = service.Analyze("T-CARD-189-003", reports);

        Assert.Equal(ExecutionPatternType.OverExecution, pattern.Type);
        Assert.Equal(ExecutionPatternSuggestion.PauseAndReview, pattern.Suggestion);
    }

    private static ExecutionRunReport CreateReport(
        string runId,
        ExecutionRunStatus status,
        string fingerprint,
        ExecutionBoundaryStopReason? boundaryReason,
        ExecutionBoundaryReplanStrategy? replanStrategy,
        int filesChanged,
        int completedSteps,
        IReadOnlyList<string> modules)
    {
        return new ExecutionRunReport
        {
            RunId = runId,
            TaskId = "T-PATTERN",
            Goal = "Pattern detection",
            RunStatus = status,
            BoundaryReason = boundaryReason,
            FailureType = status == ExecutionRunStatus.Failed ? FailureType.Unknown : null,
            ReplanStrategy = replanStrategy,
            ModulesTouched = modules,
            StepKinds = [ExecutionStepKind.Inspect, ExecutionStepKind.Implement, ExecutionStepKind.Verify],
            FilesChanged = filesChanged,
            CompletedSteps = completedSteps,
            TotalSteps = 5,
            Fingerprint = fingerprint,
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
