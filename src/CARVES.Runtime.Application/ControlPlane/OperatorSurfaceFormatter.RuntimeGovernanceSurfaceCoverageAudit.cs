namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeGovernanceSurfaceCoverageAudit(RuntimeGovernanceSurfaceCoverageAuditSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime governance surface coverage audit",
            $"Product closure phase: {surface.ProductClosurePhase}",
            $"Repo root: {surface.RepoRoot}",
            $"Overall posture: {surface.OverallPosture}",
            $"Coverage complete: {surface.CoverageComplete}",
            $"Lifecycle budget complete: {surface.LifecycleBudgetComplete}",
            $"Registered surfaces: {surface.RegisteredSurfaceCount}",
            $"Required surfaces: {surface.RequiredSurfaceCount}/{surface.MaxGovernanceCriticalSurfaceCount}",
            $"Covered surfaces: {surface.CoveredSurfaceCount}",
            $"Default-path surfaces: {surface.DefaultPathSurfaceCount}/{surface.MaxDefaultPathSurfaceCount}",
            $"Audit/handoff surfaces: {surface.AuditHandoffSurfaceCount}/{surface.MaxAuditHandoffSurfaceCount}",
            $"Blocking gaps: {surface.BlockingGapCount}",
            $"Advisory gaps: {surface.AdvisoryGapCount}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Coverage dimensions:",
        };

        lines.AddRange(surface.CoverageDimensions.Count == 0
            ? ["- none"]
            : surface.CoverageDimensions.Select(dimension => $"- {dimension}"));
        lines.Add("Surface entries:");
        lines.AddRange(surface.Entries.Count == 0
            ? ["- none"]
            : surface.Entries.Select(entry =>
                $"- {entry.SurfaceId} | status={entry.CoverageStatus} | lifecycle={entry.LifecycleClass} | read_path={entry.ReadPathClass} | default={entry.DefaultPathParticipation} | budget={entry.LifecycleBudgetStatus} | role={entry.Role} | registry={entry.RegistryRegistered} | inspect={entry.InspectUsageExposed} | api={entry.ApiUsageExposed} | resource_pack={FormatRequirement(entry.ResourcePackRequired, entry.ResourcePackCovered)} | quickstart={FormatRequirement(entry.QuickstartRequired, entry.QuickstartDocumented)} | consumer_pack={FormatRequirement(entry.ConsumerPackRequired, entry.ConsumerPackDocumented)} | host_contract={entry.HostContractCovered}"));
        lines.Add("Gaps:");
        lines.AddRange(surface.Gaps.Count == 0
            ? ["- none"]
            : surface.Gaps.Select(gap => $"- {gap}"));
        lines.Add("Lifecycle budget gaps:");
        lines.AddRange(surface.LifecycleBudgetGaps.Count == 0
            ? ["- none"]
            : surface.LifecycleBudgetGaps.Select(gap => $"- {gap}"));
        lines.Add("Advisory gaps:");
        lines.AddRange(surface.AdvisoryGaps.Count == 0
            ? ["- none"]
            : surface.AdvisoryGaps.Select(gap => $"- {gap}"));
        lines.Add("Evidence source paths:");
        lines.AddRange(surface.EvidenceSourcePaths.Count == 0
            ? ["- none"]
            : surface.EvidenceSourcePaths.Select(path => $"- {path}"));
        lines.Add("Non-claims:");
        lines.AddRange(surface.NonClaims.Select(nonClaim => $"- {nonClaim}"));

        return new OperatorCommandResult(surface.CoverageComplete ? 0 : 1, lines);
    }

    private static string FormatRequirement(bool required, bool covered)
    {
        return required ? covered.ToString() : "not_required";
    }
}
