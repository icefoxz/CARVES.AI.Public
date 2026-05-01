using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class ExecutionRunReportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string aiRoot;
    private readonly RuntimeEvidenceStoreService? evidenceStoreService;

    public ExecutionRunReportService(ControlPlanePaths paths)
        : this(paths.AiRoot, new RuntimeEvidenceStoreService(paths))
    {
    }

    public ExecutionRunReportService(string aiRoot)
        : this(aiRoot, evidenceStoreService: null)
    {
    }

    private ExecutionRunReportService(string aiRoot, RuntimeEvidenceStoreService? evidenceStoreService)
    {
        this.aiRoot = aiRoot;
        this.evidenceStoreService = evidenceStoreService;
    }

    public ExecutionRunReport Persist(
        ExecutionRun run,
        ResultEnvelope? resultEnvelope = null,
        FailureReport? failure = null,
        ExecutionBoundaryViolation? violation = null,
        ExecutionBoundaryReplanRequest? replan = null,
        TaskRunReport? taskRunReport = null)
    {
        EnsureTerminalRun(run);
        resultEnvelope ??= LoadOptional<ResultEnvelope>(run.ResultEnvelopePath);
        violation ??= LoadOptional<ExecutionBoundaryViolation>(run.BoundaryViolationPath);
        replan ??= LoadOptional<ExecutionBoundaryReplanRequest>(run.ReplanArtifactPath);

        var changedPaths = ResolveChangedPaths(resultEnvelope, violation, taskRunReport);
        var modulesTouched = changedPaths
            .Select(NormalizeModule)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();
        var report = new ExecutionRunReport
        {
            RunId = run.RunId,
            TaskId = run.TaskId,
            Goal = run.Goal,
            RunStatus = run.Status,
            BoundaryReason = violation?.Reason,
            FailureType = ResolveFailureType(resultEnvelope, failure),
            ReplanStrategy = replan?.Strategy,
            ModulesTouched = modulesTouched,
            StepKinds = run.Steps.Select(static step => step.Kind).ToArray(),
            FilesChanged = ResolveFilesChanged(resultEnvelope, violation, taskRunReport, changedPaths),
            CompletedSteps = run.Steps.Count(static step => step.Status == ExecutionStepStatus.Completed),
            TotalSteps = run.Steps.Count,
            SelectedPack = run.SelectedPack,
            Fingerprint = ComputeFingerprint(run.Goal, modulesTouched, violation?.Reason, replan?.Strategy, run.SelectedPack),
            RecordedAtUtc = DateTimeOffset.UtcNow,
        };

        var path = GetReportPath(run.TaskId, run.RunId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
        evidenceStoreService?.RecordExecutionRun(
            run,
            report,
            resultEnvelope,
            failure,
            sourceEvidenceIds: ResolveRunSourceEvidenceIds(run.TaskId));
        return report;
    }

    public IReadOnlyList<ExecutionRunReport> ListReports(string taskId)
    {
        var root = GetTaskRoot(taskId);
        if (!Directory.Exists(root))
        {
            return Array.Empty<ExecutionRunReport>();
        }

        return Directory.GetFiles(root, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Read)
            .OrderBy(static report => report.RecordedAtUtc)
            .ThenBy(static report => report.RunId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<ExecutionRunReport> ListRecentReports(int limit)
    {
        if (limit <= 0)
        {
            return Array.Empty<ExecutionRunReport>();
        }

        var root = Path.Combine(aiRoot, "runtime", "run-reports");
        if (!Directory.Exists(root))
        {
            return Array.Empty<ExecutionRunReport>();
        }

        return Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
            .Select(Read)
            .OrderByDescending(static report => report.RecordedAtUtc)
            .ThenByDescending(static report => report.RunId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    public string GetReportPath(string taskId, string runId)
    {
        return Path.Combine(GetTaskRoot(taskId), $"{runId}.json");
    }

    private static ExecutionRunReport Read(string path)
    {
        return JsonSerializer.Deserialize<ExecutionRunReport>(File.ReadAllText(path), JsonOptions)
               ?? throw new InvalidOperationException($"Execution run report at '{path}' could not be deserialized.");
    }

    private static void EnsureTerminalRun(ExecutionRun run)
    {
        if (run.Status is ExecutionRunStatus.Planned or ExecutionRunStatus.Running)
        {
            throw new InvalidOperationException($"Execution run '{run.RunId}' is not terminal and cannot produce a run report.");
        }
    }

    private static IReadOnlyList<string> ResolveChangedPaths(
        ResultEnvelope? resultEnvelope,
        ExecutionBoundaryViolation? violation,
        TaskRunReport? taskRunReport)
    {
        if (violation?.Telemetry.ObservedPaths.Count > 0)
        {
            return violation.Telemetry.ObservedPaths;
        }

        if (resultEnvelope?.Telemetry.ObservedPaths.Count > 0)
        {
            return resultEnvelope.Telemetry.ObservedPaths;
        }

        if (resultEnvelope is not null)
        {
            return resultEnvelope.Changes.FilesModified
                .Concat(resultEnvelope.Changes.FilesAdded)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        if (taskRunReport?.WorkerExecution.ChangedFiles.Count > 0)
        {
            return taskRunReport.WorkerExecution.ChangedFiles;
        }

        return Array.Empty<string>();
    }

    private static int ResolveFilesChanged(
        ResultEnvelope? resultEnvelope,
        ExecutionBoundaryViolation? violation,
        TaskRunReport? taskRunReport,
        IReadOnlyList<string> changedPaths)
    {
        if (violation is not null && violation.Telemetry.FilesChanged > 0)
        {
            return violation.Telemetry.FilesChanged;
        }

        if (resultEnvelope is not null)
        {
            if (resultEnvelope.Telemetry.FilesChanged > 0)
            {
                return resultEnvelope.Telemetry.FilesChanged;
            }

            return changedPaths.Count;
        }

        if (taskRunReport is not null)
        {
            return taskRunReport.WorkerExecution.ChangedFiles.Count;
        }

        return changedPaths.Count;
    }

    private static FailureType? ResolveFailureType(ResultEnvelope? resultEnvelope, FailureReport? failure)
    {
        if (failure is not null)
        {
            return failure.Failure.Type;
        }

        if (!string.IsNullOrWhiteSpace(resultEnvelope?.Failure.Type)
            && Enum.TryParse<FailureType>(resultEnvelope.Failure.Type, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string NormalizeModule(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var directory = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
        return (directory ?? normalized).Replace('\\', '/').Trim().ToLowerInvariant();
    }

    private static string ComputeFingerprint(
        string goal,
        IReadOnlyList<string> modulesTouched,
        ExecutionBoundaryStopReason? boundaryReason,
        ExecutionBoundaryReplanStrategy? replanStrategy,
        RuntimePackExecutionAttribution? selectedPack)
    {
        var payload = string.Join(
            "|",
            NormalizeGoal(goal),
            string.Join(",", modulesTouched.Select(static item => item.ToLowerInvariant())),
            boundaryReason?.ToString().ToLowerInvariant() ?? "none",
            replanStrategy?.ToString().ToLowerInvariant() ?? "none",
            selectedPack?.PackId?.ToLowerInvariant() ?? "none",
            selectedPack?.PackVersion?.ToLowerInvariant() ?? "none",
            selectedPack?.Channel?.ToLowerInvariant() ?? "none");
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string NormalizeGoal(string goal)
    {
        var builder = new StringBuilder(goal.Length);
        var lastWasSpace = false;
        foreach (var character in goal.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSpace = false;
                continue;
            }

            if (lastWasSpace)
            {
                continue;
            }

            builder.Append(' ');
            lastWasSpace = true;
        }

        return builder.ToString().Trim();
    }

    private T? LoadOptional<T>(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return default;
        }

        var absolutePath = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(aiRoot, "..", path));
        if (!File.Exists(absolutePath))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(absolutePath), JsonOptions);
    }

    private string GetTaskRoot(string taskId)
    {
        return Path.Combine(aiRoot, "runtime", "run-reports", taskId);
    }

    private IReadOnlyList<string> ResolveRunSourceEvidenceIds(string taskId)
    {
        if (evidenceStoreService is null)
        {
            return Array.Empty<string>();
        }

        var context = evidenceStoreService.TryGetLatest(taskId, Carves.Runtime.Domain.Evidence.RuntimeEvidenceKind.ContextPack);
        return context is null ? Array.Empty<string>() : [context.EvidenceId];
    }
}
