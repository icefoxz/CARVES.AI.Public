using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.Failures;

public sealed class FailureSummaryProjectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ControlPlanePaths paths;
    private readonly FailureContextService failureContextService;
    private readonly ExecutionRunService executionRunService;

    public FailureSummaryProjectionService(
        ControlPlanePaths paths,
        FailureContextService failureContextService,
        ExecutionRunService executionRunService)
    {
        this.paths = paths;
        this.failureContextService = failureContextService;
        this.executionRunService = executionRunService;
    }

    public CompactFailureSummary? BuildForTask(string taskId)
    {
        var report = failureContextService.GetTaskFailures(taskId, 1).FirstOrDefault();
        if (report is null)
        {
            return null;
        }

        return Build(report, taskId);
    }

    public CompactFailureSummary? BuildLatest()
    {
        var report = failureContextService.LoadAll()
            .OrderByDescending(item => item.Timestamp)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (report is null)
        {
            return null;
        }

        return Build(report, report.TaskId);
    }

    private CompactFailureSummary Build(FailureReport report, string? taskId)
    {
        var affectedFile = report.InputSummary.FilesInvolved.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        var affectedModule = NormalizeModule(affectedFile);
        var latestRun = string.IsNullOrWhiteSpace(taskId)
            ? null
            : executionRunService.ListRuns(taskId).LastOrDefault();
        var envelope = LoadOptional<ResultEnvelope>(latestRun?.ResultEnvelopePath);
        var evidence = LoadOptional<ExecutionEvidence>(envelope?.ExecutionEvidencePath);
        var artifactRefs = new List<string>();
        AddArtifactRef(artifactRefs, envelope?.ExecutionEvidencePath);
        AddArtifactRef(artifactRefs, evidence?.BuildOutputRef);
        AddArtifactRef(artifactRefs, evidence?.TestOutputRef);
        AddArtifactRef(artifactRefs, evidence?.CommandLogRef);
        AddArtifactRef(artifactRefs, evidence?.PatchRef);

        return new CompactFailureSummary
        {
            FailureType = report.Failure.Type.ToString(),
            FailureLane = ResolveLane(report).ToLowerInvariant(),
            AffectedFile = affectedFile,
            AffectedModule = string.IsNullOrWhiteSpace(affectedModule) ? null : affectedModule,
            Reason = ResolveReason(report),
            BuildStatus = envelope?.Validation.Build ?? InferBuildStatus(report),
            TestStatus = envelope?.Validation.Tests ?? InferTestStatus(report),
            RuntimeStatus = envelope?.Status ?? report.Result.Status,
            ArtifactReferences = artifactRefs,
        };
    }

    private static string ResolveReason(FailureReport report)
    {
        var reason = report.Failure.Message;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            return reason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(report.Result.StopReason))
        {
            return report.Result.StopReason.Trim();
        }

        return report.Failure.Type.ToString();
    }

    private static string ResolveLane(FailureReport report)
    {
        if (!string.IsNullOrWhiteSpace(report.Failure.Lane))
        {
            return report.Failure.Lane!;
        }

        return report.Attribution.Layer switch
        {
            FailureAttributionLayer.Environment or FailureAttributionLayer.Provider => "substrate",
            FailureAttributionLayer.Worker or FailureAttributionLayer.Task or FailureAttributionLayer.Planner => "semantic",
            _ => report.Failure.Type is FailureType.EnvironmentFailure or FailureType.Timeout ? "substrate" : "semantic",
        };
    }

    private static string InferBuildStatus(FailureReport report)
    {
        return report.Failure.Type == FailureType.BuildFailure ? "failed" : "unknown";
    }

    private static string InferTestStatus(FailureReport report)
    {
        return report.Failure.Type == FailureType.TestRegression ? "failed" : "unknown";
    }

    private static string NormalizeModule(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim();
        var slash = normalized.LastIndexOf('/');
        return slash <= 0 ? string.Empty : normalized[..slash].ToLowerInvariant();
    }

    private static void AddArtifactRef(List<string> refs, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            refs.Add(path);
        }
    }

    private T? LoadOptional<T>(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
        {
            return default;
        }

        var path = Path.IsPathRooted(relativeOrAbsolutePath)
            ? relativeOrAbsolutePath
            : Path.GetFullPath(Path.Combine(paths.RepoRoot, relativeOrAbsolutePath));
        if (!File.Exists(path))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
    }
}
