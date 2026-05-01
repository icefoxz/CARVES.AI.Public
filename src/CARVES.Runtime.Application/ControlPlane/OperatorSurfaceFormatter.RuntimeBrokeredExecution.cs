using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeBrokeredExecution(RuntimeBrokeredExecutionSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime brokered execution",
            $"Task: {surface.TaskId}",
            $"Card: {surface.CardId}",
            $"Task status: {surface.TaskStatus}",
            $"Mode E profile: {surface.ModeEProfileId}",
            $"Brokered execution state: {surface.BrokeredExecutionState}",
            $"Doctrine: {surface.BrokeredExecutionDocumentPath}",
            $"Planner intent: {surface.PlannerIntent}",
            $"Packet path: {surface.PacketPath}",
            $"Packet persisted: {surface.PacketPersisted}",
            $"Result return channel: {surface.ResultReturnChannel}",
            $"Result return payload status: {surface.ResultReturn.PayloadStatus}",
            $"Result return expected payload shape: {surface.ResultReturn.ExpectedPayloadShape}",
            $"Result return payload present: {surface.ResultReturn.PayloadPresent}",
            $"Result return payload valid: {surface.ResultReturn.PayloadValid}",
            $"Result return official truth state: {surface.ResultReturn.OfficialTruthState}",
            $"Result return expected evidence: {string.Join(", ", surface.ResultReturn.ExpectedEvidence)}",
            $"Result return missing evidence: {FormatList(surface.ResultReturn.MissingEvidence)}",
            $"Result return payload issues: {FormatList(surface.ResultReturn.PayloadIssues)}",
            $"Worker artifact channel: {surface.WorkerArtifactChannel}",
            $"Packet enforcement path: {surface.PacketEnforcementPath}",
            $"Packet enforcement verdict: {surface.PacketEnforcementVerdict}",
            $"Mode E review preflight status: {surface.ReviewPreflight.Status}",
            $"Mode E review preflight can approve: {surface.ReviewPreflight.CanProceedToReviewApproval}",
            $"Mode E review preflight packet scope: {surface.ReviewPreflight.PacketScopeStatus}",
            $"Mode E review preflight packet mismatches: {FormatList(surface.ReviewPreflight.PacketScopeMismatchFiles)}",
            $"Mode E review preflight acceptance evidence: {surface.ReviewPreflight.AcceptanceEvidenceStatus}",
            $"Mode E review preflight missing acceptance evidence: {FormatList(surface.ReviewPreflight.MissingAcceptanceEvidence)}",
            $"Mode E review preflight path policy: {surface.ReviewPreflight.PathPolicyStatus}",
            $"Mode E review preflight protected paths: {FormatList(surface.ReviewPreflight.ProtectedPathViolations)}",
            $"Mode E review preflight mutation audit: {surface.ReviewPreflight.MutationAuditStatus}",
            $"Mode E review preflight mutation paths: {FormatList(surface.ReviewPreflight.MutationAuditViolationPaths)}",
            $"Mode E review preflight mutation first class: {surface.ReviewPreflight.MutationAuditFirstViolationClass ?? "(none)"}",
            $"Mode E review preflight mutation action: {surface.ReviewPreflight.MutationAuditRecommendedAction}",
            $"Workspace ownership policy: {surface.WorkspaceOwnershipPolicy}",
            $"Official truth ingress policy: {surface.OfficialTruthIngressPolicy}",
            $"Result return policy: {surface.ResultReturnPolicy}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Brokered checks:",
        };

        foreach (var check in surface.BrokeredChecks)
        {
            lines.Add($"- check: {check.CheckId} | state={check.State} | required_action={check.RequiredAction}");
        }

        lines.Add("Mode E review preflight blockers:");
        foreach (var blocker in surface.ReviewPreflight.Blockers)
        {
            lines.Add($"- blocker: {blocker.BlockerId} | category={blocker.Category} | required_action={blocker.RequiredAction}");
        }

        if (surface.ReviewPreflight.Blockers.Count == 0)
        {
            lines.Add("- (none)");
        }

        lines.Add("Protected path violation details:");
        foreach (var detail in surface.ReviewPreflight.ProtectedPathViolationDetails)
        {
            lines.Add($"- path: {detail.Path} | classification={detail.ProtectedClassification} | remediation={detail.RemediationAction}");
        }

        if (surface.ReviewPreflight.ProtectedPathViolationDetails.Count == 0)
        {
            lines.Add("- (none)");
        }

        if (surface.MutationAudit is not null)
        {
            lines.Add("Workspace mutation audit:");
            lines.Add($"- status: {surface.MutationAudit.Status} | lease_aware={surface.MutationAudit.LeaseAware} | changed_paths={surface.MutationAudit.ChangedPathCount} | violations={surface.MutationAudit.ViolationCount}");
            lines.Add($"- next_action: {surface.MutationAudit.RecommendedNextAction}");
        }

        lines.Add("Inspect chain:");
        lines.AddRange(surface.InspectCommands.Select(command => $"- {command}"));

        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));

        if (surface.Errors.Count > 0)
        {
            lines.Add("Errors:");
            lines.AddRange(surface.Errors.Select(error => $"- {error}"));
        }

        return new OperatorCommandResult(0, lines);
    }

    private static string FormatList(IReadOnlyList<string> items)
    {
        return items.Count == 0 ? "(none)" : string.Join(", ", items);
    }
}
