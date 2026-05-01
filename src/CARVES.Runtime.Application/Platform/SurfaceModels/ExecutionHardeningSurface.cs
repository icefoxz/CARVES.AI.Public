using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class ExecutionHardeningSurfaceSnapshot
{
    public string SchemaVersion { get; init; } = "execution-hardening-surface.v1";

    public string SurfaceId { get; init; } = "execution-hardening";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string TaskId { get; init; } = string.Empty;

    public string CardId { get; init; } = string.Empty;

    public string ScenarioId { get; init; } = string.Empty;

    public string TaskStatus { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public CodexGovernanceBootstrapSnapshot Governance { get; init; } = new();

    public CodexToolDescriptor[] RelevantTools { get; init; } = [];

    public ExecutionPacketSurfaceSnapshot ExecutionPacket { get; init; } = new();

    public PacketEnforcementSurfaceSnapshot PacketEnforcement { get; init; } = new();

    public AuthoritativeTruthStoreSurface AuthoritativeTruth { get; init; } = new();

    public string ReviewStatus { get; init; } = "not_reviewed";

    public ExecutionHardeningNegativePath[] NegativePaths { get; init; } = [];

    public string[] InspectCommands { get; init; } = [];
}

public sealed class CodexGovernanceBootstrapSnapshot
{
    public bool BootstrapReady { get; init; }

    public string Summary { get; init; } = string.Empty;

    public CodexGovernanceAssetStatus[] Assets { get; init; } = [];

    public string[] MustCallActions { get; init; } = [];
}

public sealed class CodexGovernanceAssetStatus
{
    public string AssetId { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class ExecutionHardeningNegativePath
{
    public string PathId { get; init; } = string.Empty;

    public bool ExplicitVerdictSupported { get; init; }

    public string CurrentStatus { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] EvidenceCommands { get; init; } = [];

    public string[] ReasonCodes { get; init; } = [];
}
