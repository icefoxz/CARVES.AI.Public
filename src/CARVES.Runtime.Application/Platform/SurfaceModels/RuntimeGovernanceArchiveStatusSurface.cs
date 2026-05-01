namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeGovernanceArchiveStatusSurface
{
    public string SchemaVersion { get; init; } = "runtime-governance-archive-status.v1";

    public string SurfaceId { get; init; } = RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId;

    public string PrimarySurfaceId { get; init; } = RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId;

    public string SurfaceRole { get; init; } = "primary";

    public string? SuccessorSurfaceId { get; init; }

    public string RetirementPosture { get; init; } = "active_primary";

    public string? LegacyArgument { get; init; }

    public string Summary { get; init; } = string.Empty;

    public int DefaultVisibleSurfaceCount { get; init; }

    public int DefaultVisibleBudget { get; init; }

    public int PrimarySurfaceCount { get; init; }

    public int CompatibilityAliasCount { get; init; }

    public IReadOnlyDictionary<string, int> CompatibilityClassCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public RuntimeGovernanceArchiveOutputBudgetSurface OutputBudget { get; init; } = new();

    public IReadOnlyList<RuntimeGovernanceArchiveAliasSurface> LegacyAliases { get; init; } = [];

    public IReadOnlyList<RuntimeGovernanceArchiveExpansionPointerSurface> ExpansionPointers { get; init; } = [];

    public RuntimeGovernanceArchiveConsumerInventorySurface ConsumerInventory { get; init; } = new();

    public IReadOnlyList<string> NonClaims { get; init; } = [];

    public bool IsValid { get; init; } = true;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class RuntimeGovernanceArchiveOutputBudgetSurface
{
    public int InspectMaxLines { get; init; } = 30;

    public int AliasEntryCount { get; init; }

    public bool HistoricalDocumentBodiesEmbedded { get; init; }

    public int MaxSampleReferencePathsPerSurface { get; init; } = 5;
}

public sealed class RuntimeGovernanceArchiveAliasSurface
{
    public string SurfaceId { get; init; } = string.Empty;

    public string SurfaceRole { get; init; } = "compatibility_alias";

    public string SuccessorSurfaceId { get; init; } = RuntimeGovernanceArchiveStatusIds.PrimarySurfaceId;

    public string RetirementPosture { get; init; } = "alias_retained";

    public string ContextTier { get; init; } = string.Empty;

    public string DefaultVisibility { get; init; } = string.Empty;

    public string InspectUsage { get; init; } = string.Empty;

    public string ApiUsage { get; init; } = string.Empty;

    public string DetailExpansionCommand { get; init; } = string.Empty;

    public bool ExactInvocationPreserved { get; init; } = true;

    public string CompatibilityClass { get; init; } = RuntimeGovernanceArchiveCompatibilityClasses.BlockedUnknownConsumer;

    public string CompatibilityDecision { get; init; } = string.Empty;

    public IReadOnlyList<string> LegacyApiFields { get; init; } = [];

    public int CommandReferenceCount { get; init; }

    public int JsonFieldConsumerReferenceCount { get; init; }

    public IReadOnlyList<string> CompatibilityEvidenceRefs { get; init; } = [];

    public IReadOnlyList<string> CommandReferenceEvidenceRefs { get; init; } = [];

    public IReadOnlyList<string> JsonFieldConsumerEvidenceRefs { get; init; } = [];
}

public sealed class RuntimeGovernanceArchiveExpansionPointerSurface
{
    public string SurfaceId { get; init; } = string.Empty;

    public string ReadMode { get; init; } = "explicit_detail_only";

    public string Path { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}

public sealed class RuntimeGovernanceArchiveConsumerInventorySurface
{
    public IReadOnlyList<string> ScannedRoots { get; init; } = [];

    public int ScannedFileCount { get; init; }

    public int SkippedFileCount { get; init; }

    public int ReferenceFileCount { get; init; }

    public IReadOnlyDictionary<string, int> ReferenceCountsBySurface { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> SampleReferencePathsBySurface { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> JsonFieldConsumerCountsBySurface { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IReadOnlyList<string>> SampleJsonFieldConsumerRefsBySurface { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public static class RuntimeGovernanceArchiveCompatibilityClasses
{
    public const string NameCompatOnly = "name_compat_only";

    public const string ShapeCompatRequired = "shape_compat_required";

    public const string HumanInspectOnly = "human_inspect_only";

    public const string BlockedUnknownConsumer = "blocked_unknown_consumer";

    public static readonly string[] All =
    [
        NameCompatOnly,
        ShapeCompatRequired,
        HumanInspectOnly,
        BlockedUnknownConsumer,
    ];
}

public static class RuntimeGovernanceArchiveStatusIds
{
    public const string PrimarySurfaceId = "runtime-governance-archive-status";

    public static readonly string[] LegacySurfaceIds =
    [
        "runtime-governance-program-reaudit",
        "runtime-hotspot-backlog-drain",
        "runtime-hotspot-cross-family-patterns",
        "runtime-packaging-proof-federation-maturity",
        "runtime-controlled-governance-proof",
        "runtime-validationlab-proof-handoff",
    ];
}
