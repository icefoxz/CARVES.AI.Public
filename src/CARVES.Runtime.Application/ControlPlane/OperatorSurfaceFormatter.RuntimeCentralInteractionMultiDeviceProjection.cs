using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeCentralInteractionMultiDeviceProjection(RuntimeCentralInteractionMultiDeviceProjectionSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime central interaction multi-device projection",
            $"Doctrine doc: {surface.DoctrinePath}",
            $"Workmap doc: {surface.WorkmapPath}",
            $"Projection-class matrix doc: {surface.ProjectionClassMatrixPath}",
            $"Role-authority matrix doc: {surface.RoleAuthorityMatrixPath}",
            $"Mobile/thin-client action envelope doc: {surface.MobileThinClientActionEnvelopePath}",
            $"External-agent ingress contract doc: {surface.ExternalAgentIngressContractPath}",
            $"Session continuity and notification return lane doc: {surface.SessionContinuityAndNotificationReturnLanePath}",
            $"Capability Forge retirement routing doc: {surface.CapabilityForgeRetirementRoutingPath}",
            $"Session Gateway plan doc: {surface.SessionGatewayPlanPath}",
            $"Runtime governance program re-audit doc: {surface.RuntimeGovernanceProgramReauditPath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Current line: {surface.CurrentLine}",
            $"Deferred next line: {surface.DeferredNextLine}",
            $"Program closure verdict: {surface.ProgramClosureVerdict}",
            $"Projection classes: {surface.ProjectionClassCount}",
            $"Role-authority entries: {surface.RoleAuthorityCount}",
            $"Client action envelopes: {surface.ClientActionEnvelopeCount}",
            $"External-agent ingress contracts: {surface.ExternalAgentIngressCount}",
            $"Continuity lanes: {surface.ContinuityLaneCount}",
            $"Recommended next action: {surface.RecommendedNextAction}",
        };

        foreach (var projectionClass in surface.ProjectionClasses)
        {
            lines.Add($"- projection_class:{projectionClass.ProjectionClassId} form={projectionClass.PrimaryForm}");
            lines.Add($"  inspect_scope: {projectionClass.InspectScope}");
            lines.Add($"  request_execution: {projectionClass.RequestExecutionMode}");
            lines.Add($"  approval: {projectionClass.ApprovalMode}");
            lines.Add($"  governed_mutation: {projectionClass.GovernedMutationMode}");
            lines.Add($"  official_truth_write: {projectionClass.OfficialTruthWriteMode}");
            lines.Add($"  summary: {projectionClass.Summary}");
        }

        foreach (var roleAuthority in surface.RoleAuthorities)
        {
            lines.Add($"- role_authority:{roleAuthority.RoleId}");
            lines.Add($"  projection_classes: {string.Join(" | ", roleAuthority.TypicalProjectionClasses)}");
            lines.Add($"  observe: {roleAuthority.ObserveAuthority}");
            lines.Add($"  propose: {roleAuthority.ProposeAuthority}");
            lines.Add($"  approve: {roleAuthority.ApproveAuthority}");
            lines.Add($"  execute: {roleAuthority.ExecuteAuthority}");
            lines.Add($"  official_truth_write: {roleAuthority.OfficialTruthWriteAuthority}");
            lines.Add($"  summary: {roleAuthority.Summary}");
        }

        foreach (var envelope in surface.ClientActionEnvelopes)
        {
            lines.Add($"- client_action_envelope:{envelope.EnvelopeId} client_class={envelope.ClientClassId}");
            lines.Add($"  allowed_actions: {string.Join(" | ", envelope.AllowedActions)}");
            lines.Add($"  host_mediation_required: {envelope.HostMediationRequired}");
            lines.Add($"  required_proof_source: {envelope.RequiredProofSource}");
            lines.Add($"  summary: {envelope.Summary}");
        }

        foreach (var ingress in surface.ExternalAgentIngressContracts)
        {
            lines.Add($"- external_agent_ingress:{ingress.ContractId}");
            lines.Add($"  allowed_actions: {string.Join(" | ", ingress.AllowedIngressActions)}");
            lines.Add($"  blocked_actions: {string.Join(" | ", ingress.BlockedIngressActions)}");
            lines.Add($"  summary: {ingress.Summary}");
        }

        foreach (var lane in surface.ContinuityLanes)
        {
            lines.Add($"- continuity_lane:{lane.LaneId}");
            lines.Add($"  trigger: {lane.Trigger}");
            lines.Add($"  host_boundary: {lane.HostBoundary}");
            lines.Add($"  summary: {lane.Summary}");
        }

        lines.Add($"Non-claims: {surface.NonClaims.Count}");
        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation valid: {surface.IsValid}");
        lines.Add($"Validation errors: {surface.Errors.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.Add($"Validation warnings: {surface.Warnings.Count}");
        lines.AddRange(surface.Warnings.Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
