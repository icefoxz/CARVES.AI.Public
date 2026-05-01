using System.Security.Cryptography;
using System.Text;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

internal sealed record HostCommandSurfaceCompatibility(
    bool Compatible,
    string Readiness,
    string Reason,
    string ExpectedSchemaVersion,
    string ExpectedFingerprint,
    string? ActualSchemaVersion,
    string? ActualFingerprint,
    int ExpectedCommandCount,
    int ActualCommandCount);

internal static class HostCommandSurfaceCatalog
{
    internal const string SchemaVersion = "host-command-surface.v1";
    internal const string RestartAction = "carves gateway restart \"surface registry stale\"";

    private static readonly string[] TopLevelCommands =
    [
        "agent",
        "api",
        "attach",
        "audit",
        "approve-card",
        "approve-review",
        "approve-suggested-task",
        "approve-task",
        "approve-taskgraph-draft",
        "archive-card",
        "card",
        "cleanup",
        "compact-history",
        "console",
        "context",
        "create-card-draft",
        "create-taskgraph-draft",
        "dashboard",
        "discuss",
        "evidence",
        "failures",
        "governance",
        "inspect",
        "inspect-card",
        "intent",
        "list-cards",
        "memory",
        "pack",
        "pilot",
        "plan",
        "plan-card",
        "planner",
        "policy",
        "prompt",
        "provider",
        "qualification",
        "rebuild",
        "reconcile",
        "reject-card",
        "reject-review",
        "repo",
        "report",
        "reset",
        "reopen-review",
        "review-task",
        "run",
        "runtime",
        "session",
        "show-opportunities",
        "status",
        "supersede-card-tasks",
        "sync-state",
        "task",
        "update-card",
        "validation",
        "verify",
        "workbench",
        "worker",
    ];

    internal static IReadOnlyList<string> CommandEntries { get; } = BuildCommandEntries().ToArray();

    internal static string Fingerprint { get; } = ComputeFingerprint(CommandEntries);

    internal static HostCommandSurfaceCompatibility Evaluate(HostHandshakeSummary? summary)
    {
        var actualSchemaVersion = summary?.CommandSurfaceSchemaVersion;
        var actualFingerprint = summary?.CommandSurfaceFingerprint;
        var actualCommandCount = summary?.CommandSurfaceCommandCount ?? 0;

        if (summary is null)
        {
            return BuildIncompatible(
                "missing_host_handshake",
                actualSchemaVersion,
                actualFingerprint,
                actualCommandCount);
        }

        if (string.IsNullOrWhiteSpace(actualSchemaVersion)
            || string.IsNullOrWhiteSpace(actualFingerprint))
        {
            return BuildIncompatible(
                "missing_command_surface_metadata",
                actualSchemaVersion,
                actualFingerprint,
                actualCommandCount);
        }

        if (!string.Equals(actualSchemaVersion, SchemaVersion, StringComparison.Ordinal)
            || !string.Equals(actualFingerprint, Fingerprint, StringComparison.Ordinal))
        {
            return BuildIncompatible(
                "command_surface_fingerprint_mismatch",
                actualSchemaVersion,
                actualFingerprint,
                actualCommandCount);
        }

        return new HostCommandSurfaceCompatibility(
            Compatible: true,
            Readiness: "command_surface_current",
            Reason: "resident_host_command_surface_matches_client",
            ExpectedSchemaVersion: SchemaVersion,
            ExpectedFingerprint: Fingerprint,
            ActualSchemaVersion: actualSchemaVersion,
            ActualFingerprint: actualFingerprint,
            ExpectedCommandCount: CommandEntries.Count,
            ActualCommandCount: actualCommandCount);
    }

    internal static OperatorCommandResult BuildStaleSurfaceResult(
        HostDiscoveryResult discovery,
        string command,
        IReadOnlyList<string> arguments)
    {
        var compatibility = Evaluate(discovery.Summary);
        var commandLine = arguments.Count == 0
            ? command
            : $"{command} {string.Join(' ', arguments)}";

        return OperatorCommandResult.Failure(
            $"Host command surface is stale; refusing to route `{commandLine}` through the resident host.",
            "surface_registry_stale",
            "host_restart_required",
            $"Reason: {compatibility.Reason}",
            $"Base URL: {discovery.Summary?.BaseUrl ?? discovery.Descriptor?.BaseUrl ?? "(unknown)"}",
            $"Expected command surface: {compatibility.ExpectedSchemaVersion}/{compatibility.ExpectedFingerprint}",
            $"Actual command surface: {compatibility.ActualSchemaVersion ?? "(missing)"}/{compatibility.ActualFingerprint ?? "(missing)"}",
            $"Expected command count: {compatibility.ExpectedCommandCount}",
            $"Actual command count: {compatibility.ActualCommandCount}",
            $"Next action: {RestartAction}");
    }

    private static HostCommandSurfaceCompatibility BuildIncompatible(
        string reason,
        string? actualSchemaVersion,
        string? actualFingerprint,
        int actualCommandCount)
    {
        return new HostCommandSurfaceCompatibility(
            Compatible: false,
            Readiness: "surface_registry_stale",
            Reason: reason,
            ExpectedSchemaVersion: SchemaVersion,
            ExpectedFingerprint: Fingerprint,
            ActualSchemaVersion: actualSchemaVersion,
            ActualFingerprint: actualFingerprint,
            ExpectedCommandCount: CommandEntries.Count,
            ActualCommandCount: actualCommandCount);
    }

    private static IEnumerable<string> BuildCommandEntries()
    {
        foreach (var command in TopLevelCommands.Order(StringComparer.Ordinal))
        {
            yield return $"command:{command}";
        }

        foreach (var command in LocalHostCommandDispatcher.FixedInspectUsageCommands.Order(StringComparer.Ordinal))
        {
            yield return $"inspect:{command}";
        }

        foreach (var command in LocalHostCommandDispatcher.FixedApiUsageCommands.Order(StringComparer.Ordinal))
        {
            yield return $"api:{command}";
        }

        foreach (var command in RuntimeSurfaceCommandRegistry.CommandMetadata.OrderBy(command => command.Name, StringComparer.Ordinal))
        {
            yield return $"runtime-surface:{command.Name}:{command.ContextTier}:{command.DefaultVisibility}:{command.SurfaceRole}:{command.RetirementPosture}:{command.SuccessorSurfaceId ?? "(none)"}";
        }
    }

    private static string ComputeFingerprint(IReadOnlyList<string> entries)
    {
        var payload = string.Join('\n', entries.Prepend(SchemaVersion));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
