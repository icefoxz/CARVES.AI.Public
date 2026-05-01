using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult ExecutionHardening(ExecutionHardeningSurfaceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Execution hardening",
            $"Task: {snapshot.TaskId}",
            $"Card: {snapshot.CardId}",
            $"Scenario: {snapshot.ScenarioId}",
            $"Task status: {snapshot.TaskStatus}",
            $"Review status: {snapshot.ReviewStatus}",
            $"Summary: {snapshot.Summary}",
            "Governance bootstrap:",
            $"- ready: {snapshot.Governance.BootstrapReady}",
            $"- summary: {snapshot.Governance.Summary}",
            $"- assets: {(snapshot.Governance.Assets.Length == 0 ? "(none)" : string.Join(" | ", snapshot.Governance.Assets.Select(asset => $"{asset.AssetId}:{(asset.Exists ? "present" : "missing")}")))}",
            "Relevant tools:",
        };

        foreach (var tool in snapshot.RelevantTools)
        {
            lines.Add($"- {tool.ToolId}: {tool.ActionClass}/{tool.Availability}; truth_affecting={tool.TruthAffecting}");
        }

        lines.Add("Negative paths:");
        foreach (var path in snapshot.NegativePaths)
        {
            lines.Add($"- {path.PathId}: supported={path.ExplicitVerdictSupported}; current={path.CurrentStatus}; reasons={(path.ReasonCodes.Length == 0 ? "(none)" : string.Join(",", path.ReasonCodes))}");
        }

        lines.Add("Inspect chain:");
        lines.AddRange(snapshot.InspectCommands.Select(command => $"- {command}"));
        lines.Add($"Packet summary: {snapshot.ExecutionPacket.Summary}");
        lines.Add($"Packet enforcement verdict: {snapshot.PacketEnforcement.Record.Verdict}");

        var driftedFamilies = snapshot.AuthoritativeTruth.Families
            .Where(family => family.MirrorDriftDetected)
            .Select(family => family.FamilyId)
            .ToArray();
        lines.Add($"Authoritative drift: {(driftedFamilies.Length == 0 ? "(none)" : string.Join(" | ", driftedFamilies))}");

        return new OperatorCommandResult(0, lines);
    }
}
