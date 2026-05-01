using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeProjectionClassBoundarySurface
{
    public string ProjectionClassId { get; init; } = string.Empty;
    public string PrimaryForm { get; init; } = string.Empty;
    public string InspectScope { get; init; } = string.Empty;
    public string RequestExecutionMode { get; init; } = string.Empty;
    public string ApprovalMode { get; init; } = string.Empty;
    public string GovernedMutationMode { get; init; } = string.Empty;
    public string OfficialTruthWriteMode { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeRoleAuthorityBoundarySurface
{
    public string RoleId { get; init; } = string.Empty;
    public IReadOnlyList<string> TypicalProjectionClasses { get; init; } = [];
    public string ObserveAuthority { get; init; } = string.Empty;
    public string ProposeAuthority { get; init; } = string.Empty;
    public string ApproveAuthority { get; init; } = string.Empty;
    public string ExecuteAuthority { get; init; } = string.Empty;
    public string OfficialTruthWriteAuthority { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeClientActionEnvelopeSurface
{
    public string EnvelopeId { get; init; } = string.Empty;
    public string ClientClassId { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public bool HostMediationRequired { get; init; }
    public string RequiredProofSource { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeExternalAgentIngressBoundarySurface
{
    public string ContractId { get; init; } = string.Empty;
    public IReadOnlyList<string> AllowedIngressActions { get; init; } = [];
    public IReadOnlyList<string> BlockedIngressActions { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeSessionContinuityLaneBoundarySurface
{
    public string LaneId { get; init; } = string.Empty;
    public string Trigger { get; init; } = string.Empty;
    public string HostBoundary { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
}

public sealed class RuntimeCentralInteractionMultiDeviceProjectionSurface
{
    public string SchemaVersion { get; init; } = "runtime-central-interaction-multi-device-projection.v1";
    public string SurfaceId { get; init; } = "runtime-central-interaction-multi-device-projection";
    public string DoctrinePath { get; init; } = string.Empty;
    public string WorkmapPath { get; init; } = string.Empty;
    public string ProjectionClassMatrixPath { get; init; } = string.Empty;
    public string RoleAuthorityMatrixPath { get; init; } = string.Empty;
    public string MobileThinClientActionEnvelopePath { get; init; } = string.Empty;
    public string ExternalAgentIngressContractPath { get; init; } = string.Empty;
    public string SessionContinuityAndNotificationReturnLanePath { get; init; } = string.Empty;
    public string CapabilityForgeRetirementRoutingPath { get; init; } = string.Empty;
    public string SessionGatewayPlanPath { get; init; } = string.Empty;
    public string RuntimeGovernanceProgramReauditPath { get; init; } = string.Empty;
    public string OverallPosture { get; init; } = string.Empty;
    public string CurrentLine { get; init; } = "611_line_central_interaction_multi_device_projection";
    public string DeferredNextLine { get; init; } = "none";
    public string ProgramClosureVerdict { get; init; } = "program_closure_complete";
    public int ProjectionClassCount { get; init; }
    public int RoleAuthorityCount { get; init; }
    public int ClientActionEnvelopeCount { get; init; }
    public int ExternalAgentIngressCount { get; init; }
    public int ContinuityLaneCount { get; init; }
    public IReadOnlyList<RuntimeProjectionClassBoundarySurface> ProjectionClasses { get; init; } = [];
    public IReadOnlyList<RuntimeRoleAuthorityBoundarySurface> RoleAuthorities { get; init; } = [];
    public IReadOnlyList<RuntimeClientActionEnvelopeSurface> ClientActionEnvelopes { get; init; } = [];
    public IReadOnlyList<RuntimeExternalAgentIngressBoundarySurface> ExternalAgentIngressContracts { get; init; } = [];
    public IReadOnlyList<RuntimeSessionContinuityLaneBoundarySurface> ContinuityLanes { get; init; } = [];
    public string RecommendedNextAction { get; init; } = string.Empty;
    public bool IsValid { get; init; } = true;
    public IReadOnlyList<string> Errors { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> NonClaims { get; init; } = [];
}
