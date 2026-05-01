using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionBoundaryServiceTests
{
    [Fact]
    public void Assess_ExcessiveSuccessfulExecution_TriggersStop()
    {
        var task = new TaskNode
        {
            TaskId = "T-CARD-170-001",
            Title = "Boundary policy",
            Description = "Apply execution boundary policy.",
            Status = DomainTaskStatus.Completed,
            Scope = ["src/CARVES.Runtime.Application/ExecutionPolicy/ExecutionBoundaryService.cs"],
            Acceptance = ["risk is evaluated"],
        };
        var service = new ExecutionBoundaryService(
            new ExecutionBudgetFactory(new ExecutionPathClassifier()),
            new ExecutionPathClassifier());
        var result = new ResultEnvelope
        {
            TaskId = task.TaskId,
            Status = "success",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified =
                [
                    "src/CARVES.Runtime.Application/ExecutionPolicy/A.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/B.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/C.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/D.cs",
                ],
                LinesChanged = 300,
            },
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = ["dotnet build"],
                Build = "success",
                Tests = "not_run",
            },
            Result = new ResultEnvelopeOutcome
            {
                StopReason = "acceptance_satisfied",
            },
            Telemetry = new ExecutionTelemetry
            {
                DurationSeconds = 300,
                ObservedPaths =
                [
                    "src/CARVES.Runtime.Application/ExecutionPolicy/A.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/B.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/C.cs",
                    "src/CARVES.Runtime.Application/ExecutionPolicy/D.cs",
                ],
                ChangeKinds = [ExecutionChangeKind.SourceCode],
                BudgetExceeded = true,
            },
        };

        var assessment = service.Assess(task, result);

        Assert.Equal(ExecutionRiskLevel.Critical, assessment.RiskLevel);
        Assert.Equal(ExecutionBoundaryDecision.Stop, assessment.Decision);
        Assert.True(assessment.ShouldStop);
        Assert.Equal(ExecutionBoundaryStopReason.SizeExceeded, assessment.StopReason);
    }

    [Fact]
    public void Assess_ManagedWorkspaceHostOnlyWrite_TriggersScopedStop()
    {
        using var workspace = new TemporaryWorkspace();
        var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
        leaseRepository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-boundary-001",
                    TaskId = "T-CARD-170-002",
                    WorkspacePath = Path.Combine(workspace.RootPath, ".carves-worktrees", "lease-boundary-001"),
                    RepoRoot = workspace.RootPath,
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    AllowedWritablePaths = ["src/CARVES.Runtime.Application/ExecutionPolicy/ExecutionBoundaryService.cs"],
                },
            ],
        });
        var task = new TaskNode
        {
            TaskId = "T-CARD-170-002",
            Title = "Boundary policy",
            Description = "Apply execution boundary policy.",
            Status = DomainTaskStatus.Pending,
            Scope = ["src/CARVES.Runtime.Application/ExecutionPolicy/ExecutionBoundaryService.cs"],
            Acceptance = ["risk is evaluated"],
        };
        var service = new ExecutionBoundaryService(
            new ExecutionBudgetFactory(new ExecutionPathClassifier()),
            new ExecutionPathClassifier(),
            new ManagedWorkspacePathPolicyService(workspace.RootPath, leaseRepository));
        var result = new ResultEnvelope
        {
            TaskId = task.TaskId,
            Status = "success",
            Changes = new ResultEnvelopeChanges
            {
                FilesModified = [".ai/tasks/graph.json"],
                LinesChanged = 12,
            },
            Validation = new ResultEnvelopeValidation
            {
                CommandsRun = ["dotnet build"],
                Build = "success",
                Tests = "not_run",
            },
            Result = new ResultEnvelopeOutcome
            {
                StopReason = "acceptance_satisfied",
            },
        };

        var assessment = service.Assess(task, result);

        Assert.True(assessment.ShouldStop);
        Assert.Equal(ExecutionBoundaryStopReason.ManagedWorkspaceHostOnlyPath, assessment.StopReason);
        Assert.Equal("host_only", assessment.ManagedWorkspacePathPolicy.Status);
        Assert.Contains(".ai/tasks/graph.json", assessment.ManagedWorkspacePathPolicy.Summary, StringComparison.Ordinal);
    }
}
