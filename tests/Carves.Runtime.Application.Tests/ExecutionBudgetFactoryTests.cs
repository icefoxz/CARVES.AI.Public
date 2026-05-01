using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionBudgetFactoryTests
{
    [Fact]
    public void Create_HighConfidenceExpandsBudget()
    {
        var factory = new ExecutionBudgetFactory(new ExecutionPathClassifier());
        var task = CreateTask("T-CARD-175-001", totalRuns: 5, successCount: 5, failureStreak: 0);

        var confidence = factory.AssessConfidence(task);
        var budget = factory.Create(task);

        Assert.Equal(Carves.Runtime.Domain.Execution.ExecutionConfidenceLevel.High, confidence.Level);
        Assert.Equal(Carves.Runtime.Domain.Execution.ExecutionConfidenceLevel.High, budget.ConfidenceLevel);
        Assert.True(budget.MaxFiles > 3);
        Assert.True(budget.MaxRetries >= 3);
    }

    [Fact]
    public void Create_LowConfidenceShrinksBudget()
    {
        var factory = new ExecutionBudgetFactory(new ExecutionPathClassifier());
        var task = CreateTask("T-CARD-175-002", totalRuns: 4, successCount: 1, failureStreak: 2);

        var confidence = factory.AssessConfidence(task);
        var budget = factory.Create(task);

        Assert.Equal(Carves.Runtime.Domain.Execution.ExecutionConfidenceLevel.Low, confidence.Level);
        Assert.Equal(Carves.Runtime.Domain.Execution.ExecutionConfidenceLevel.Low, budget.ConfidenceLevel);
        Assert.True(budget.MaxFiles < 3);
        Assert.True(budget.MaxFailureDensity < 0.35);
    }

    private static TaskNode CreateTask(string taskId, int totalRuns, int successCount, int failureStreak)
    {
        return new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-175",
            Title = "Budget scaling test",
            Description = "Budget scales from historical confidence.",
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            TaskType = TaskType.Execution,
            Scope = ["src/CARVES.Runtime.Application/ExecutionPolicy/ExecutionBoundaryService.cs"],
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["execution_total_runs"] = totalRuns.ToString(),
                ["execution_success_count"] = successCount.ToString(),
                ["execution_failure_streak"] = failureStreak.ToString(),
            },
        };
    }
}
