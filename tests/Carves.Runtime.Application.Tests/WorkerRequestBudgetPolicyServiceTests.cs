using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class WorkerRequestBudgetPolicyServiceTests
{
    [Fact]
    public void Resolve_UsesSelectedProviderBaselineForTrustedLongRunningLane()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRoot = workspace.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai"));
        var worktreeRoot = Path.Combine(repoRoot, ".carves-worktrees", "T-BUDGET-CODEX");
        Directory.CreateDirectory(worktreeRoot);

        var task = new TaskNode
        {
            TaskId = "T-BUDGET-CODEX",
            Title = "Codex live worker task",
            Description = "Project a governed request budget for the codex live lane.",
            TaskType = TaskType.Execution,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope = ["README.md"],
            Acceptance = ["request budget is explicit"],
            Validation = new ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "test", "tests/Ordering.UnitTests/Ordering.UnitTests.csproj"],
                ],
            },
        };

        var service = new WorkerRequestBudgetPolicyService(30);
        var budget = service.Resolve(
            task,
            new WorkerSelectionDecision
            {
                SelectedBackendId = "codex_cli",
                SelectedProviderId = "codex",
                SelectedProviderTimeoutSeconds = 120,
                SelectedBackendSupportsLongRunningTasks = true,
            },
            repoRoot,
            worktreeRoot);

        Assert.Equal(120, budget.TimeoutSeconds);
        Assert.Equal(120, budget.ProviderBaselineSeconds);
        Assert.Equal(ExecutionBudgetSize.Small, budget.ExecutionBudgetSize);
        Assert.Equal(ExecutionConfidenceLevel.Medium, budget.ConfidenceLevel);
        Assert.True(budget.LongRunningLane);
        Assert.True(budget.RepoTruthGuidanceRequired);
        Assert.Contains("provider_baseline=120s", budget.Reasons);
        Assert.Contains("long_running_lane", budget.Reasons);
        Assert.Contains("repo_root_truth_guidance", budget.Reasons);
    }

    [Fact]
    public void Resolve_UsesLongRunningCeilingForMediumCodexCliRepoTruthTask()
    {
        using var workspace = new TemporaryWorkspace();
        var repoRoot = workspace.RootPath;
        Directory.CreateDirectory(Path.Combine(repoRoot, ".ai"));
        var worktreeRoot = Path.Combine(repoRoot, ".carves-worktrees", "T-BUDGET-CODEX-MEDIUM");
        Directory.CreateDirectory(worktreeRoot);

        var task = new TaskNode
        {
            TaskId = "T-BUDGET-CODEX-MEDIUM",
            Title = "Codex live worker task",
            Description = "Project a governed request budget for a medium codex live lane task.",
            TaskType = TaskType.Execution,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope =
            [
                "docs/runtime/",
                "src/CARVES.Runtime.Application/Platform/",
                "src/CARVES.Runtime.Application/Planning/",
                "src/CARVES.Runtime.Application/ControlPlane/",
                "src/CARVES.Runtime.Host/",
            ],
            Acceptance = ["request budget gives the delegated CLI enough room to return"],
        };

        var service = new WorkerRequestBudgetPolicyService(30);
        var budget = service.Resolve(
            task,
            new WorkerSelectionDecision
            {
                SelectedBackendId = "codex_cli",
                SelectedProviderId = "codex",
                SelectedProviderTimeoutSeconds = 120,
                SelectedBackendSupportsLongRunningTasks = true,
            },
            repoRoot,
            worktreeRoot);

        Assert.Equal(180, budget.TimeoutSeconds);
        Assert.Equal(ExecutionBudgetSize.Medium, budget.ExecutionBudgetSize);
        Assert.True(budget.LongRunningLane);
        Assert.True(budget.RepoTruthGuidanceRequired);
        Assert.Contains("long_running_repo_truth_budget", budget.Reasons);
    }

    [Fact]
    public void Resolve_CanExpandBeyondShortProviderBaselineWhenExecutionBudgetDemandsMoreTime()
    {
        using var workspace = new TemporaryWorkspace();
        var task = new TaskNode
        {
            TaskId = "T-BUDGET-OPENAI",
            Title = "Remote worker task",
            Description = "Project a governed request budget for a larger bounded task.",
            TaskType = TaskType.Execution,
            Status = Carves.Runtime.Domain.Tasks.TaskStatus.Pending,
            Scope =
            [
                "src/A.cs",
                "src/B.cs",
                "src/C.cs",
                "src/D.cs",
                "src/E.cs",
                "tests/F.cs",
            ],
            Acceptance = ["request budget expands beyond the short provider baseline"],
            Validation = new ValidationPlan
            {
                Commands =
                [
                    ["dotnet", "test", "tests/Ordering.UnitTests/Ordering.UnitTests.csproj"],
                ],
            },
        };

        var service = new WorkerRequestBudgetPolicyService(30);
        var budget = service.Resolve(
            task,
            new WorkerSelectionDecision
            {
                SelectedBackendId = "openai_api",
                SelectedProviderId = "openai",
                SelectedProviderTimeoutSeconds = 30,
            },
            workspace.RootPath,
            workspace.RootPath);

        Assert.Equal(95, budget.TimeoutSeconds);
        Assert.Equal(30, budget.ProviderBaselineSeconds);
        Assert.Equal(ExecutionBudgetSize.Large, budget.ExecutionBudgetSize);
        Assert.Equal(75, budget.MaxDurationMinutes);
        Assert.False(budget.LongRunningLane);
        Assert.False(budget.RepoTruthGuidanceRequired);
        Assert.Contains("max_duration_minutes=75", budget.Reasons);
        Assert.Contains("validation_commands=1", budget.Reasons);
    }
}
