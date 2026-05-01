using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionEnvelopeService
{
    private const string TaskJsonFileName = "task.json";
    private const string BriefFileName = "brief.md";
    private const string ResultJsonFileName = "result.json";
    private const string DefaultTaskSchemaJson = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "TaskExecutionEnvelope",
  "type": "object",
  "required": [
    "schemaVersion",
    "taskId",
    "cardId",
    "objective",
    "scope",
    "files",
    "acceptance",
    "budget",
    "validation",
    "stopConditions",
    "rollback"
  ]
}
""";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly IReadOnlyList<string> DefaultForbiddenPaths =
    [
        ".ai/**",
        ".carves-platform/**",
    ];

    private static readonly IReadOnlyList<string> DefaultStopConditions =
    [
        "blocked",
        "retry_required",
        "stalled",
        "lost_tracking",
    ];

    private readonly ControlPlanePaths paths;
    private readonly ExecutionBudgetFactory budgetFactory;

    public ExecutionEnvelopeService(ControlPlanePaths paths, ExecutionBudgetFactory? budgetFactory = null)
    {
        this.paths = paths;
        this.budgetFactory = budgetFactory ?? new ExecutionBudgetFactory(new ExecutionPathClassifier());
    }

    public void Generate(TaskNode task, ExecutionRun? run = null)
    {
        var executionRoot = Path.Combine(paths.AiRoot, "execution");
        var schemaRoot = Path.Combine(executionRoot, "schema");
        var taskRoot = Path.Combine(executionRoot, task.TaskId);
        var taskJsonPath = Path.Combine(taskRoot, TaskJsonFileName);
        var briefPath = Path.Combine(taskRoot, BriefFileName);
        var schemaTargetPath = Path.Combine(schemaRoot, "task.schema.json");
        var schemaSourcePath = Path.Combine(paths.RepoRoot, "docs", "contracts", "task.schema.json");

        Directory.CreateDirectory(schemaRoot);
        Directory.CreateDirectory(taskRoot);
        PruneTaskDirectory(taskRoot);

        var schemaJson = File.Exists(schemaSourcePath) ? File.ReadAllText(schemaSourcePath) : DefaultTaskSchemaJson;

        File.WriteAllText(schemaTargetPath, schemaJson);
        File.WriteAllText(taskJsonPath, BuildTaskEnvelope(task, run).ToJsonString(JsonOptions));
        File.WriteAllText(briefPath, BuildBrief(task, run));
    }
}
