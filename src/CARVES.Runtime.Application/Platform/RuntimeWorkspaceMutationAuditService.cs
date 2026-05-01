using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeWorkspaceMutationAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string repoRoot;
    private readonly TaskGraphService taskGraphService;
    private readonly IRuntimeArtifactRepository artifactRepository;
    private readonly Func<string, IEnumerable<string>, ManagedWorkspacePathPolicyAssessment> pathPolicyEvaluator;

    public RuntimeWorkspaceMutationAuditService(
        string repoRoot,
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository,
        ManagedWorkspaceLeaseService managedWorkspaceLeaseService)
        : this(repoRoot, taskGraphService, artifactRepository, managedWorkspaceLeaseService.EvaluatePathPolicy)
    {
    }

    public RuntimeWorkspaceMutationAuditService(
        string repoRoot,
        TaskGraphService taskGraphService,
        IRuntimeArtifactRepository artifactRepository,
        Func<string, IEnumerable<string>, ManagedWorkspacePathPolicyAssessment> pathPolicyEvaluator)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.taskGraphService = taskGraphService;
        this.artifactRepository = artifactRepository;
        this.pathPolicyEvaluator = pathPolicyEvaluator;
    }

    public RuntimeWorkspaceMutationAuditSurface Build(string taskId)
    {
        var task = taskGraphService.GetTask(taskId);
        var changedPaths = LoadChangedPaths(taskId);
        var assessment = pathPolicyEvaluator(taskId, changedPaths);
        var blockers = assessment.TouchedPaths
            .Where(static path => path.PolicyClass is "scope_escape" or "host_only" or "deny")
            .Select(path => new RuntimeWorkspaceMutationAuditBlockerSurface
            {
                BlockerId = $"mutation_audit_{path.PolicyClass}",
                Path = path.Path,
                PolicyClass = path.PolicyClass,
                RequiredAction = ResolveRequiredAction(path.PolicyClass, assessment),
            })
            .ToArray();

        return new RuntimeWorkspaceMutationAuditSurface
        {
            TaskId = task.TaskId,
            CardId = task.CardId ?? string.Empty,
            ResultReturnChannel = $".ai/execution/{task.TaskId}/result.json",
            Status = ResolveStatus(assessment, changedPaths),
            LeaseAware = assessment.LeaseAware,
            LeaseId = assessment.LeaseId,
            AllowedWritablePaths = assessment.AllowedWritablePaths,
            ChangedPathCount = changedPaths.Count,
            ViolationCount = blockers.Length,
            ScopeEscapeCount = assessment.ScopeEscapeCount,
            HostOnlyCount = assessment.HostOnlyCount,
            DenyCount = assessment.DenyCount,
            CanProceedToWriteback = blockers.Length == 0,
            ChangedPaths = assessment.TouchedPaths.Select(static path => new RuntimeWorkspaceMutationTouchedPathSurface
            {
                Path = path.Path,
                PolicyClass = path.PolicyClass,
                AssetClass = path.AssetClass,
                Summary = path.Summary,
            }).ToArray(),
            Blockers = blockers,
            Summary = assessment.Summary,
            RecommendedNextAction = blockers.Length == 0
                ? assessment.RecommendedNextAction
                : $"Resolve mutation-audit blockers before review approval/writeback: {string.Join(", ", blockers.Select(blocker => blocker.BlockerId).Distinct(StringComparer.Ordinal))}.",
        };
    }

    private IReadOnlyList<string> LoadChangedPaths(string taskId)
    {
        var paths = new List<string>();
        var workerArtifact = artifactRepository.TryLoadWorkerExecutionArtifact(taskId);
        if (workerArtifact is not null)
        {
            paths.AddRange(workerArtifact.Evidence.FilesWritten);
            paths.AddRange(workerArtifact.Result.ChangedFiles);
        }

        var resultEnvelope = TryLoadResultEnvelope(taskId);
        if (resultEnvelope is not null)
        {
            paths.AddRange(resultEnvelope.Changes.FilesModified);
            paths.AddRange(resultEnvelope.Changes.FilesAdded);
            if (resultEnvelope.Telemetry.ObservedPaths.Count > 0)
            {
                paths.AddRange(resultEnvelope.Telemetry.ObservedPaths);
            }
        }

        return paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Replace('\\', '/').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ResultEnvelope? TryLoadResultEnvelope(string taskId)
    {
        var path = Path.Combine(repoRoot, ".ai", "execution", taskId, "result.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ResultEnvelope>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static string ResolveStatus(ManagedWorkspacePathPolicyAssessment assessment, IReadOnlyList<string> changedPaths)
    {
        if (changedPaths.Count == 0)
        {
            return assessment.LeaseAware ? "clear" : "no_changed_paths";
        }

        return assessment.Status;
    }

    private static string ResolveRequiredAction(string policyClass, ManagedWorkspacePathPolicyAssessment assessment)
    {
        return policyClass switch
        {
            "deny" => "remove denied-root or secret-like writes before any review or writeback",
            "host_only" => "route governed truth mutations through host-owned Runtime writeback",
            "scope_escape" => assessment.LeaseId is null
                ? "replan before accepting changes outside task scope"
                : $"replan before widening beyond active lease '{assessment.LeaseId}'",
            _ => assessment.RecommendedNextAction,
        };
    }
}
