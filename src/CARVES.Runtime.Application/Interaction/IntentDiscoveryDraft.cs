using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Interaction;

public sealed class IntentDiscoveryDraft
{
    public int SchemaVersion { get; init; } = 2;

    public string DraftId { get; init; } = $"intent-draft-{Guid.NewGuid():N}";

    public string RepoRoot { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public IReadOnlyList<string> Users { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> CoreCapabilities { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> TechnologyScope { get; init; } = Array.Empty<string>();

    public string SourceSummary { get; init; } = string.Empty;

    public string SuggestedMarkdown { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public GuidedPlanningPosture PlanningPosture { get; init; } = GuidedPlanningPosture.NeedsConfirmation;

    public FormalPlanningState FormalPlanningState { get; init; } = FormalPlanningState.Discuss;

    public string? FocusCardId { get; init; }

    public GuidedPlanningScopeFrame ScopeFrame { get; init; } = new();

    public IReadOnlyList<GuidedPlanningPendingDecision> PendingDecisions { get; init; } = Array.Empty<GuidedPlanningPendingDecision>();

    public IReadOnlyList<GuidedPlanningCandidateCard> CandidateCards { get; init; } = Array.Empty<GuidedPlanningCandidateCard>();

    public ActivePlanningCard? ActivePlanningCard { get; init; }

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum GuidedPlanningPosture
{
    Emerging,
    NeedsConfirmation,
    Wobbling,
    Grounded,
    Paused,
    Forbidden,
    ReadyToPlan,
}

public enum GuidedPlanningDecisionStatus
{
    Open,
    Resolved,
    Paused,
    Forbidden,
}

public sealed class GuidedPlanningScopeFrame
{
    public string Goal { get; init; } = string.Empty;

    public IReadOnlyList<string> FirstUsers { get; init; } = Array.Empty<string>();

    public string ValidationArtifact { get; init; } = string.Empty;

    public IReadOnlyList<string> MustHave { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NiceToHave { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NotNow { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> OpenQuestions { get; init; } = Array.Empty<string>();
}

public sealed class GuidedPlanningPendingDecision
{
    public string DecisionId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string WhyItMatters { get; init; } = string.Empty;

    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    public string CurrentRecommendation { get; init; } = string.Empty;

    public string BlockingLevel { get; init; } = string.Empty;

    public GuidedPlanningDecisionStatus Status { get; init; } = GuidedPlanningDecisionStatus.Open;
}

public sealed class GuidedPlanningCandidateCard
{
    public string CandidateCardId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public GuidedPlanningPosture PlanningPosture { get; init; } = GuidedPlanningPosture.NeedsConfirmation;

    public string WritebackEligibility { get; init; } = string.Empty;

    public IReadOnlyList<string> FocusQuestions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> AllowedUserActions { get; init; } = Array.Empty<string>();
}

public sealed class ActivePlanningCard
{
    public string PlanningCardId { get; init; } = string.Empty;

    public string PlanningSlotId { get; init; } = string.Empty;

    public string SourceIntentDraftId { get; init; } = string.Empty;

    public string? SourceCandidateCardId { get; init; }

    public FormalPlanningState State { get; init; } = FormalPlanningState.Planning;

    public ActivePlanningCardLockedDoctrine LockedDoctrine { get; init; } = new();

    public ActivePlanningCardOperatorIntent OperatorIntent { get; init; } = new();

    public ActivePlanningCardAgentProposal AgentProposal { get; init; } = new();

    public ActivePlanningCardSystemDerived SystemDerived { get; init; } = new();

    public DateTimeOffset IssuedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ActivePlanningCardLockedDoctrine
{
    public IReadOnlyList<string> LiteralLines { get; init; } = Array.Empty<string>();

    public string CompareRule { get; init; } = "literal_and_digest";

    public string Digest { get; init; } = string.Empty;
}

public sealed class ActivePlanningCardOperatorIntent
{
    public string Title { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public string ValidationArtifact { get; init; } = string.Empty;

    public IReadOnlyList<string> AcceptanceOutline { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Constraints { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> NonGoals { get; init; } = Array.Empty<string>();
}

public sealed class ActivePlanningCardAgentProposal
{
    public string CandidateSummary { get; init; } = string.Empty;

    public IReadOnlyList<string> DecompositionCandidates { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> OpenQuestions { get; init; } = Array.Empty<string>();

    public string SuggestedNextAction { get; init; } = string.Empty;
}

public sealed class ActivePlanningCardSystemDerived
{
    public IReadOnlyList<PlanningFieldClassRule> FieldClasses { get; init; } = Array.Empty<PlanningFieldClassRule>();

    public string ComparisonPolicySummary { get; init; } = string.Empty;

    public string LockedDoctrineDigest { get; init; } = string.Empty;

    public DateTimeOffset? LastExportedAt { get; init; }

    public string? LastExportedCardPayloadPath { get; init; }
}

public sealed class PlanningFieldClassRule
{
    public string FieldPath { get; init; } = string.Empty;

    public string Ownership { get; init; } = string.Empty;

    public string EditPolicy { get; init; } = string.Empty;

    public string CompareRule { get; init; } = string.Empty;
}
