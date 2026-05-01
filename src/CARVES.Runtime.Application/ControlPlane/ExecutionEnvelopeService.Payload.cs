using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ExecutionEnvelopeService
{
    private static void PruneTaskDirectory(string taskRoot)
    {
        foreach (var directory in Directory.GetDirectories(taskRoot))
        {
            Directory.Delete(directory, recursive: true);
        }

        foreach (var file in Directory.GetFiles(taskRoot))
        {
            var fileName = Path.GetFileName(file);
            if (string.Equals(fileName, TaskJsonFileName, StringComparison.Ordinal)
                || string.Equals(fileName, BriefFileName, StringComparison.Ordinal)
                || string.Equals(fileName, ResultJsonFileName, StringComparison.Ordinal))
            {
                continue;
            }

            File.Delete(file);
        }
    }

    private JsonObject BuildTaskEnvelope(TaskNode task, ExecutionRun? run)
    {
        var relevantFiles = task.Scope.Count == 0 ? Array.Empty<string>() : task.Scope.Distinct(StringComparer.Ordinal).ToArray();
        var objective = string.IsNullOrWhiteSpace(task.Description) ? task.Title : task.Description;
        var scopeOut = task.Constraints
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var budget = budgetFactory.Create(task);

        return new JsonObject
        {
            ["schemaVersion"] = "1.0",
            ["taskId"] = task.TaskId,
            ["cardId"] = task.CardId,
            ["objective"] = objective,
            ["scope"] = new JsonObject
            {
                ["in"] = ToJsonArray(task.Scope),
                ["out"] = ToJsonArray(scopeOut),
            },
            ["files"] = new JsonObject
            {
                ["relevant"] = ToJsonArray(relevantFiles),
                ["allowed"] = ToJsonArray(relevantFiles),
                ["forbidden"] = ToJsonArray(DefaultForbiddenPaths),
            },
            ["acceptance"] = ToJsonArray(task.Acceptance),
            ["acceptanceContract"] = ToJsonNode(task.AcceptanceContract),
            ["budget"] = new JsonObject
            {
                ["schemaVersion"] = budget.SchemaVersion,
                ["size"] = budget.Size.ToString().ToLowerInvariant(),
                ["confidenceLevel"] = budget.ConfidenceLevel.ToString().ToLowerInvariant(),
                ["maxFiles"] = budget.MaxFiles,
                ["maxLinesChanged"] = budget.MaxLinesChanged,
                ["maxRetries"] = budget.MaxRetries,
                ["maxFailureDensity"] = budget.MaxFailureDensity,
                ["maxDurationMinutes"] = budget.MaxDurationMinutes,
                ["requiresReviewBoundary"] = budget.RequiresReviewBoundary,
                ["changeKinds"] = ToJsonArray(budget.ChangeKinds.Select(kind => kind.ToString().ToLowerInvariant())),
                ["summary"] = budget.Summary,
                ["rationale"] = budget.Rationale,
            },
            ["executionRun"] = run is null
                ? null
                : new JsonObject
                {
                    ["schemaVersion"] = run.SchemaVersion,
                    ["runId"] = run.RunId,
                    ["status"] = run.Status.ToString().ToLowerInvariant(),
                    ["triggerReason"] = run.TriggerReason.ToString().ToLowerInvariant(),
                    ["goal"] = run.Goal,
                    ["currentStepIndex"] = run.CurrentStepIndex,
                    ["steps"] = new JsonArray(run.Steps.Select(step => new JsonObject
                    {
                        ["stepId"] = step.StepId,
                        ["title"] = step.Title,
                        ["kind"] = step.Kind.ToString().ToLowerInvariant(),
                        ["status"] = step.Status.ToString().ToLowerInvariant(),
                    }).ToArray()),
                },
            ["validation"] = new JsonObject
            {
                ["commands"] = ToNestedJsonArray(task.Validation.Commands),
                ["checks"] = ToJsonArray(task.Validation.Checks),
                ["expectedEvidence"] = ToJsonArray(task.Validation.ExpectedEvidence),
            },
            ["stopConditions"] = ToJsonArray(DefaultStopConditions),
            ["rollback"] = new JsonObject
            {
                ["strategy"] = "discard",
                ["notes"] = "Regenerate the envelope from task truth before retrying in a clean workspace.",
            },
        };
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonNode? ToJsonNode<T>(T value)
    {
        return value is null ? null : JsonSerializer.SerializeToNode(value, JsonOptions);
    }

    private static JsonArray ToNestedJsonArray(IEnumerable<IReadOnlyList<string>> commands)
    {
        var array = new JsonArray();
        foreach (var command in commands)
        {
            array.Add(ToJsonArray(command));
        }

        return array;
    }
}
