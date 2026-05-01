using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Application.Workers;

namespace Carves.Runtime.Application.Platform;

public sealed class PlatformGovernanceService
{
    private readonly IPlatformGovernanceRepository repository;

    public PlatformGovernanceService(IPlatformGovernanceRepository repository)
    {
        this.repository = repository;
    }

    public PlatformGovernanceSnapshot GetSnapshot()
    {
        var snapshot = repository.Load();
        if (snapshot.RepoPolicies.Count == 0)
        {
            snapshot = new PlatformGovernanceSnapshot
            {
                PlatformPolicy = new PlatformPolicy
                {
                    PolicyId = "default-platform",
                    MaxActiveSessions = 4,
                    MaxWorkerNodes = 4,
                    ProviderQuotaPerHour = 100,
                    FairSchedulingEnabled = true,
                    MaxRepoSelectionsPerTick = 2,
                    StarvationPreventionMinutes = 30,
                },
                RepoPolicies =
                [
                    new RepoPolicy
                    {
                        ProfileId = "balanced",
                        MaxPlannerRounds = 3,
                        MaxGeneratedTasks = 20,
                        MaxConcurrentExecutions = 4,
                        RuntimeSelectionPriority = 10,
                        StarvationWindowMinutes = 30,
                        AllowAutonomousRefactor = true,
                        AllowAutonomousMemoryUpdate = true,
                        ManualApprovalMode = false,
                        ProviderPolicyProfile = "default-provider",
                        WorkerPolicyProfile = "trusted-dotnet-only",
                        PreferredTrustProfileId = "workspace_build_test",
                        ReviewPolicyProfile = "manual-on-architecture-change",
                    },
                ],
                ProviderPolicies =
                [
                    new ProviderPolicy
                    {
                        PolicyId = "default-provider",
                        AllowedProviderProfiles = ["default", "planner-high-context", "claude-planner-high-context", "claude-worker-bounded", "gemini-planner-high-context", "worker-codegen-fast", "review-structured", "codex-worker-trusted", "codex-worker-local-cli", "gemini-worker-balanced", "local-agent-worker"],
                        AllowCodeGeneration = true,
                        AllowPlanning = true,
                        AllowedRepoScopes = ["*"],
                        AllowFallbackProfiles = true,
                        FallbackProviderProfiles = ["default", "planner-high-context", "claude-planner-high-context", "claude-worker-bounded", "gemini-planner-high-context", "worker-codegen-fast", "codex-worker-trusted", "codex-worker-local-cli", "gemini-worker-balanced", "local-agent-worker"],
                    },
                ],
                WorkerPolicies =
                [
                    BuildDefaultWorkerPolicy(),
                ],
                ReviewPolicies =
                [
                    new ReviewPolicy
                    {
                        PolicyId = "manual-on-architecture-change",
                        ManualOnArchitectureChange = true,
                        ManualOnAutonomousRefactor = true,
                        ManualOnProviderRotation = true,
                    },
                ],
            };
        }

        snapshot = Normalize(snapshot);
        repository.Save(snapshot);
        return snapshot;
    }

    public IReadOnlyList<GovernanceEvent> LoadEvents()
    {
        return repository.LoadEvents().OrderByDescending(item => item.OccurredAt).ToArray();
    }

    public RepoPolicy ResolveRepoPolicy(string profileId)
    {
        return GetSnapshot().RepoPolicies.First(policy => string.Equals(policy.ProfileId, profileId, StringComparison.Ordinal));
    }

    public ProviderPolicy ResolveProviderPolicy(string policyId)
    {
        return GetSnapshot().ProviderPolicies.First(policy => string.Equals(policy.PolicyId, policyId, StringComparison.Ordinal));
    }

    public WorkerPolicy ResolveWorkerPolicy(string policyId)
    {
        return GetSnapshot().WorkerPolicies.First(policy => string.Equals(policy.PolicyId, policyId, StringComparison.Ordinal));
    }

    public ReviewPolicy ResolveReviewPolicy(string policyId)
    {
        return GetSnapshot().ReviewPolicies.First(policy => string.Equals(policy.PolicyId, policyId, StringComparison.Ordinal));
    }

    public void RecordEvent(GovernanceEventType eventType, string repoId, string message)
    {
        var events = repository.LoadEvents().ToList();
        events.Add(new GovernanceEvent
        {
            EventId = $"{eventType}-{Guid.NewGuid():N}",
            EventType = eventType,
            RepoId = repoId,
            Message = message,
        });
        repository.SaveEvents(events.OrderByDescending(item => item.OccurredAt).ToArray());
    }

    private static PlatformGovernanceSnapshot Normalize(PlatformGovernanceSnapshot snapshot)
    {
        var workerPolicies = snapshot.WorkerPolicies.Count == 0
            ? [BuildDefaultWorkerPolicy()]
            : snapshot.WorkerPolicies
                .Select(policy => string.Equals(policy.PolicyId, "trusted-dotnet-only", StringComparison.Ordinal)
                    ? new WorkerPolicy
                    {
                        PolicyId = policy.PolicyId,
                        MaxConcurrentTasks = policy.MaxConcurrentTasks,
                        RequireTrustedNodes = policy.RequireTrustedNodes,
                        AllowedRepoScopes = policy.AllowedRepoScopes,
                        DefaultProfileId = string.IsNullOrWhiteSpace(policy.DefaultProfileId) ? "workspace_build_test" : policy.DefaultProfileId,
                        Profiles = policy.Profiles.Count == 0 ? WorkerExecutionBoundaryService.DefaultProfiles() : policy.Profiles,
                    }
                    : policy)
                .ToArray();

        var repoPolicies = snapshot.RepoPolicies
            .Select(policy => string.Equals(policy.ProfileId, "balanced", StringComparison.Ordinal)
                ? new RepoPolicy
                {
                    ProfileId = policy.ProfileId,
                    MaxPlannerRounds = policy.MaxPlannerRounds,
                    MaxGeneratedTasks = policy.MaxGeneratedTasks,
                    MaxConcurrentExecutions = policy.MaxConcurrentExecutions,
                    RuntimeSelectionPriority = policy.RuntimeSelectionPriority,
                    StarvationWindowMinutes = policy.StarvationWindowMinutes,
                    AllowAutonomousRefactor = policy.AllowAutonomousRefactor,
                    AllowAutonomousMemoryUpdate = policy.AllowAutonomousMemoryUpdate,
                    ManualApprovalMode = policy.ManualApprovalMode,
                    ProviderPolicyProfile = policy.ProviderPolicyProfile,
                    WorkerPolicyProfile = policy.WorkerPolicyProfile,
                    PreferredTrustProfileId = string.IsNullOrWhiteSpace(policy.PreferredTrustProfileId) ? "workspace_build_test" : policy.PreferredTrustProfileId,
                    ReviewPolicyProfile = policy.ReviewPolicyProfile,
                }
                : policy)
            .ToArray();

        var providerPolicies = snapshot.ProviderPolicies
            .Select(policy => string.Equals(policy.PolicyId, "default-provider", StringComparison.Ordinal)
                ? new ProviderPolicy
                {
                    PolicyId = policy.PolicyId,
                    AllowedProviderProfiles = policy.AllowedProviderProfiles
                        .Concat(["claude-worker-bounded", "codex-worker-trusted", "codex-worker-local-cli", "gemini-worker-balanced", "local-agent-worker"])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    AllowCodeGeneration = policy.AllowCodeGeneration,
                    AllowPlanning = policy.AllowPlanning,
                    AllowedRepoScopes = policy.AllowedRepoScopes,
                    AllowFallbackProfiles = policy.AllowFallbackProfiles,
                    FallbackProviderProfiles = policy.FallbackProviderProfiles
                        .Concat(["claude-worker-bounded", "codex-worker-trusted", "codex-worker-local-cli", "gemini-worker-balanced", "local-agent-worker"])
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                }
                : policy)
            .ToArray();

        return new PlatformGovernanceSnapshot
        {
            Version = snapshot.Version,
            UpdatedAt = snapshot.UpdatedAt,
            PlatformPolicy = snapshot.PlatformPolicy,
            RepoPolicies = repoPolicies,
            ProviderPolicies = providerPolicies,
            WorkerPolicies = workerPolicies,
            ReviewPolicies = snapshot.ReviewPolicies,
        };
    }

    private static WorkerPolicy BuildDefaultWorkerPolicy()
    {
        return new WorkerPolicy
        {
            PolicyId = "trusted-dotnet-only",
            MaxConcurrentTasks = 4,
            RequireTrustedNodes = true,
            AllowedRepoScopes = ["*"],
            DefaultProfileId = "workspace_build_test",
            Profiles = WorkerExecutionBoundaryService.DefaultProfiles(),
        };
    }
}
