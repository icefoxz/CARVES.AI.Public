using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Application.ExecutionPolicy;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    private ResourceLeaseRecord EnforceResultWriteSetLease(
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact,
        PacketEnforcementRecord packetEnforcement,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        ExecutionBoundaryArtifactSet boundaryArtifacts,
        bool requireWithinDeclaredScope)
    {
        var declaredWriteSet = BuildHostDeclaredResultWriteSet(
            originalTask,
            nextTask,
            run,
            packetEnforcement,
            reviewSubmissionAttempt,
            boundaryArtifacts);
        var issue = resourceLeaseService.TryAcquire(new ResourceLeaseRequest
        {
            WorkOrderId = $"result-ingestion:{run.RunId}",
            TaskId = originalTask.TaskId,
            DeclaredWriteSet = declaredWriteSet,
            ConflictPolicy = ResourceLeaseConflictPolicy.Stop,
            Now = DateTimeOffset.UtcNow,
        });
        if (!issue.Acquired)
        {
            throw new InvalidOperationException(
                $"Resource lease rejected task truth writeback for {originalTask.TaskId}: {string.Join("; ", issue.Lease.ConflictReasons)}");
        }

        var actualWriteSet = BuildActualResultWriteSet(
            originalTask,
            nextTask,
            run,
            envelope,
            workerArtifact,
            packetEnforcement,
            reviewSubmissionAttempt,
            boundaryArtifacts);
        var reconcile = resourceLeaseService.ReconcileActualWriteSet(issue.Lease.LeaseId, actualWriteSet);
        if (!reconcile.WithinDeclaredWriteSet)
        {
            if (!requireWithinDeclaredScope)
            {
                return reconcile.Lease!;
            }

            throw new InvalidOperationException(
                $"Actual write set exceeded Host-declared scope for {originalTask.TaskId}: {string.Join("; ", reconcile.EscalationReasons)}");
        }

        return reconcile.Lease!;
    }

    private ResourceLeaseRecord ReconcileResultWriteSetLease(
        ResourceLeaseRecord lease,
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact,
        PacketEnforcementRecord packetEnforcement,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        ExecutionBoundaryArtifactSet boundaryArtifacts,
        bool requireWithinDeclaredScope)
    {
        var actualWriteSet = BuildActualResultWriteSet(
            originalTask,
            nextTask,
            run,
            envelope,
            workerArtifact,
            packetEnforcement,
            reviewSubmissionAttempt,
            boundaryArtifacts);
        var reconcile = resourceLeaseService.ReconcileActualWriteSet(lease.LeaseId, actualWriteSet);
        if (!reconcile.WithinDeclaredWriteSet)
        {
            if (!requireWithinDeclaredScope)
            {
                return reconcile.Lease!;
            }

            throw new InvalidOperationException(
                $"Actual write set exceeded Host-declared scope for {originalTask.TaskId}: {string.Join("; ", reconcile.EscalationReasons)}");
        }

        return reconcile.Lease!;
    }

    private void ReleaseResultWriteSetLease(ResourceLeaseRecord? lease, string reason)
    {
        if (lease is null || lease.Status != ResourceLeaseStatus.Active)
        {
            return;
        }

        _ = resourceLeaseService.Release(lease.LeaseId, reason);
    }

    private ResourceWriteSet BuildHostDeclaredResultWriteSet(
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        PacketEnforcementRecord packetEnforcement,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        ExecutionBoundaryArtifactSet boundaryArtifacts)
    {
        var declaredPaths = (packetEnforcement.EditableRoots.Count == 0 ? originalTask.Scope : packetEnforcement.EditableRoots)
            .Select(NormalizeLeasePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Concat(BuildHostGovernedDeclaredPaths(originalTask, nextTask, run, reviewSubmissionAttempt, boundaryArtifacts))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var declaredModules = BuildDeclaredModules(originalTask, declaredPaths);

        return new ResourceWriteSet
        {
            TaskIds = [originalTask.TaskId],
            Paths = declaredPaths,
            Modules = declaredModules,
            TruthOperations = BuildTruthOperations(originalTask.TaskId, originalTask.Status, nextTask.Status, reviewSubmissionAttempt),
            TargetBranches = [],
        };
    }

    private ResourceWriteSet BuildActualResultWriteSet(
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact,
        PacketEnforcementRecord packetEnforcement,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        ExecutionBoundaryArtifactSet boundaryArtifacts)
    {
        var actualSourcePaths = ResolveActualChangedPaths(envelope, workerArtifact, packetEnforcement)
            .Select(NormalizeLeasePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
        var actualPaths = actualSourcePaths
            .Concat(BuildActualGovernedPaths(run, reviewSubmissionAttempt, boundaryArtifacts))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ResourceWriteSet
        {
            TaskIds = [originalTask.TaskId],
            Paths = actualPaths,
            Modules = actualSourcePaths
                .Select(NormalizeModuleHint)
                .Where(static module => !string.IsNullOrWhiteSpace(module))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            TruthOperations = BuildTruthOperations(originalTask.TaskId, originalTask.Status, nextTask.Status, reviewSubmissionAttempt),
            TargetBranches = [],
        };
    }

    private static IReadOnlyList<string> ResolveActualChangedPaths(
        ResultEnvelope envelope,
        WorkerExecutionArtifact? workerArtifact,
        PacketEnforcementRecord packetEnforcement)
    {
        if (packetEnforcement.ChangedFiles.Count > 0)
        {
            return packetEnforcement.ChangedFiles;
        }

        var workerPaths = (workerArtifact?.Evidence.FilesWritten ?? Array.Empty<string>())
            .Concat(workerArtifact?.Result.ChangedFiles ?? Array.Empty<string>())
            .Concat(workerArtifact?.Result.ObservedChangedFiles ?? Array.Empty<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (workerPaths.Length > 0)
        {
            return workerPaths;
        }

        return envelope.Changes.FilesModified
            .Concat(envelope.Changes.FilesAdded)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildHostGovernedDeclaredPaths(
        TaskNode originalTask,
        TaskNode nextTask,
        ExecutionRun run,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        ExecutionBoundaryArtifactSet boundaryArtifacts)
    {
        var declared = new List<string>
        {
            ToLeaseRelativePath(executionRunReportService.GetReportPath(originalTask.TaskId, run.RunId)),
            ToLeaseRelativePath(Path.Combine(paths.TaskNodesRoot, $"{originalTask.TaskId}.json")),
        };

        AddIfPresent(declared, boundaryArtifacts.DecisionPath is null ? null : ToLeaseRelativePath(boundaryArtifacts.DecisionPath));

        if (reviewSubmissionAttempt.Created)
        {
            AddIfPresent(declared, NormalizeLeasePath(reviewSubmissionAttempt.SubmissionPath));
            AddIfPresent(declared, ToLeaseRelativePath(Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.pending.json")));
            AddIfPresent(declared, ToLeaseRelativePath(Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json")));
        }

        if (originalTask.Status != nextTask.Status || reviewSubmissionAttempt.Created)
        {
            AddIfPresent(declared, ToLeaseRelativePath(Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "effect-ledger.jsonl")));
            AddIfPresent(declared, ToLeaseRelativePath(stateTransitionCertificateService.GetRunCertificatePath(run.RunId)));
        }

        if (originalTask.Status != nextTask.Status)
        {
            AddIfPresent(declared, ToLeaseRelativePath(Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "task-truth-writeback-receipt.json")));
        }

        return declared
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<string> BuildActualGovernedPaths(
        ExecutionRun run,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt,
        ExecutionBoundaryArtifactSet boundaryArtifacts)
    {
        var governed = new List<string>();
        AddIfFileExists(governed, executionRunReportService.GetReportPath(run.TaskId, run.RunId));
        AddIfFileExists(governed, boundaryArtifacts.DecisionPath);

        if (reviewSubmissionAttempt.Created)
        {
            AddIfFileExists(governed, reviewSubmissionAttempt.SubmissionPath);
            AddIfFileExists(governed, Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.pending.json"));
            AddIfFileExists(governed, Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "review-submission.json"));
        }

        AddIfFileExists(governed, Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "effect-ledger.jsonl"));
        AddIfFileExists(governed, stateTransitionCertificateService.GetRunCertificatePath(run.RunId));
        AddIfFileExists(governed, Path.Combine(paths.WorkerExecutionArtifactsRoot, run.RunId, "task-truth-writeback-receipt.json"));

        return governed
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDeclaredModules(TaskNode task, IReadOnlyList<string> declaredPaths)
    {
        var modules = new List<string>();
        AddMetadataModules(modules, task.Metadata, "codegraph_modules");
        AddMetadataModules(modules, task.Metadata, "codegraph_dependency_modules");
        AddMetadataModules(modules, task.Metadata, "codegraph_impacted_modules");
        foreach (var path in declaredPaths)
        {
            var normalized = NormalizeModuleHint(path);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                modules.Add(normalized!);
            }
        }

        return modules
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildTruthOperations(
        string taskId,
        DomainTaskStatus originalStatus,
        DomainTaskStatus nextStatus,
        RunToReviewSubmissionAttempt reviewSubmissionAttempt)
    {
        var operations = new List<string>();
        if (reviewSubmissionAttempt.Created && reviewSubmissionAttempt.Submission is not null)
        {
            operations.Add($"review_submission_recorded:{reviewSubmissionAttempt.Submission.SubmissionId}");
        }

        if (originalStatus != nextStatus)
        {
            operations.Add($"task_status_to_{nextStatus.ToString().ToLowerInvariant()}:{taskId}");
        }

        return operations
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddMetadataModules(ICollection<string> modules, IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        foreach (var module in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeModuleHint(module);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                modules.Add(normalized!);
            }
        }
    }

    private void AddIfFileExists(ICollection<string> pathsCollection, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = ResolveRepoPath(path);
        if (!File.Exists(fullPath))
        {
            return;
        }

        AddIfPresent(pathsCollection, ToLeaseRelativePath(fullPath));
    }

    private string ToLeaseRelativePath(string path)
    {
        var fullPath = ResolveRepoPath(path);
        return Path.GetRelativePath(paths.RepoRoot, fullPath).Replace('\\', '/');
    }

    private string ResolveRepoPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(paths.RepoRoot, path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static void AddIfPresent(ICollection<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    private static string? NormalizeLeasePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return path
            .Trim()
            .Trim('`')
            .Replace('\\', '/')
            .TrimStart("./".ToCharArray())
            .TrimEnd('/');
    }

    private static string? NormalizeModuleHint(string? value)
    {
        var normalized = NormalizeLeasePath(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Contains('/', StringComparison.Ordinal))
        {
            if (normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                var directory = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
                return directory?.Replace('\\', '/').Trim().ToLowerInvariant();
            }

            return normalized.TrimEnd('/').ToLowerInvariant();
        }

        return normalized.Trim().ToLowerInvariant();
    }
}
