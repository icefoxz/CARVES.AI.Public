namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeWorkspaceMutationAudit(RuntimeWorkspaceMutationAuditSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime workspace mutation audit",
            $"Task: {surface.TaskId}",
            $"Card: {surface.CardId}",
            $"Result return channel: {surface.ResultReturnChannel}",
            $"Status: {surface.Status}",
            $"Lease aware: {surface.LeaseAware}",
            $"Lease: {surface.LeaseId ?? "(none)"}",
            $"Allowed writable paths: {FormatList(surface.AllowedWritablePaths)}",
            $"Changed paths: {surface.ChangedPathCount}",
            $"Violations: {surface.ViolationCount}",
            $"Scope escapes: {surface.ScopeEscapeCount}",
            $"Host-only paths: {surface.HostOnlyCount}",
            $"Denied paths: {surface.DenyCount}",
            $"Can proceed to writeback: {surface.CanProceedToWriteback}",
            $"Summary: {surface.Summary}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            "Changed path detail:",
        };

        foreach (var path in surface.ChangedPaths)
        {
            lines.Add($"- path: {path.Path} | policy={path.PolicyClass} | asset={path.AssetClass} | summary={path.Summary}");
        }

        if (surface.ChangedPaths.Count == 0)
        {
            lines.Add("- (none)");
        }

        lines.Add("Mutation-audit blockers:");
        foreach (var blocker in surface.Blockers)
        {
            lines.Add($"- blocker: {blocker.BlockerId} | path={blocker.Path} | policy={blocker.PolicyClass} | action={blocker.RequiredAction}");
        }

        if (surface.Blockers.Count == 0)
        {
            lines.Add("- (none)");
        }

        return new OperatorCommandResult(0, lines);
    }
}
