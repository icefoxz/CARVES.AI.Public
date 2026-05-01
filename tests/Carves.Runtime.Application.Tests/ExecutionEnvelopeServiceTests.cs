using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class ExecutionEnvelopeServiceTests
{
    [Fact]
    public void Generate_CopiesSchemaAndWritesDeterministicEnvelopeFiles()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/contracts/task.schema.json", """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "TaskExecutionEnvelope"
}
""");
        var service = new ExecutionEnvelopeService(workspace.Paths);
        var task = new TaskNode
        {
            TaskId = "T-CARD-167-002",
            CardId = "CARD-167",
            Title = "Implement Task Execution Envelope v1",
            Description = "Generate provider-neutral task execution artifacts.",
            Scope = [".ai/execution/schema/task.schema.json", "src/CARVES.Runtime.Application/ControlPlane/ExecutionEnvelopeService.cs"],
            Constraints = ["result ingestion", "failure bridging"],
            Acceptance = ["task.json exists", "brief.md exists"],
            Validation = new ValidationPlan
            {
                Commands = [["dotnet", "test", "tests/Carves.Runtime.Application.Tests"]],
                Checks = ["schema remains stable"],
                ExpectedEvidence = ["execution envelope artifacts exist"],
            },
            AcceptanceContract = new AcceptanceContract
            {
                ContractId = "AC-T-CARD-167-002",
                Title = "Envelope contract",
                Status = AcceptanceContractLifecycleStatus.HumanReview,
                Intent = new AcceptanceContractIntent
                {
                    Goal = "Keep acceptance contract visible in execution envelope artifacts.",
                    BusinessValue = "Operators and workers should inspect the same governed intent.",
                },
                NonGoals = ["Do not hide evidence requirements inside freeform prose."],
                EvidenceRequired =
                [
                    new AcceptanceContractEvidenceRequirement { Type = "result_commit" },
                ],
                HumanReview = new AcceptanceContractHumanReviewPolicy
                {
                    Required = true,
                    ProvisionalAllowed = true,
                },
            },
        };
        var run = new ExecutionRun
        {
            RunId = "RUN-T-CARD-167-002-001",
            TaskId = task.TaskId,
            Status = ExecutionRunStatus.Running,
            TriggerReason = ExecutionRunTriggerReason.Initial,
            Goal = task.Description,
            CurrentStepIndex = 1,
            Steps =
            [
                new ExecutionStep
                {
                    StepId = "RUN-T-CARD-167-002-001-STEP-001",
                    Title = "Inspect task context.",
                    Kind = ExecutionStepKind.Inspect,
                    Status = ExecutionStepStatus.Completed,
                },
                new ExecutionStep
                {
                    StepId = "RUN-T-CARD-167-002-001-STEP-002",
                    Title = "Implement task.",
                    Kind = ExecutionStepKind.Implement,
                    Status = ExecutionStepStatus.InProgress,
                },
            ],
        };

        service.Generate(task, run);

        var schemaPath = Path.Combine(workspace.RootPath, ".ai", "execution", "schema", "task.schema.json");
        var taskJsonPath = Path.Combine(workspace.RootPath, ".ai", "execution", task.TaskId, "task.json");
        var briefPath = Path.Combine(workspace.RootPath, ".ai", "execution", task.TaskId, "brief.md");
        var resultPath = Path.Combine(workspace.RootPath, ".ai", "execution", task.TaskId, "result.json");
        var roguePath = Path.Combine(workspace.RootPath, ".ai", "execution", task.TaskId, "rogue.tmp");
        var rogueDirectory = Path.Combine(workspace.RootPath, ".ai", "execution", task.TaskId, "junk");
        var firstTaskJson = File.ReadAllText(taskJsonPath);
        var firstBrief = File.ReadAllText(briefPath);
        File.WriteAllText(resultPath, "{}");
        File.WriteAllText(roguePath, "rogue");
        Directory.CreateDirectory(rogueDirectory);

        service.Generate(task, run);

        Assert.Equal(File.ReadAllText(Path.Combine(workspace.RootPath, "docs", "contracts", "task.schema.json")), File.ReadAllText(schemaPath));
        Assert.Equal(firstTaskJson, File.ReadAllText(taskJsonPath));
        Assert.Equal(firstBrief, File.ReadAllText(briefPath));
        Assert.True(File.Exists(resultPath));
        Assert.False(File.Exists(roguePath));
        Assert.False(Directory.Exists(rogueDirectory));
        Assert.Contains("\"schemaVersion\": \"1.0\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"taskId\": \"T-CARD-167-002\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"budget\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"acceptanceContract\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"contractId\": \"AC-T-CARD-167-002\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"executionRun\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"runId\": \"RUN-T-CARD-167-002-001\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("\"size\": \"small\"", firstTaskJson, StringComparison.Ordinal);
        Assert.Contains("## Objective", firstBrief, StringComparison.Ordinal);
        Assert.Contains("## Acceptance Contract", firstBrief, StringComparison.Ordinal);
        Assert.Contains("result_commit", firstBrief, StringComparison.Ordinal);
        Assert.Contains("## Execution Budget", firstBrief, StringComparison.Ordinal);
        Assert.Contains("## Execution Run", firstBrief, StringComparison.Ordinal);
        Assert.Contains("- run id: RUN-T-CARD-167-002-001", firstBrief, StringComparison.Ordinal);
        Assert.Contains("- result ingestion", firstBrief, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_FallsBackToEmbeddedSchemaWhenRepoContractIsMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new ExecutionEnvelopeService(workspace.Paths);
        var task = new TaskNode
        {
            TaskId = "T-CARD-241-001",
            CardId = "CARD-241",
            Title = "Capture attach proof",
            Description = "Prove attach to task runtime truth for a real repo.",
            Scope = ["src/"],
            Acceptance = ["task.json exists"],
        };

        service.Generate(task);

        var schemaPath = Path.Combine(workspace.RootPath, ".ai", "execution", "schema", "task.schema.json");
        var schema = File.ReadAllText(schemaPath);

        Assert.True(File.Exists(schemaPath));
        Assert.Contains("\"title\": \"TaskExecutionEnvelope\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"required\": [", schema, StringComparison.Ordinal);
    }
}
