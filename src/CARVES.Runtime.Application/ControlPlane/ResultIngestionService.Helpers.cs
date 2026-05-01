using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class ResultIngestionService
{
    private string NormalizeRepoRelative(string path)
    {
        return Path.GetRelativePath(paths.RepoRoot, path).Replace('\\', '/');
    }

    private static int ParseCounter(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var raw)
               && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private string ResultPath(string taskId)
    {
        return Path.Combine(paths.AiRoot, "execution", taskId, "result.json");
    }

    private static string ComputeFingerprint(ResultEnvelope envelope)
    {
        using var sha = SHA256.Create();
        var payload = JsonSerializer.Serialize(envelope, JsonOptions);
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static TaskNode Clone(
        TaskNode task,
        DomainTaskStatus status,
        PlannerReview review,
        IReadOnlyDictionary<string, string> metadata,
        AcceptanceContract? acceptanceContract = null)
    {
        return new TaskNode
        {
            TaskId = task.TaskId,
            Title = task.Title,
            Description = task.Description,
            Status = status,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Source = task.Source,
            CardId = task.CardId,
            ProposalSource = task.ProposalSource,
            ProposalReason = task.ProposalReason,
            ProposalConfidence = task.ProposalConfidence,
            ProposalPriorityHint = task.ProposalPriorityHint,
            BaseCommit = task.BaseCommit,
            ResultCommit = task.ResultCommit,
            Dependencies = task.Dependencies,
            Scope = task.Scope,
            Acceptance = task.Acceptance,
            Constraints = task.Constraints,
            AcceptanceContract = acceptanceContract ?? task.AcceptanceContract,
            Validation = task.Validation,
            RetryCount = task.RetryCount,
            Capabilities = task.Capabilities,
            Metadata = metadata,
            LastWorkerRunId = task.LastWorkerRunId,
            LastWorkerBackend = task.LastWorkerBackend,
            LastWorkerFailureKind = task.LastWorkerFailureKind,
            LastWorkerRetryable = task.LastWorkerRetryable,
            LastWorkerSummary = task.LastWorkerSummary,
            LastWorkerDetailRef = task.LastWorkerDetailRef,
            LastProviderDetailRef = task.LastProviderDetailRef,
            LastRecoveryAction = task.LastRecoveryAction,
            LastRecoveryReason = task.LastRecoveryReason,
            RetryNotBefore = task.RetryNotBefore,
            PlannerReview = review,
            CreatedAt = task.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }
}
