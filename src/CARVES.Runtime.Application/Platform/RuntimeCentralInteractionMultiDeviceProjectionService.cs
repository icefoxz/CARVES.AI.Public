using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeCentralInteractionMultiDeviceProjectionService
{
    private readonly string repoRoot;

    public RuntimeCentralInteractionMultiDeviceProjectionService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeCentralInteractionMultiDeviceProjectionSurface Build()
    {
        var errors = new List<string>();

        const string doctrinePath = "docs/runtime/runtime-central-interaction-point-and-official-truth-ingress.md";
        const string workmapPath = "docs/runtime/runtime-central-interaction-multi-device-projection-workmap.md";
        const string projectionClassMatrixPath = "docs/runtime/runtime-projection-class-matrix.md";
        const string roleAuthorityMatrixPath = "docs/runtime/runtime-role-authority-matrix.md";
        const string mobileThinClientActionEnvelopePath = "docs/runtime/runtime-mobile-thin-client-action-envelope.md";
        const string externalAgentIngressContractPath = "docs/runtime/runtime-external-agent-ingress-contract.md";
        const string sessionContinuityAndNotificationReturnLanePath = "docs/runtime/runtime-session-continuity-and-notification-return-lane.md";
        const string capabilityForgeRetirementRoutingPath = "docs/session-gateway/capability-forge-retirement-routing.md";
        const string sessionGatewayPlanPath = "docs/session-gateway/session-gateway-v1-post-closure-execution-plan.md";
        const string runtimeGovernanceProgramReauditPath = "docs/runtime/runtime-governance-program-reaudit.md";

        ValidateDocument(doctrinePath, "Central interaction doctrine", errors);
        ValidateDocument(workmapPath, "Central interaction workmap", errors);
        ValidateDocument(projectionClassMatrixPath, "Projection-class matrix", errors);
        ValidateDocument(roleAuthorityMatrixPath, "Role-authority matrix", errors);
        ValidateDocument(mobileThinClientActionEnvelopePath, "Mobile/thin-client action envelope", errors);
        ValidateDocument(externalAgentIngressContractPath, "External-agent ingress contract", errors);
        ValidateDocument(sessionContinuityAndNotificationReturnLanePath, "Session continuity and notification return lane", errors);
        ValidateDocument(capabilityForgeRetirementRoutingPath, "Capability Forge retirement routing", errors);
        ValidateDocument(sessionGatewayPlanPath, "Session Gateway post-closure execution plan", errors);
        ValidateDocument(runtimeGovernanceProgramReauditPath, "Runtime governance program re-audit", errors);

        var projectionClasses = RuntimeCentralInteractionMultiDeviceProjectionCatalog.BuildProjectionClasses();
        var roleAuthorities = RuntimeCentralInteractionMultiDeviceProjectionCatalog.BuildRoleAuthorities();
        var clientActionEnvelopes = RuntimeCentralInteractionMultiDeviceProjectionCatalog.BuildClientActionEnvelopes();
        var externalAgentIngressContracts = RuntimeCentralInteractionMultiDeviceProjectionCatalog.BuildExternalAgentIngressContracts();
        var continuityLanes = RuntimeCentralInteractionMultiDeviceProjectionCatalog.BuildContinuityLanes();

        if (projectionClasses.Count != 4)
        {
            errors.Add($"Expected 4 projection classes but found {projectionClasses.Count}.");
        }

        if (roleAuthorities.Count < 6)
        {
            errors.Add($"Expected at least 6 role-authority entries but found {roleAuthorities.Count}.");
        }

        if (clientActionEnvelopes.Count < 5)
        {
            errors.Add($"Expected at least 5 client action envelopes but found {clientActionEnvelopes.Count}.");
        }

        if (externalAgentIngressContracts.Count < 4)
        {
            errors.Add($"Expected at least 4 external-agent ingress contracts but found {externalAgentIngressContracts.Count}.");
        }

        if (continuityLanes.Count < 4)
        {
            errors.Add($"Expected at least 4 continuity lanes but found {continuityLanes.Count}.");
        }

        var isValid = errors.Count == 0;

        return new RuntimeCentralInteractionMultiDeviceProjectionSurface
        {
            DoctrinePath = doctrinePath,
            WorkmapPath = workmapPath,
            ProjectionClassMatrixPath = projectionClassMatrixPath,
            RoleAuthorityMatrixPath = roleAuthorityMatrixPath,
            MobileThinClientActionEnvelopePath = mobileThinClientActionEnvelopePath,
            ExternalAgentIngressContractPath = externalAgentIngressContractPath,
            SessionContinuityAndNotificationReturnLanePath = sessionContinuityAndNotificationReturnLanePath,
            CapabilityForgeRetirementRoutingPath = capabilityForgeRetirementRoutingPath,
            SessionGatewayPlanPath = sessionGatewayPlanPath,
            RuntimeGovernanceProgramReauditPath = runtimeGovernanceProgramReauditPath,
            OverallPosture = isValid ? "central_interaction_multi_device_projection_ready" : "blocked_by_central_interaction_projection_gaps",
            ProjectionClassCount = projectionClasses.Count,
            RoleAuthorityCount = roleAuthorities.Count,
            ClientActionEnvelopeCount = clientActionEnvelopes.Count,
            ExternalAgentIngressCount = externalAgentIngressContracts.Count,
            ContinuityLaneCount = continuityLanes.Count,
            ProjectionClasses = projectionClasses,
            RoleAuthorities = roleAuthorities,
            ClientActionEnvelopes = clientActionEnvelopes,
            ExternalAgentIngressContracts = externalAgentIngressContracts,
            ContinuityLanes = continuityLanes,
            RecommendedNextAction = isValid
                ? "Use inspect/api runtime-central-interaction-multi-device-projection before opening client-facing maintenance so workstation, thin/mobile, and external-agent work stays bounded to one official truth ingress."
                : "Restore the missing doctrine/workmap packet files before claiming the 611-line is ready.",
            IsValid = isValid,
            Errors = errors,
            NonClaims =
            [
                "This surface does not prove workstation, mobile, or external-agent clients are already implemented.",
                "This surface does not create a second control plane, queue, or official truth root.",
                "This surface does not grant direct official-truth write authority to workstation, thin/mobile, or external-agent projections.",
            ],
        };
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
