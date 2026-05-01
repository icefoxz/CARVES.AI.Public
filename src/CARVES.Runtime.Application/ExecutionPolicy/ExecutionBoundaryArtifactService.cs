using System.Text.Json;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ExecutionPolicy;

public sealed class ExecutionBoundaryArtifactService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string boundaryRoot;

    public ExecutionBoundaryArtifactService(string aiRoot)
    {
        boundaryRoot = Path.Combine(aiRoot, "runtime", "boundary");
    }

    public ExecutionBoundaryArtifactSet Persist(
        string taskId,
        ExecutionBoundaryAssessment assessment,
        ExecutionBoundaryViolation? violation = null,
        ExecutionBoundaryReplanRequest? replan = null,
        ExecutionRun? run = null,
        BoundaryDecision? decision = null)
    {
        Directory.CreateDirectory(Path.Combine(boundaryRoot, "budgets"));
        Directory.CreateDirectory(Path.Combine(boundaryRoot, "telemetry"));
        Directory.CreateDirectory(Path.Combine(boundaryRoot, "violations"));
        Directory.CreateDirectory(Path.Combine(boundaryRoot, "replans"));
        Directory.CreateDirectory(Path.Combine(boundaryRoot, "decisions"));

        var budgetPath = GetBudgetPath(taskId);
        var telemetryPath = GetTelemetryPath(taskId);
        var violationPath = violation is null ? null : GetViolationPath(taskId);
        var replanPath = replan is null ? null : GetReplanPath(taskId);
        var decisionPath = decision is null ? null : GetDecisionPath(taskId);

        WriteJson(
            budgetPath,
            new ExecutionBoundaryBudgetSnapshot
            {
                TaskId = taskId,
                RunId = run?.RunId,
                CurrentStepIndex = run?.CurrentStepIndex ?? 0,
                TotalSteps = run?.Steps.Count ?? 0,
                Budget = assessment.Budget,
                Confidence = assessment.Confidence,
            });
        WriteJson(
            telemetryPath,
            new ExecutionBoundaryTelemetrySnapshot
            {
                TaskId = taskId,
                RunId = run?.RunId,
                CurrentStepIndex = run?.CurrentStepIndex ?? 0,
                TotalSteps = run?.Steps.Count ?? 0,
                Telemetry = assessment.Telemetry,
            });

        if (violationPath is not null && violation is not null)
        {
            WriteJson(violationPath, violation);
        }

        if (replanPath is not null && replan is not null)
        {
            WriteJson(replanPath, replan);
        }

        if (decisionPath is not null && decision is not null)
        {
            WriteJson(decisionPath, decision);
        }

        return new ExecutionBoundaryArtifactSet(budgetPath, telemetryPath, violationPath, replanPath, decisionPath);
    }

    public ExecutionBoundaryBudgetSnapshot? LoadBudget(string taskId)
    {
        return Read<ExecutionBoundaryBudgetSnapshot>(GetBudgetPath(taskId));
    }

    public ExecutionBoundaryTelemetrySnapshot? LoadTelemetry(string taskId)
    {
        return Read<ExecutionBoundaryTelemetrySnapshot>(GetTelemetryPath(taskId));
    }

    public ExecutionBoundaryViolation? LoadViolation(string taskId)
    {
        return Read<ExecutionBoundaryViolation>(GetViolationPath(taskId));
    }

    public ExecutionBoundaryReplanRequest? LoadReplan(string taskId)
    {
        return Read<ExecutionBoundaryReplanRequest>(GetReplanPath(taskId));
    }

    public BoundaryDecision? LoadDecision(string taskId)
    {
        return Read<BoundaryDecision>(GetDecisionPath(taskId));
    }

    public string GetBudgetPath(string taskId)
    {
        return Path.Combine(boundaryRoot, "budgets", $"{taskId}.json");
    }

    public string GetTelemetryPath(string taskId)
    {
        return Path.Combine(boundaryRoot, "telemetry", $"{taskId}.json");
    }

    public string GetViolationPath(string taskId)
    {
        return Path.Combine(boundaryRoot, "violations", $"{taskId}.json");
    }

    public string GetReplanPath(string taskId)
    {
        return Path.Combine(boundaryRoot, "replans", $"{taskId}.json");
    }

    public string GetDecisionPath(string taskId)
    {
        return Path.Combine(boundaryRoot, "decisions", $"{taskId}.json");
    }

    private static void WriteJson<T>(string path, T payload)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(payload, JsonOptions));
    }

    private static T? Read<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }
}

public sealed record ExecutionBoundaryArtifactSet(
    string BudgetPath,
    string TelemetryPath,
    string? ViolationPath,
    string? ReplanPath,
    string? DecisionPath);
