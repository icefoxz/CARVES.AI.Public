namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeExternalConsumerResourcePackSurface
{
    public string SchemaVersion { get; init; } = "runtime-external-consumer-resource-pack.v1";

    public string SurfaceId { get; init; } = "runtime-external-consumer-resource-pack";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ProductClosurePhase { get; init; } = RuntimeProductClosureMetadata.CurrentPhase;

    public string PhaseDocumentPath { get; init; } = string.Empty;

    public string PreviousPhaseDocumentPath { get; init; } = string.Empty;

    public string ResourcePackGuideDocumentPath { get; init; } = string.Empty;

    public string RuntimeDocumentRoot { get; init; } = string.Empty;

    public string RuntimeDocumentRootMode { get; init; } = "repo_local";

    public string RepoRoot { get; init; } = string.Empty;

    public string OverallPosture { get; init; } = string.Empty;

    public string CommandEntry { get; init; } = "carves pilot resources";

    public string JsonCommandEntry { get; init; } = "carves pilot resources --json";

    public string InspectCommandEntry { get; init; } = "carves inspect runtime-external-consumer-resource-pack";

    public string ApiCommandEntry { get; init; } = "carves api runtime-external-consumer-resource-pack";

    public bool ResourcePackComplete { get; init; }

    public int RuntimeOwnedResourceCount { get; init; }

    public int TargetGeneratedResourceCount { get; init; }

    public int CommandEntryCount { get; init; }

    public IReadOnlyList<RuntimeExternalConsumerResourceSurface> RuntimeOwnedResources { get; init; } = [];

    public IReadOnlyList<RuntimeExternalConsumerGeneratedResourceSurface> TargetGeneratedResources { get; init; } = [];

    public IReadOnlyList<RuntimeExternalConsumerCommandEntrySurface> CommandEntries { get; init; } = [];

    public IReadOnlyList<string> BoundaryRules { get; init; } = [];

    public IReadOnlyList<string> RequiredReadbackCommands { get; init; } = [];

    public IReadOnlyList<string> Gaps { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string RecommendedNextAction { get; init; } = string.Empty;

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> NonClaims { get; init; } = [];
}

public sealed class RuntimeExternalConsumerResourceSurface
{
    public string Path { get; init; } = string.Empty;

    public string ResourceClass { get; init; } = string.Empty;

    public string ConsumerUse { get; init; } = string.Empty;

    public string Boundary { get; init; } = string.Empty;
}

public sealed class RuntimeExternalConsumerGeneratedResourceSurface
{
    public string Path { get; init; } = string.Empty;

    public string ResourceClass { get; init; } = string.Empty;

    public string MaterializationCommand { get; init; } = string.Empty;

    public string Boundary { get; init; } = string.Empty;
}

public sealed class RuntimeExternalConsumerCommandEntrySurface
{
    public string Command { get; init; } = string.Empty;

    public string SurfaceId { get; init; } = string.Empty;

    public string AuthorityClass { get; init; } = string.Empty;

    public string ConsumerUse { get; init; } = string.Empty;
}
