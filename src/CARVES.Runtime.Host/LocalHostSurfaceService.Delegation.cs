using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private static JsonObject BuildActionResult(OperatorCommandResult result)
    {
        return new JsonObject
        {
            ["exit_code"] = result.ExitCode,
            ["lines"] = ToJsonArray(result.Lines),
        };
    }

    private JsonObject BuildDelegationProtocol(bool includeDriftWarning = true)
    {
        var policy = services.RuntimePolicyBundleService.LoadDelegationPolicy();
        var warning = includeDriftWarning ? GetDelegationDriftWarning() : null;
        return new JsonObject
        {
            ["kind"] = "delegation_protocol",
            ["rule"] = "Inspect, delegate execution through CARVES Host, then review the resulting worker summary.",
            ["require_inspect_before_execution"] = policy.RequireInspectBeforeExecution,
            ["require_resident_host"] = policy.RequireResidentHost,
            ["allow_manual_execution_fallback"] = policy.AllowManualExecutionFallback,
            ["inspect_commands"] = ToJsonArray(policy.InspectCommands),
            ["run_commands"] = ToJsonArray(policy.RunCommands),
            ["inspect_command"] = policy.InspectCommands.FirstOrDefault() ?? "task inspect <task-id>",
            ["delegate_command"] = policy.RunCommands.FirstOrDefault() ?? "task run <task-id>",
            ["fallback"] = policy.AllowManualExecutionFallback
                ? "Start or recover the resident host before execution. Use `--cold task run <task-id>` only as explicit operator-governed fallback."
                : "Start or recover the resident host before execution. Manual execution fallback is disabled by policy.",
            ["warning"] = warning is null
                ? null
                : new JsonObject
                {
                    ["summary"] = warning.Summary,
                    ["recommended_command"] = warning.RecommendedCommand,
                    ["dirty_paths"] = ToJsonArray(warning.DirtyPaths),
                },
        };
    }

    private DelegationDriftWarning? GetDelegationDriftWarning()
    {
        if (!services.GitClient.IsRepository(services.Paths.RepoRoot))
        {
            return null;
        }

        var dirtyPaths = services.GitClient.GetUncommittedPaths(services.Paths.RepoRoot)
            .Where(path =>
                path.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (dirtyPaths.Length == 0)
        {
            return null;
        }

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
        var recentDelegation = services.OperatorApiService.GetOperatorOsEvents()
            .Any(item =>
                item.OccurredAt >= cutoff
                && item.EventKind is OperatorOsEventKind.DelegationRequested
                    or OperatorOsEventKind.DelegationCompleted
                    or OperatorOsEventKind.DelegationFallbackUsed);
        if (recentDelegation)
        {
            return null;
        }

        var warning = new DelegationDriftWarning(
            "Protected source areas are dirty and no recent delegation telemetry was observed.",
            "task run <task-id>",
            dirtyPaths);
        MaybeAppendDelegationDriftEvent(warning);
        return warning;
    }

    private void MaybeAppendDelegationDriftEvent(DelegationDriftWarning warning)
    {
        var existing = services.OperatorOsEventStreamService.Load(eventKind: OperatorOsEventKind.DelegationBypassDetected)
            .FirstOrDefault();
        if (existing is not null
            && existing.OccurredAt >= DateTimeOffset.UtcNow.AddMinutes(-10)
            && string.Equals(existing.Summary, warning.Summary, StringComparison.Ordinal))
        {
            return;
        }

        services.OperatorOsEventStreamService.Append(new OperatorOsEventRecord
        {
            EventKind = OperatorOsEventKind.DelegationBypassDetected,
            RepoId = services.OperatorApiService.GetPlatformStatus().Repos.FirstOrDefault()?.RepoId
                ?? Path.GetFileName(services.Paths.RepoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
            ReasonCode = "delegation_drift_warning",
            Summary = warning.Summary,
            ReferenceId = string.Join("|", warning.DirtyPaths.Take(5)),
        });
    }
}
