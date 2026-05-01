using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeAgentGovernanceKernelSurface
{
    public string SchemaVersion { get; init; } = "runtime-agent-governance-kernel.v1";

    public string SurfaceId { get; init; } = "runtime-agent-governance-kernel";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PolicyPath { get; init; } = string.Empty;

    public AgentGovernanceKernelPolicy Policy { get; init; } = new();
}

public sealed class AgentGovernanceKernelPolicy
{
    public string SchemaVersion { get; init; } = "agent-governance-kernel.v1";

    public string PolicyId { get; init; } = "agent-governance-kernel";

    public int PolicyVersion { get; init; } = 3;

    public string Summary { get; init; } = string.Empty;

    public string[] GovernedEntryOrder { get; init; } = [];

    public string[] LifecycleClasses { get; init; } = [];

    public string[] CommitClasses { get; init; } = [];

    public string[] CorrectActions { get; init; } = [];

    public string[] HostRoutedActions { get; init; } = [];

    public AgentGovernancePathFamily[] PathFamilies { get; init; } = [];

    public AgentGovernanceMixedRoot[] MixedRoots { get; init; } = [];

    public string[] GovernanceBoundaryDocPatterns { get; init; } = [];

    public AgentGovernanceDefaultBehavior UnclassifiedDefault { get; init; } = new();

    public AgentGovernanceInitializationContract InitializationContract { get; init; } = new();

    public AgentGovernanceAppliedGovernanceContract AppliedGovernanceContract { get; init; } = new();

    public AgentGovernanceBootstrapPacketContract BootstrapPacketContract { get; init; } = new();

    public AgentGovernanceWarmResumeContract WarmResumeContract { get; init; } = new();

    public AgentGovernanceTaskOverlayContract TaskOverlayContract { get; init; } = new();

    public AgentGovernanceModelProfile[] ModelProfiles { get; init; } = [];

    public AgentGovernanceLoopStallGuardPolicy LoopStallGuardPolicy { get; init; } = new();

    public AgentGovernanceWeakExecutionLane[] WeakExecutionLanes { get; init; } = [];

    public string[] NotYetProven { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class AgentGovernancePathFamily
{
    public string FamilyId { get; init; } = string.Empty;

    public string[] PathPatterns { get; init; } = [];

    public string LifecycleClass { get; init; } = string.Empty;

    public string CommitClass { get; init; } = string.Empty;

    public string CorrectAction { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] Notes { get; init; } = [];
}

public sealed class AgentGovernanceMixedRoot
{
    public string RootPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] ChildExceptions { get; init; } = [];
}

public sealed class AgentGovernanceDefaultBehavior
{
    public string LifecycleClass { get; init; } = string.Empty;

    public string CommitClass { get; init; } = string.Empty;

    public string CorrectAction { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class AgentGovernanceInitializationContract
{
    public string ReportHeading { get; init; } = "CARVES.AI initialization report";

    public string SourcesHeading { get; init; } = "Agent bootstrap sources";

    public string[] RequiredFields { get; init; } = [];

    public string[] SourceFields { get; init; } = [];

    public string EntryPathShape { get; init; } = string.Empty;

    public string RuntimeStateShape { get; init; } = string.Empty;
}

public sealed class AgentGovernanceAppliedGovernanceContract
{
    public string JudgmentHeading { get; init; } = "CARVES.AI mixed diff judgment";

    public string[] TableColumns { get; init; } = [];

    public string[] VerdictFields { get; init; } = [];

    public string[] AutomaticFailConditions { get; init; } = [];
}

public sealed class AgentGovernanceBootstrapPacketContract
{
    public string SurfaceId { get; init; } = "runtime-agent-bootstrap-packet";

    public string Summary { get; init; } = string.Empty;

    public string StartupMode { get; init; } = "bootstrap_packet_default";

    public string[] RequiredPacketFields { get; init; } = [];

    public string[] DefaultInspectCommands { get; init; } = [];

    public string[] OptionalDeepReadCommands { get; init; } = [];
}

public sealed class AgentGovernanceWarmResumeContract
{
    public string SurfaceId { get; init; } = "runtime-agent-bootstrap-receipt";

    public string Summary { get; init; } = string.Empty;

    public string[] ResumeModes { get; init; } = [];

    public string[] RequiredReceiptFields { get; init; } = [];

    public string[] DefaultInspectCommands { get; init; } = [];

    public string[] WarmResumeChecks { get; init; } = [];

    public string[] InvalidationTriggers { get; init; } = [];

    public string[] ColdInitTriggers { get; init; } = [];
}

public sealed class AgentGovernanceTaskOverlayContract
{
    public string SurfaceId { get; init; } = "runtime-agent-task-overlay";

    public string Summary { get; init; } = string.Empty;

    public string[] RequiredOverlayFields { get; init; } = [];

    public string[] DefaultInspectCommands { get; init; } = [];

    public string[] SourceTruthRefs { get; init; } = [];
}

public sealed class AgentGovernanceModelProfile
{
    public string ProfileId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public int MaxStartupSources { get; init; }

    public string GovernanceCeiling { get; init; } = string.Empty;

    public bool RequiresTaskOverlay { get; init; }

    public bool DeepGovernanceReady { get; init; }

    public string[] PreferredBackendIds { get; init; } = [];

    public string[] StartupSurfaces { get; init; } = [];

    public string[] AllowedActions { get; init; } = [];

    public string[] ForbiddenActions { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class AgentGovernanceLoopStallGuardPolicy
{
    public int DetectorWindow { get; init; } = 5;

    public string DetectionLineage { get; init; } = string.Empty;

    public string[] StrictProfileIds { get; init; } = [];

    public AgentGovernanceLoopStallRule[] Rules { get; init; } = [];
}

public sealed class AgentGovernanceLoopStallRule
{
    public string PatternType { get; init; } = string.Empty;

    public string StandardOutcome { get; init; } = string.Empty;

    public string WeakOutcome { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class AgentGovernanceWeakExecutionLane
{
    public string LaneId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ModelProfileId { get; init; } = "weak";

    public string[] AllowedTaskTypes { get; init; } = [];

    public string[] RequiredStartupSurfaces { get; init; } = [];

    public string[] RequiredRuntimeSurfaces { get; init; } = [];

    public string[] RequiredVerification { get; init; } = [];

    public string[] ForbiddenCapabilities { get; init; } = [];

    public AgentGovernanceScopeCeiling ScopeCeiling { get; init; } = new();

    public string[] StopConditions { get; init; } = [];
}

public sealed class AgentGovernanceScopeCeiling
{
    public int MaxScopeEntries { get; init; }

    public int MaxRelevantFiles { get; init; }

    public int MaxFilesChanged { get; init; }

    public int MaxLinesChanged { get; init; }
}
