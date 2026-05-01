namespace Carves.Runtime.Domain.Execution;

public sealed record RuntimePackExecutionAttribution
{
    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string ArtifactRef { get; init; } = string.Empty;

    public string PolicyPreset { get; init; } = string.Empty;

    public string GatePreset { get; init; } = string.Empty;

    public string ValidatorProfile { get; init; } = string.Empty;

    public string RoutingProfile { get; init; } = string.Empty;

    public string EnvironmentProfile { get; init; } = string.Empty;

    public string SelectionMode { get; init; } = string.Empty;

    public DateTimeOffset SelectedAtUtc { get; init; }

    public RuntimePackDeclarativeContribution? DeclarativeContribution { get; init; }
}

public sealed record RuntimePackDeclarativeContribution
{
    public string ManifestPath { get; init; } = string.Empty;

    public string ContributionFingerprint { get; init; } = string.Empty;

    public IReadOnlyList<string> CapabilityKinds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ProjectUnderstandingRecipeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> VerificationRecipeIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> VerificationCommandIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ReviewRubricIds { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> ReviewChecklistItemIds { get; init; } = Array.Empty<string>();

    public string Summary { get; init; } = string.Empty;
}
