using System.Text.Json;
using Carves.Runtime.Application.ExecutionPolicy;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult AuditRuntimeNoise()
    {
        var report = new RuntimeNoiseAuditService(taskGraphService, operatorApiService).Build();
        var lines = new List<string>
        {
            "Runtime noise audit:",
            $"Start gate: {report.StartGate.ToString().ToLowerInvariant()}",
            $"Blocked task entries: {report.BlockedTasks.Count}",
            $"Incident entries: {report.Incidents.Count}",
            $"Counts: active_blocker={report.ClassificationCounts.GetValueOrDefault("active_blocker")}, projection_noise={report.ClassificationCounts.GetValueOrDefault("projection_noise")}, legacy_debt={report.ClassificationCounts.GetValueOrDefault("legacy_debt")}",
            string.Empty,
            "Blocked tasks:",
        };

        if (report.BlockedTasks.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var task in report.BlockedTasks.Take(10))
            {
                lines.Add($"- {task.TaskId} [{task.Status}] {task.Classification.ToString().ToLowerInvariant()}: {task.Reason}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Incidents:");
        if (report.Incidents.Count == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var incident in report.Incidents.Take(10))
            {
                lines.Add($"- {incident.IncidentId} [{incident.IncidentType}] task={incident.TaskId ?? "(none)"} {incident.Classification.ToString().ToLowerInvariant()}: {incident.Reason}");
            }
        }

        return new OperatorCommandResult(0, lines);
    }

    public OperatorCommandResult InspectExecutionBudget(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var budgetFactory = new ExecutionBudgetFactory(new ExecutionPathClassifier());
        var budget = budgetFactory.Create(task);
        var confidence = budgetFactory.AssessConfidence(task);
        return new OperatorCommandResult(0,
        [
            $"Execution budget for {taskId}:",
            $"Size: {budget.Size.ToString().ToLowerInvariant()}",
            $"Confidence: {budget.ConfidenceLevel.ToString().ToLowerInvariant()} (success_rate={confidence.SuccessRate:0.00}, failure_streak={confidence.FailureStreak})",
            $"Max files: {budget.MaxFiles}",
            $"Max lines changed: {budget.MaxLinesChanged}",
            $"Max retries: {budget.MaxRetries}",
            $"Max failure density: {budget.MaxFailureDensity:0.00}",
            $"Max duration minutes: {budget.MaxDurationMinutes}",
            $"Requires review boundary: {budget.RequiresReviewBoundary}",
            $"Change kinds: {(budget.ChangeKinds.Count == 0 ? "(none)" : string.Join(", ", budget.ChangeKinds.Select(kind => kind.ToString().ToLowerInvariant())))}",
            $"Summary: {budget.Summary}",
            $"Rationale: {budget.Rationale}",
        ]);
    }

    public OperatorCommandResult InspectExecutionRisk(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var budgetFactory = new ExecutionBudgetFactory(new ExecutionPathClassifier());
        var budget = budgetFactory.Create(task);
        var resultPath = Path.Combine(paths.AiRoot, "execution", taskId, "result.json");
        if (!File.Exists(resultPath))
        {
            return new OperatorCommandResult(0,
            [
                $"Execution risk for {taskId}:",
                "State: no result envelope has been written yet.",
                $"Projected budget: {budget.Summary}",
                $"Current task metadata risk level: {task.Metadata.GetValueOrDefault("execution_risk_level", "(none)")}",
            ]);
        }

        var envelope = resultIngestionService.Read(taskId);
        var assessment = new ExecutionBoundaryService(budgetFactory, new ExecutionPathClassifier()).Assess(task, envelope);
        return new OperatorCommandResult(0,
        [
            $"Execution risk for {taskId}:",
            $"Risk level: {assessment.RiskLevel.ToString().ToLowerInvariant()}",
            $"Risk score: {assessment.RiskScore}",
            $"Decision: {assessment.Decision.ToString().ToLowerInvariant()}",
            $"Confidence: {assessment.Confidence.Level.ToString().ToLowerInvariant()}",
            $"Observed paths: {(assessment.Telemetry.ObservedPaths.Count == 0 ? "(none)" : string.Join(", ", assessment.Telemetry.ObservedPaths))}",
            $"Observed change kinds: {(assessment.Telemetry.ChangeKinds.Count == 0 ? "(none)" : string.Join(", ", assessment.Telemetry.ChangeKinds.Select(kind => kind.ToString())))}",
            $"Budget exceeded: {assessment.Telemetry.BudgetExceeded}",
            .. assessment.Reasons.Select(reason => $"- {reason}"),
        ]);
    }

    public OperatorCommandResult InspectBoundary(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var artifactService = new ExecutionBoundaryArtifactService(paths.AiRoot);
        var budgetSnapshot = artifactService.LoadBudget(taskId);
        var telemetrySnapshot = artifactService.LoadTelemetry(taskId);
        var violation = artifactService.LoadViolation(taskId);
        var replan = artifactService.LoadReplan(taskId);
        var decision = artifactService.LoadDecision(taskId);

        if (budgetSnapshot is null && telemetrySnapshot is null && violation is null && replan is null && decision is null)
        {
            return new OperatorCommandResult(0,
            [
                $"Boundary inspection for {taskId}:",
                "State: no persisted boundary artifacts exist.",
                $"Task status: {task.Status}",
                $"Boundary stopped: {task.Metadata.GetValueOrDefault("boundary_stopped", "false")}",
            ]);
        }

        var lines = new List<string>
        {
            $"Boundary inspection for {taskId}:",
            $"Task status: {task.Status}",
            $"Boundary stopped: {(violation is not null ? "true" : task.Metadata.GetValueOrDefault("boundary_stopped", "false"))}",
        };

        if (violation is not null)
        {
            lines.Add($"STOPPED: {violation.Reason}");
            lines.Add(string.Empty);
            lines.Add("Reason:");
            lines.Add($"- {violation.Detail}");
        }
        else
        {
            lines.Add($"Decision: {task.Metadata.GetValueOrDefault("execution_boundary_decision", "(none)")}");
            if (decision is not null)
            {
                lines.Add($"Writeback decision: {JsonNamingPolicy.SnakeCaseLower.ConvertName(decision.WritebackDecision.ToString())}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Context:");
        if (budgetSnapshot is not null)
        {
            lines.Add($"- run: {budgetSnapshot.RunId ?? "(none)"}");
            lines.Add($"- step position: {Math.Min(budgetSnapshot.TotalSteps, budgetSnapshot.CurrentStepIndex + 1)}/{budgetSnapshot.TotalSteps}");
            lines.Add($"- confidence: {budgetSnapshot.Confidence.Level.ToString().ToLowerInvariant()} (success_rate={budgetSnapshot.Confidence.SuccessRate:0.00}, failure_streak={budgetSnapshot.Confidence.FailureStreak})");
            lines.Add($"- budget: {budgetSnapshot.Budget.Size.ToString().ToLowerInvariant()}, <= {budgetSnapshot.Budget.MaxFiles} files, <= {budgetSnapshot.Budget.MaxLinesChanged} lines, <= {budgetSnapshot.Budget.MaxRetries} retries, failure density <= {budgetSnapshot.Budget.MaxFailureDensity:0.00}");
        }
        else
        {
            lines.Add("- budget: (missing)");
        }

        if (telemetrySnapshot is not null)
        {
            lines.Add($"- telemetry: {telemetrySnapshot.Telemetry.FilesChanged} files, {telemetrySnapshot.Telemetry.LinesChanged} lines, retries={telemetrySnapshot.Telemetry.RetryCount}, failure_density={telemetrySnapshot.Telemetry.FailureDensity:0.00}, duration={telemetrySnapshot.Telemetry.DurationSeconds}s");
        }
        else
        {
            lines.Add("- telemetry: (missing)");
        }

        if (decision is not null)
        {
            lines.Add($"- evidence status: {decision.EvidenceStatus}");
            lines.Add($"- safety status: {decision.SafetyStatus}");
            lines.Add($"- test status: {decision.TestStatus}");
            lines.Add($"- failure lane: {decision.FailureLane}");
            lines.Add($"- reviewer required: {decision.ReviewerRequired}");
            lines.Add($"- decision confidence: {decision.DecisionConfidence:0.00}");
            lines.Add($"- reason codes: {(decision.ReasonCodes.Count == 0 ? "(none)" : string.Join(", ", decision.ReasonCodes))}");
            lines.Add($"- summary: {decision.Summary}");
            if (!string.IsNullOrWhiteSpace(decision.RecommendedNextAction))
            {
                lines.Add($"- next action: {decision.RecommendedNextAction}");
            }
        }

        if (task.Metadata.TryGetValue("managed_workspace_path_policy_status", out var pathPolicyStatus)
            && !string.IsNullOrWhiteSpace(pathPolicyStatus))
        {
            lines.Add($"- managed workspace path policy: {pathPolicyStatus}");
            lines.Add($"- managed workspace path summary: {task.Metadata.GetValueOrDefault("managed_workspace_path_policy_summary", "(none)")}");
        }

        lines.Add(string.Empty);
        lines.Add("Next Action:");
        if (replan is not null)
        {
            lines.Add($"- strategy: {replan.Strategy}");
            lines.Add($"- constraints: max_files={replan.Constraints.MaxFiles}, max_lines_changed={replan.Constraints.MaxLinesChanged}");
            foreach (var suggestion in replan.FollowUpSuggestions)
            {
                lines.Add($"- {suggestion}");
            }
        }
        else
        {
            lines.Add("- strategy: (none)");
        }

        return new OperatorCommandResult(0, lines);
    }
}
