namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentDeliveryReadinessSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-delivery-readiness.v1";

    public string SurfaceId { get; init; } = "runtime-agent-delivery-readiness";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string BoundaryDocumentPath { get; init; } = string.Empty;

    public string GuidePath { get; init; } = string.Empty;

    public string PackagingMaturityPath { get; init; } = string.Empty;

    public string FirstRunPacketPath { get; init; } = string.Empty;

    public string ValidationBundleGuidePath { get; init; } = string.Empty;

    public string TrialWrapperPath { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string DeliveryOwnership { get; init; } = "runtime_owned_source_tree_trial_entry";

    public string EntryLaneId { get; init; } = "resident_host_attach_first_run_validation_bundle";

    public IReadOnlyList<string> EntryCommands { get; init; } = [];

    public IReadOnlyList<string> RuntimeTruthFiles { get; init; } = [];

    public IReadOnlyList<string> DerivedPackagingArtifacts { get; init; } = [];

    public IReadOnlyList<string> RelatedSurfaceRefs { get; init; } = [];

    public IReadOnlyList<string> DeliveryClaims { get; init; } = [];

    public IReadOnlyList<string> BlockedClaims { get; init; } = [];

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
