using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Failures;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private ExecutionHistorySummary? BuildLastRunSummary(string taskId)
    {
        var run = executionRunService.ListRuns(taskId).LastOrDefault();
        if (run is null)
        {
            return null;
        }

        var report = executionRunReportService.ListReports(taskId)
            .LastOrDefault(item => string.Equals(item.RunId, run.RunId, StringComparison.Ordinal))
            ?? executionRunReportService.ListReports(taskId).LastOrDefault();
        var summary = report is null
            ? $"{run.Status} at step {Math.Min(run.Steps.Count, run.CurrentStepIndex + 1)}/{run.Steps.Count}"
            : $"{report.RunStatus} with {report.CompletedSteps}/{report.TotalSteps} completed steps";
        return new ExecutionHistorySummary
        {
            RunId = run.RunId,
            Status = run.Status.ToString(),
            Summary = summary,
            BoundaryReason = report?.BoundaryReason?.ToString(),
            ReplanStrategy = report?.ReplanStrategy?.ToString(),
        };
    }

    private TaskGraphLocalProjection BuildLocalTaskProjection(Carves.Runtime.Domain.Tasks.TaskGraph graph, TaskNode task)
    {
        var dependencies = task.Dependencies
            .Select(dependencyId => graph.Tasks.TryGetValue(dependencyId, out var dependency) ? dependency : null)
            .Where(static dependency => dependency is not null)
            .Select(dependency => ToScopeItem(dependency!))
            .ToArray();
        var blockers = graph.ListTasks()
            .Where(item => item.Dependencies.Contains(task.TaskId, StringComparer.Ordinal))
            .Take(5)
            .Select(ToScopeItem)
            .ToArray();

        return new TaskGraphLocalProjection
        {
            CurrentTaskId = task.TaskId,
            CurrentTaskTitle = task.Title,
            Dependencies = dependencies,
            Blockers = blockers,
        };
    }

    private IReadOnlyList<ContextPackModuleProjection> BuildModuleProjection(
        CodeGraphScopeAnalysis scopeAnalysis,
        IReadOnlyList<Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry> moduleSummaries,
        IReadOnlyList<Carves.Runtime.Domain.Memory.MemoryDocument> moduleMemory,
        int top)
    {
        return scopeAnalysis.Modules
            .Take(top)
            .Select(module =>
            {
                var moduleSummary = moduleSummaries.FirstOrDefault(entry => string.Equals(entry.Name, module, StringComparison.OrdinalIgnoreCase));
                var memoryDoc = moduleMemory.FirstOrDefault(document => string.Equals(document.Title, module, StringComparison.OrdinalIgnoreCase));
                var summary = moduleSummary?.Summary
                    ?? scopeAnalysis.SummaryLines.FirstOrDefault(line => line.StartsWith($"{module}:", StringComparison.OrdinalIgnoreCase))
                    ?? memoryDoc?.Title
                    ?? "Relevant module";
                var files = scopeAnalysis.Files
                    .Where(file => MatchesModulePath(file, moduleSummary, module))
                    .Take(3)
                    .DefaultIfEmpty(scopeAnalysis.Files.FirstOrDefault() ?? string.Empty)
                    .Where(file => !string.IsNullOrWhiteSpace(file))
                    .ToArray();
                return new ContextPackModuleProjection
                {
                    Module = module,
                    Summary = summary,
                    Files = files,
                };
            })
            .ToArray();
    }

    private static bool MatchesModulePath(
        string filePath,
        Carves.Runtime.Domain.CodeGraph.CodeGraphModuleEntry? moduleSummary,
        string moduleName)
    {
        if (moduleSummary is not null && !string.IsNullOrWhiteSpace(moduleSummary.PathPrefix))
        {
            return filePath.StartsWith(moduleSummary.PathPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        }

        return filePath.Contains(moduleName, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<ContextPackArtifactReference> BuildTaskReferences(
        TaskNode task,
        IReadOnlyList<Carves.Runtime.Domain.Memory.MemoryDocument> moduleMemory,
        CompactFailureSummary? lastFailureSummary,
        ExecutionHistorySummary? lastRunSummary)
    {
        var references = new List<ContextPackArtifactReference>();
        if (!string.IsNullOrWhiteSpace(task.CardId))
        {
            var cardPath = Path.Combine(paths.CardsRoot, $"{task.CardId}.md");
            if (File.Exists(cardPath))
            {
                references.Add(new ContextPackArtifactReference
                {
                    Kind = "card",
                    Path = ToRuntimeRelativePath(cardPath),
                    Summary = "Full card source remains on disk.",
                });
            }
        }

        foreach (var document in moduleMemory.Take(3))
        {
            references.Add(new ContextPackArtifactReference
            {
                Kind = "memory",
                Path = document.Path,
                Summary = $"Module memory: {document.Title}",
            });
        }

        if (!string.IsNullOrWhiteSpace(task.LastWorkerDetailRef))
        {
            references.Add(new ContextPackArtifactReference
            {
                Kind = "worker_execution",
                Path = task.LastWorkerDetailRef,
                Summary = "Full worker execution detail remains on disk behind a prompt-safe summary.",
            });
        }

        if (!string.IsNullOrWhiteSpace(task.LastProviderDetailRef))
        {
            references.Add(new ContextPackArtifactReference
            {
                Kind = "provider_execution",
                Path = task.LastProviderDetailRef,
                Summary = "Full provider execution detail remains on disk behind a prompt-safe summary.",
            });
        }

        if (lastFailureSummary is not null)
        {
            foreach (var artifact in lastFailureSummary.ArtifactReferences)
            {
                references.Add(new ContextPackArtifactReference
                {
                    Kind = "failure_artifact",
                    Path = artifact,
                    Summary = "Full failure artifact remains on disk.",
                });
            }
        }

        if (lastRunSummary is not null)
        {
            references.Add(new ContextPackArtifactReference
            {
                Kind = "execution_history",
                Path = Path.Combine(".ai", "runtime", "runs", task.TaskId, $"{lastRunSummary.RunId}.json").Replace('\\', '/'),
                Summary = "Full execution run history remains on disk.",
            });
        }

        return references
            .DistinctBy(reference => reference.Path, StringComparer.Ordinal)
            .ToArray();
    }
}
