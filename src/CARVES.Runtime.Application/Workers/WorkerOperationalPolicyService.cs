using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.Workers;

public sealed class WorkerOperationalPolicyService
{
    private readonly string? repoRoot;
    private readonly RepoRegistryService? repoRegistryService;
    private readonly WorkerOperationalPolicy policy;

    public WorkerOperationalPolicyService(
        string? repoRoot,
        RepoRegistryService? repoRegistryService,
        WorkerOperationalPolicy policy)
    {
        this.repoRoot = string.IsNullOrWhiteSpace(repoRoot)
            ? null
            : Path.GetFullPath(repoRoot);
        this.repoRegistryService = repoRegistryService;
        this.policy = policy;
    }

    public WorkerOperationalPolicyService(WorkerOperationalPolicy policy)
        : this(repoRoot: null, repoRegistryService: null, policy)
    {
    }

    public WorkerOperationalPolicy GetPolicy()
    {
        return policy;
    }

    public bool AppliesTo(string? repoId)
    {
        if (repoRegistryService is null || string.IsNullOrWhiteSpace(repoRoot))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(repoId))
        {
            return true;
        }

        var descriptor = repoRegistryService.List()
            .FirstOrDefault(item => string.Equals(item.RepoId, repoId, StringComparison.Ordinal));
        return descriptor is not null
            && string.Equals(Path.GetFullPath(descriptor.RepoPath), repoRoot, StringComparison.OrdinalIgnoreCase);
    }

    public string ResolvePreferredTrustProfileId(string? repoId, string defaultProfileId)
    {
        if (AppliesTo(repoId) && !string.IsNullOrWhiteSpace(policy.PreferredTrustProfileId))
        {
            return policy.PreferredTrustProfileId;
        }

        return defaultProfileId;
    }

    public string? ResolvePreferredBackendId(string? repoId, string? fallbackBackendId = null)
    {
        if (AppliesTo(repoId) && !string.IsNullOrWhiteSpace(policy.PreferredBackendId))
        {
            return policy.PreferredBackendId;
        }

        return fallbackBackendId;
    }

    public IReadOnlyList<string> Describe(string? repoId = null)
    {
        var applies = AppliesTo(repoId);
        return
        [
            $"Worker operational policy version: {policy.Version}",
            $"Policy scope: {(applies ? "repo-local override active" : "platform defaults only")}",
            $"Preferred backend: {policy.PreferredBackendId ?? "(none)"}",
            $"Preferred trust profile: {policy.PreferredTrustProfileId}",
            $"Approval allow/review/deny: {policy.Approval.AutoAllowCategories.Count}/{policy.Approval.ForceReviewCategories.Count}/{policy.Approval.AutoDenyCategories.Count}",
            $"Recovery retries/backoffs: max={policy.Recovery.MaxRetryCount}; transient={policy.Recovery.TransientInfraBackoffSeconds}s; timeout={policy.Recovery.TimeoutBackoffSeconds}s; invalid={policy.Recovery.InvalidOutputBackoffSeconds}s; rebuild={policy.Recovery.EnvironmentRebuildBackoffSeconds}s",
            $"Observability: degraded_latency={policy.Observability.ProviderDegradedLatencyMs}ms; approval_preview={policy.Observability.ApprovalQueuePreviewLimit}; blocked_preview={policy.Observability.BlockedQueuePreviewLimit}; incidents={policy.Observability.IncidentPreviewLimit}; report_window={policy.Observability.GovernanceReportDefaultHours}h",
        ];
    }
}
