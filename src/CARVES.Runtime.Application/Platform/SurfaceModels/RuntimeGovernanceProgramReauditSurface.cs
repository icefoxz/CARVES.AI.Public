using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeGovernanceProgramReauditSurface
{
    public string SchemaVersion { get; init; } = "runtime-governance-program-reaudit.v1";

    public string SurfaceId { get; init; } = "runtime-governance-program-reaudit";

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string HotspotDrainSurfaceId { get; init; } = "runtime-hotspot-backlog-drain";

    public string CrossFamilyPatternSurfaceId { get; init; } = "runtime-hotspot-cross-family-patterns";

    public string ControlledGovernanceProofSurfaceId { get; init; } = "runtime-controlled-governance-proof";

    public string PackagingProofFederationMaturitySurfaceId { get; init; } = "runtime-packaging-proof-federation-maturity";

    public string SustainabilityAuditPath { get; init; } = string.Empty;

    public string ArchiveReadinessPath { get; init; } = string.Empty;

    public string ClosureReviewPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string OverallVerdict { get; init; } = string.Empty;

    public string ContinuationGateOutcome { get; init; } = string.Empty;

    public string ClosureDeltaPosture { get; init; } = string.Empty;

    public string ClosureReviewOutcome { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public DateTimeOffset? QueueSnapshotGeneratedAt { get; init; }

    public DateTimeOffset? SustainabilityGeneratedAt { get; init; }

    public DateTimeOffset? ArchiveReadinessGeneratedAt { get; init; }

    public DateTimeOffset? ClosureReviewRecordedAt { get; init; }

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public RuntimeGovernanceProgramReauditCountsSurface Counts { get; init; } = new();

    public IReadOnlyList<RuntimeGovernanceProgramReauditCriterionSurface> Criteria { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeGovernanceProgramReauditCountsSurface
{
    public int QueueFamilyCount { get; init; }

    public int ClearedQueueCount { get; init; }

    public int AcceptedResidualQueueCount { get; init; }

    public int GovernedCompletedQueueCount { get; init; }

    public int ResidualCompletedQueueCount { get; init; }

    public int ResidualOpenQueueCount { get; init; }

    public int ContinuedQueueCount { get; init; }

    public int OpenBacklogItems { get; init; }

    public int SuggestedBacklogItems { get; init; }

    public int ResolvedBacklogItems { get; init; }

    public int ClosureBlockingBacklogItems { get; init; }

    public int NonBlockingBacklogItems { get; init; }

    public int UnselectedBacklogItems { get; init; }

    public int UnselectedClosureRelevantBacklogItems { get; init; }

    public int UnselectedMaintenanceNoiseBacklogItems { get; init; }

    public int PatternCount { get; init; }

    public int ResidualPatternCount { get; init; }

    public int RepeatedBacklogKindPatternCount { get; init; }

    public int ValidationOverlapPatternCount { get; init; }

    public int SharedBoundaryCategoryCount { get; init; }

    public int ProofLaneCount { get; init; }

    public int PackagingProfileCount { get; init; }

    public int ClosedCapabilityCount { get; init; }

    public bool SustainabilityAuditAvailable { get; init; }

    public int? SustainabilityAuditAgeDays { get; init; }

    public string SustainabilityAuditFreshness { get; init; } = "missing";

    public bool SustainabilityStrictPassed { get; init; }

    public int SustainabilityFindingCount { get; init; }

    public int SustainabilityErrorCount { get; init; }

    public int SustainabilityWarningCount { get; init; }

    public bool ArchiveReadinessAvailable { get; init; }

    public int? ArchiveReadinessAgeDays { get; init; }

    public string ArchiveReadinessFreshness { get; init; } = "missing";

    public int ArchiveFamilyCount { get; init; }

    public int PromotionRelevantArchivedEntryCount { get; init; }
}

public sealed class RuntimeGovernanceProgramReauditCriterionSurface
{
    public string CriterionId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> EvidenceRefs { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
