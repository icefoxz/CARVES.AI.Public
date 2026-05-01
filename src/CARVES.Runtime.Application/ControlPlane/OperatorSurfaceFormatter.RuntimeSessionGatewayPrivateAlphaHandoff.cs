using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeSessionGatewayPrivateAlphaHandoff(RuntimeSessionGatewayPrivateAlphaHandoffSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime Session Gateway private alpha handoff",
            $"Execution plan: {surface.ExecutionPlanPath}",
            $"Release surface: {surface.ReleaseSurfacePath}",
            $"Dogfood validation doc: {surface.DogfoodValidationPath}",
            $"Operator proof contract doc: {surface.OperatorProofContractPath}",
            $"Alpha setup doc: {surface.AlphaSetupPath}",
            $"Alpha quickstart doc: {surface.AlphaQuickstartPath}",
            $"Known limitations doc: {surface.KnownLimitationsPath}",
            $"Bug report bundle doc: {surface.BugReportBundlePath}",
            $"Overall posture: {surface.OverallPosture}",
            $"Dogfood validation posture: {surface.DogfoodValidationPosture}",
            $"Program closure verdict: {surface.ProgramClosureVerdict}",
            $"Continuation gate outcome: {surface.ContinuationGateOutcome}",
            $"Broker mode: {surface.BrokerMode}",
            $"Truth owner: {surface.TruthOwner}",
            $"Handoff ownership: {surface.HandoffOwnership}",
            $"Thin shell route: {surface.ThinShellRoute}",
            $"Session collection route: {surface.SessionCollectionRoute}",
            $"Message route: {surface.MessageRouteTemplate}",
            $"Events route: {surface.EventsRouteTemplate}",
            $"Accepted operation route: {surface.AcceptedOperationRouteTemplate}",
            $"Runtime health: {surface.RuntimeHealthState}; summary={surface.RuntimeHealthSummary}; suggested_action={surface.RuntimeHealthSuggestedAction}; issues={surface.RuntimeHealthIssueCount}",
            $"Provider visibility: {surface.ProviderVisibilitySummary}",
            $"Operational next action: {surface.OperationalRecommendedNextAction}",
            $"Supported intents: {string.Join(", ", surface.SupportedIntents)}",
            $"Recommended next action: {surface.RecommendedNextAction}",
            $"Startup commands: {surface.StartupCommands.Count}",
        };

        AppendSessionGatewayOperatorProofContract(lines, surface.OperatorProofContract);
        lines.AddRange(surface.StartupCommands.Select(item => $"- start: {item}"));
        lines.Add($"Provider status commands: {surface.ProviderStatusCommands.Count}");
        lines.AddRange(surface.ProviderStatusCommands.Select(item => $"- provider: {item}"));
        lines.Add($"Runtime health commands: {surface.RuntimeHealthCommands.Count}");
        lines.AddRange(surface.RuntimeHealthCommands.Select(item => $"- health: {item}"));
        lines.Add($"Maintenance commands: {surface.MaintenanceCommands.Count}");
        lines.AddRange(surface.MaintenanceCommands.Select(item => $"- maintenance: {item}"));
        lines.Add($"Bug-report bundle commands: {surface.BugReportBundleCommands.Count}");
        lines.AddRange(surface.BugReportBundleCommands.Select(item => $"- bundle: {item}"));
        lines.Add($"Provider statuses: {surface.ProviderStatuses.Count}");
        lines.AddRange(surface.ProviderStatuses.Select(item => $"- provider-status: {item}"));
        lines.Add($"Runtime issue summaries: {surface.RuntimeIssueSummaries.Count}");
        lines.AddRange(surface.RuntimeIssueSummaries.Select(item => $"- runtime-issue: {item}"));
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
