namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeGovernanceSurfaceCoverageAuditSurface
{
    public string SchemaVersion { get; init; } = "runtime-governance-surface-coverage-audit.v1";

    public string SurfaceId { get; init; } = "runtime-governance-surface-coverage-audit";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public bool CoverageComplete { get; init; }

    public bool LifecycleBudgetComplete { get; init; }

    public int RegisteredSurfaceCount { get; init; }

    public int RequiredSurfaceCount { get; init; }

    public int MaxGovernanceCriticalSurfaceCount { get; init; }

    public int CoveredSurfaceCount { get; init; }

    public int DefaultPathSurfaceCount { get; init; }

    public int MaxDefaultPathSurfaceCount { get; init; }

    public int AuditHandoffSurfaceCount { get; init; }

    public int MaxAuditHandoffSurfaceCount { get; init; }

    public int BlockingGapCount { get; init; }

    public int AdvisoryGapCount { get; init; }

    public IReadOnlyList<string> CoverageDimensions { get; init; } = [];

    public IReadOnlyList<string> RequiredSurfaces { get; init; } = [];

    public IReadOnlyList<RuntimeGovernanceSurfaceCoverageEntry> Entries { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public IReadOnlyList<string> AdvisoryGaps { get; init; } = [];

    public IReadOnlyList<string> LifecycleBudgetGaps { get; init; } = [];

    public IReadOnlyList<string> EvidenceSourcePaths { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeGovernanceSurfaceCoverageEntry
{
    public string SurfaceId { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string LifecycleClass { get; init; } = string.Empty;

    public string ReadPathClass { get; init; } = string.Empty;

    public string DefaultPathParticipation { get; init; } = string.Empty;

    public bool CountsTowardDefaultPathBudget { get; init; }

    public string LifecycleBudgetStatus { get; init; } = string.Empty;

    public bool ExternalAgentVisible { get; init; }

    public bool RegistryRequired { get; init; } = true;

    public bool RegistryRegistered { get; init; }

    public bool InspectUsageExposed { get; init; }

    public bool ApiUsageExposed { get; init; }

    public bool ResourcePackRequired { get; init; }

    public bool ResourcePackCovered { get; init; }

    public string ResourcePackNeedle { get; init; } = "N/A";

    public bool QuickstartRequired { get; init; }

    public bool QuickstartDocumented { get; init; }

    public string QuickstartNeedle { get; init; } = "N/A";

    public bool ConsumerPackRequired { get; init; }

    public bool ConsumerPackDocumented { get; init; }

    public string ConsumerPackNeedle { get; init; } = "N/A";

    public bool HostContractRequired { get; init; } = true;

    public bool HostContractCovered { get; init; }

    public string HostContractEvidencePath { get; init; } = "N/A";

    public string CoverageStatus { get; init; } = string.Empty;

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public IReadOnlyList<string> AdvisoryGaps { get; init; } = [];

    public IReadOnlyList<string> Evidence { get; init; } = [];
}
