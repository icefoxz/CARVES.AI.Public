using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class JsonRuntimeArtifactRepository : IRuntimeArtifactRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneLockService lockService;

    public JsonRuntimeArtifactRepository(ControlPlanePaths paths, IControlPlaneLockService? lockService = null)
    {
        this.paths = paths;
        this.lockService = lockService ?? NoOpControlPlaneLockService.Instance;
    }

    public void SaveWorkerArtifact(TaskRunArtifact artifact)
    {
        var outputPath = Path.Combine(paths.WorkerArtifactsRoot, $"{artifact.Report.TaskId}.json");
        WriteArtifact(paths.WorkerArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "worker");
    }

    public TaskRunArtifact? TryLoadWorkerArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.WorkerArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<TaskRunArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SaveWorkerExecutionArtifact(WorkerExecutionArtifact artifact)
    {
        var outputPath = Path.Combine(paths.WorkerExecutionArtifactsRoot, $"{artifact.TaskId}.json");
        WriteArtifact(paths.WorkerExecutionArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "worker-execution");
    }

    public WorkerExecutionArtifact? TryLoadWorkerExecutionArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.WorkerExecutionArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        var artifact = JsonSerializer.Deserialize<WorkerExecutionArtifact>(File.ReadAllText(outputPath), JsonOptions);
        if (artifact is null)
        {
            return null;
        }

        var projection = string.IsNullOrWhiteSpace(artifact.Projection.DetailRef)
            ? PromptSafeArtifactProjectionFactory.ForWorkerExecution(taskId, artifact.Result)
            : artifact.Projection;
        return new WorkerExecutionArtifact
        {
            SchemaVersion = artifact.SchemaVersion,
            CapturedAt = artifact.CapturedAt,
            TaskId = artifact.TaskId,
            Result = artifact.Result with
            {
                Summary = string.IsNullOrWhiteSpace(artifact.Result.Summary)
                    ? projection.Summary
                    : PromptSafeArtifactProjectionFactory.Create(artifact.Result.Summary, artifact.Result.Summary, projection.DetailRef).Summary,
            },
            Evidence = artifact.Evidence,
            Projection = projection,
        };
    }

    public void SaveWorkerPermissionArtifact(WorkerPermissionArtifact artifact)
    {
        var outputPath = Path.Combine(paths.WorkerPermissionArtifactsRoot, $"{artifact.TaskId}.json");
        WriteArtifact(paths.WorkerPermissionArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "worker-permission");
    }

    public WorkerPermissionArtifact? TryLoadWorkerPermissionArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.WorkerPermissionArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkerPermissionArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public IReadOnlyList<WorkerPermissionArtifact> LoadWorkerPermissionArtifacts()
    {
        if (!Directory.Exists(paths.WorkerPermissionArtifactsRoot))
        {
            return Array.Empty<WorkerPermissionArtifact>();
        }

        return Directory.GetFiles(paths.WorkerPermissionArtifactsRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<WorkerPermissionArtifact>(File.ReadAllText(path), JsonOptions))
            .Where(artifact => artifact is not null)
            .Cast<WorkerPermissionArtifact>()
            .OrderBy(item => item.TaskId, StringComparer.Ordinal)
            .ToArray();
    }

    public void SaveProviderArtifact(AiExecutionArtifact artifact)
    {
        var outputPath = Path.Combine(paths.ProviderArtifactsRoot, $"{artifact.TaskId}.json");
        WriteArtifact(paths.ProviderArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "provider");
    }

    public AiExecutionArtifact? TryLoadProviderArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.ProviderArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        var artifact = JsonSerializer.Deserialize<AiExecutionArtifact>(File.ReadAllText(outputPath), JsonOptions);
        if (artifact is null)
        {
            return null;
        }

        var projection = string.IsNullOrWhiteSpace(artifact.Projection.DetailRef)
            ? PromptSafeArtifactProjectionFactory.ForProviderArtifact(taskId, artifact.Record)
            : artifact.Projection;
        return new AiExecutionArtifact
        {
            SchemaVersion = artifact.SchemaVersion,
            CapturedAt = artifact.CapturedAt,
            TaskId = artifact.TaskId,
            Record = artifact.Record,
            Projection = projection,
        };
    }

    public void SavePlannerProposalArtifact(PlannerProposalEnvelope artifact)
    {
        var outputPath = Path.Combine(paths.PlannerArtifactsRoot, $"{artifact.ProposalId}.json");
        WriteArtifact(paths.PlannerArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "planner-proposal");
    }

    public PlannerProposalEnvelope? TryLoadPlannerProposalArtifact(string proposalId)
    {
        var outputPath = Path.Combine(paths.PlannerArtifactsRoot, $"{proposalId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PlannerProposalEnvelope>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SaveSafetyArtifact(SafetyArtifact artifact)
    {
        var outputPath = Path.Combine(paths.SafetyArtifactsRoot, $"{artifact.Decision.TaskId}.json");
        WriteArtifact(paths.SafetyArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "safety");
    }

    public SafetyArtifact? TryLoadSafetyArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.SafetyArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<SafetyArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SavePlannerReviewArtifact(PlannerReviewArtifact artifact)
    {
        var outputPath = Path.Combine(paths.ReviewArtifactsRoot, $"{artifact.TaskId}.json");
        WriteArtifact(paths.ReviewArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "review");
    }

    public PlannerReviewArtifact? TryLoadPlannerReviewArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.ReviewArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<PlannerReviewArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SaveMergeCandidateArtifact(MergeCandidateArtifact artifact)
    {
        var outputPath = Path.Combine(paths.MergeArtifactsRoot, $"{artifact.TaskId}.json");
        WriteArtifact(paths.MergeArtifactsRoot, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "merge-candidate");
    }

    public void DeleteMergeCandidateArtifact(string taskId)
    {
        var outputPath = Path.Combine(paths.MergeArtifactsRoot, $"{taskId}.json");
        if (!File.Exists(outputPath))
        {
            return;
        }

        try
        {
            using var _ = lockService.Acquire($"artifact:merge-candidate:{Path.GetFileName(outputPath)}");
            File.Delete(outputPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RuntimeArtifactPersistenceException("merge-candidate", outputPath, exception);
        }
    }

    public void SaveRuntimeFailureArtifact(RuntimeFailureRecord artifact)
    {
        var payload = JsonSerializer.Serialize(artifact, JsonOptions);
        var artifactPath = Path.Combine(paths.RuntimeFailureArtifactsRoot, $"{artifact.FailureId}.json");
        WriteArtifact(Path.GetDirectoryName(paths.RuntimeFailureFile)!, paths.RuntimeFailureFile, payload, "runtime-failure");
        WriteArtifact(paths.RuntimeFailureArtifactsRoot, artifactPath, payload, "runtime-failure");
    }

    public RuntimeFailureRecord? TryLoadLatestRuntimeFailure()
    {
        var latestFailurePath = ResolveLatestFailurePath();
        if (latestFailurePath is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimeFailureRecord>(File.ReadAllText(latestFailurePath), JsonOptions);
    }

    public void SaveRuntimePackAdmissionArtifact(RuntimePackAdmissionArtifact artifact)
    {
        var directory = GetRuntimePackAdmissionRoot();
        var outputPath = Path.Combine(directory, "current.json");
        WriteArtifact(directory, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "runtime-pack-admission");
    }

    public RuntimePackAdmissionArtifact? TryLoadCurrentRuntimePackAdmissionArtifact()
    {
        var outputPath = Path.Combine(GetRuntimePackAdmissionRoot(), "current.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimePackAdmissionArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SaveRuntimePackAdmissionPolicyArtifact(RuntimePackAdmissionPolicyArtifact artifact)
    {
        var directory = GetRuntimePackAdmissionPolicyRoot();
        var outputPath = Path.Combine(directory, "current.json");
        WriteArtifact(directory, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "runtime-pack-admission-policy");
    }

    public RuntimePackAdmissionPolicyArtifact? TryLoadCurrentRuntimePackAdmissionPolicyArtifact()
    {
        var outputPath = Path.Combine(GetRuntimePackAdmissionPolicyRoot(), "current.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimePackAdmissionPolicyArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SaveRuntimePackSelectionArtifact(RuntimePackSelectionArtifact artifact)
    {
        var directory = GetRuntimePackSelectionRoot();
        var outputPath = Path.Combine(directory, "current.json");
        var payload = JsonSerializer.Serialize(artifact, JsonOptions);
        WriteArtifact(directory, outputPath, payload, "runtime-pack-selection");
        if (!string.IsNullOrWhiteSpace(artifact.SelectionId))
        {
            var historyRoot = GetRuntimePackSelectionHistoryRoot();
            var historyPath = Path.Combine(historyRoot, $"{artifact.SelectionId}.json");
            WriteArtifact(historyRoot, historyPath, payload, "runtime-pack-selection-history");
        }
    }

    public RuntimePackSelectionArtifact? TryLoadCurrentRuntimePackSelectionArtifact()
    {
        var outputPath = Path.Combine(GetRuntimePackSelectionRoot(), "current.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimePackSelectionArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public RuntimePackSelectionArtifact? TryLoadRuntimePackSelectionArtifact(string selectionId)
    {
        if (string.IsNullOrWhiteSpace(selectionId))
        {
            return null;
        }

        var outputPath = Path.Combine(GetRuntimePackSelectionHistoryRoot(), $"{selectionId.Trim()}.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimePackSelectionArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public IReadOnlyList<RuntimePackSelectionArtifact> LoadRuntimePackSelectionHistory(int? limit = null)
    {
        var directory = GetRuntimePackSelectionHistoryRoot();
        if (!Directory.Exists(directory))
        {
            return Array.Empty<RuntimePackSelectionArtifact>();
        }

        var artifacts = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<RuntimePackSelectionArtifact>(File.ReadAllText(path), JsonOptions))
            .Where(item => item is not null)
            .Cast<RuntimePackSelectionArtifact>()
            .OrderByDescending(item => item.SelectedAt)
            .ThenByDescending(item => item.SelectionId, StringComparer.Ordinal)
            .ToArray();

        return limit is > 0 ? artifacts.Take(limit.Value).ToArray() : artifacts;
    }

    public void SaveRuntimePackSelectionAuditEntry(RuntimePackSelectionAuditEntry artifact)
    {
        var directory = GetRuntimePackSelectionAuditRoot();
        var outputPath = Path.Combine(directory, $"{artifact.AuditId}.json");
        WriteArtifact(directory, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "runtime-pack-selection-audit");
    }

    public IReadOnlyList<RuntimePackSelectionAuditEntry> LoadRuntimePackSelectionAuditEntries(int? limit = null)
    {
        var directory = GetRuntimePackSelectionAuditRoot();
        if (!Directory.Exists(directory))
        {
            return Array.Empty<RuntimePackSelectionAuditEntry>();
        }

        var artifacts = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<RuntimePackSelectionAuditEntry>(File.ReadAllText(path), JsonOptions))
            .Where(item => item is not null)
            .Cast<RuntimePackSelectionAuditEntry>()
            .OrderByDescending(item => item.RecordedAt)
            .ThenByDescending(item => item.AuditId, StringComparer.Ordinal)
            .ToArray();

        return limit is > 0 ? artifacts.Take(limit.Value).ToArray() : artifacts;
    }

    public void SaveRuntimePackSwitchPolicyArtifact(RuntimePackSwitchPolicyArtifact artifact)
    {
        var directory = GetRuntimePackSwitchPolicyRoot();
        var outputPath = Path.Combine(directory, "current.json");
        WriteArtifact(directory, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "runtime-pack-switch-policy");
    }

    public RuntimePackSwitchPolicyArtifact? TryLoadCurrentRuntimePackSwitchPolicyArtifact()
    {
        var outputPath = Path.Combine(GetRuntimePackSwitchPolicyRoot(), "current.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimePackSwitchPolicyArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    public void SaveRuntimePackPolicyAuditEntry(RuntimePackPolicyAuditEntry artifact)
    {
        var directory = GetRuntimePackPolicyAuditRoot();
        var outputPath = Path.Combine(directory, $"{artifact.AuditId}.json");
        WriteArtifact(directory, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "runtime-pack-policy-audit");
    }

    public IReadOnlyList<RuntimePackPolicyAuditEntry> LoadRuntimePackPolicyAuditEntries(int? limit = null)
    {
        var directory = GetRuntimePackPolicyAuditRoot();
        if (!Directory.Exists(directory))
        {
            return Array.Empty<RuntimePackPolicyAuditEntry>();
        }

        var artifacts = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => JsonSerializer.Deserialize<RuntimePackPolicyAuditEntry>(File.ReadAllText(path), JsonOptions))
            .Where(item => item is not null)
            .Cast<RuntimePackPolicyAuditEntry>()
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.AuditId, StringComparer.Ordinal)
            .ToArray();

        return limit is > 0 ? artifacts.Take(limit.Value).ToArray() : artifacts;
    }

    public void SaveRuntimePackPolicyPreviewArtifact(RuntimePackPolicyPreviewArtifact artifact)
    {
        var directory = GetRuntimePackPolicyPreviewRoot();
        var outputPath = Path.Combine(directory, "current.json");
        WriteArtifact(directory, outputPath, JsonSerializer.Serialize(artifact, JsonOptions), "runtime-pack-policy-preview");
    }

    public RuntimePackPolicyPreviewArtifact? TryLoadCurrentRuntimePackPolicyPreviewArtifact()
    {
        var outputPath = Path.Combine(GetRuntimePackPolicyPreviewRoot(), "current.json");
        if (!File.Exists(outputPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimePackPolicyPreviewArtifact>(File.ReadAllText(outputPath), JsonOptions);
    }

    private string? ResolveLatestFailurePath()
    {
        if (File.Exists(paths.RuntimeFailureFile))
        {
            return paths.RuntimeFailureFile;
        }

        var legacyPath = Path.Combine(paths.RuntimeRoot, "last_failure.json");
        return File.Exists(legacyPath)
            ? legacyPath
            : null;
    }

    private string GetRuntimePackAdmissionRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-admission");
    }

    private string GetRuntimePackSelectionRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-selection");
    }

    private string GetRuntimePackAdmissionPolicyRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-admission-policy");
    }

    private string GetRuntimePackSelectionHistoryRoot()
    {
        return Path.Combine(GetRuntimePackSelectionRoot(), "history");
    }

    private string GetRuntimePackSelectionAuditRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-selection-audit");
    }

    private string GetRuntimePackSwitchPolicyRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-switch-policy");
    }

    private string GetRuntimePackPolicyAuditRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-policy-audit");
    }

    private string GetRuntimePackPolicyPreviewRoot()
    {
        return Path.Combine(paths.ArtifactsRoot, "runtime-pack-policy-preview");
    }

    private void WriteArtifact(string directory, string outputPath, string payload, string artifactKind)
    {
        try
        {
            using var _ = lockService.Acquire($"artifact:{artifactKind}:{Path.GetFileName(outputPath)}");
            Directory.CreateDirectory(directory);
            AtomicFileWriter.WriteAllText(outputPath, payload);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RuntimeArtifactPersistenceException(artifactKind, outputPath, exception);
        }
    }
}
