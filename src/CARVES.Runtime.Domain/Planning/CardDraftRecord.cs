using System.Text.Json.Serialization;

namespace Carves.Runtime.Domain.Planning;

public sealed class CardDraftRecord
{
    public string DraftId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public IReadOnlyList<string> Acceptance { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scope { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Notes { get; init; } = Array.Empty<string>();

    public AcceptanceContract? AcceptanceContract { get; init; }

    public CardRealityModel? RealityModel { get; init; }

    [JsonPropertyName("planning_lineage")]
    public PlanningLineage? PlanningLineage { get; init; }

    public CardLifecycleState Status { get; set; } = CardLifecycleState.Draft;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }

    public DateTimeOffset? RejectedAtUtc { get; set; }

    public DateTimeOffset? ArchivedAtUtc { get; set; }

    public string? LifecycleReason { get; set; }

    public bool MethodologyRequired { get; init; }

    public bool MethodologyAcknowledged { get; init; }

    public string? MethodologyReferencePath { get; init; }

    public string? MethodologyCoverageStatus { get; init; }

    public IReadOnlyList<string> MethodologyRelatedCards { get; init; } = Array.Empty<string>();

    public string? MethodologySummary { get; init; }

    public string? MethodologyRecommendedAction { get; init; }

    public string MarkdownPath { get; init; } = string.Empty;
}

public sealed class CardRealityModel
{
    public string OuterVision { get; init; } = string.Empty;

    public string CurrentSolidScope { get; init; } = string.Empty;

    public string NextRealSlice { get; init; } = string.Empty;

    public RealityState RealityState { get; init; } = RealityState.Imagined;

    public SolidityClass SolidityClass { get; init; } = SolidityClass.Ghost;

    public RealityProofTarget ProofTarget { get; init; } = new();

    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();

    public IllusionRiskModel IllusionRisk { get; init; } = new();

    public string PromotionGate { get; init; } = string.Empty;
}

public sealed class RealityProofTarget
{
    public ProofTargetKind Kind { get; init; } = ProofTargetKind.Boundary;

    public string Description { get; init; } = string.Empty;
}

public sealed class IllusionRiskModel
{
    public IllusionRiskLevel Level { get; init; } = IllusionRiskLevel.Low;

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}

public enum RealityState
{
    Imagined = 0,
    Bounded = 1,
    SkeletonProven = 2,
    CompileProven = 3,
    SliceProven = 4,
    ReintegrationProven = 5,
    SecondaryConsumerProven = 6,
    Operational = 7,
}

public enum SolidityClass
{
    Ghost = 0,
    Proto = 1,
    Solid = 2,
}

public enum ProofTargetKind
{
    Boundary = 0,
    Skeleton = 1,
    Compile = 2,
    Smoke = 3,
    FocusedBehavior = 4,
    Reintegration = 5,
    CleanConsumer = 6,
    SecondaryConsumer = 7,
    Operational = 8,
}

public enum IllusionRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3,
}
