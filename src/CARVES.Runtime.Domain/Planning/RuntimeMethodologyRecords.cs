namespace Carves.Runtime.Domain.Planning;

public enum RuntimeMethodologyCoverageStatus
{
    NotApplicable,
    NewDelta,
    CoveredCompletedLineage,
    DeferredLineageResume,
    MissingAcknowledgment,
}

public sealed record RuntimeMethodologyAssessment
{
    public bool Applies { get; init; }

    public bool Acknowledged { get; init; }

    public string ReferencePath { get; init; } = string.Empty;

    public RuntimeMethodologyCoverageStatus CoverageStatus { get; init; } = RuntimeMethodologyCoverageStatus.NotApplicable;

    public IReadOnlyList<string> RelatedCardIds { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;

    public string RecommendedAction { get; init; } = string.Empty;
}

public sealed record DeferredLineageResumeStep
{
    public int Order { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> CardIds { get; init; } = Array.Empty<string>();

    public string ResumeReason { get; init; } = string.Empty;
}

public sealed record DeferredLineageResumeGate
{
    public string SchemaVersion { get; init; } = "async-multi-worker-resume-gate.v1";

    public string ReferencePath { get; init; } = ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md";

    public IReadOnlyList<string> Preconditions { get; init; } = Array.Empty<string>();

    public IReadOnlyList<DeferredLineageResumeStep> ResumeOrder { get; init; } = Array.Empty<DeferredLineageResumeStep>();

    public IReadOnlyList<string> AntiDuplicationRules { get; init; } = Array.Empty<string>();
}
