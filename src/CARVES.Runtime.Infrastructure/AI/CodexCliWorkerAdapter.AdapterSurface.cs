using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    public WorkerProviderCapabilities GetCapabilities()
    {
        return new WorkerProviderCapabilities
        {
            SupportsExecution = true,
            SupportsEventStream = true,
            SupportsHealthProbe = true,
            SupportsCancellation = false,
            SupportsTrustedProfiles = true,
            SupportsNetworkAccess = true,
            SupportsDotNetBuild = true,
            SupportsLongRunningTasks = true,
        };
    }

    public WorkerBackendHealthSummary CheckHealth()
    {
        var command = TryResolveCodexCommand();
        if (command is null)
        {
            return new WorkerBackendHealthSummary
            {
                State = WorkerBackendHealthState.Unavailable,
                Summary = "Codex CLI is not installed or CARVES_CODEX_CLI_PATH is invalid.",
            };
        }

        var result = RunProbe(command);
        return new WorkerBackendHealthSummary
        {
            State = result.ExitCode == 0 ? WorkerBackendHealthState.Healthy : WorkerBackendHealthState.Degraded,
            Summary = result.ExitCode == 0
                ? $"Codex CLI is available via '{command.CommandPath}'."
                : $"Codex CLI probe failed: {FirstNonEmpty(result.StandardError, result.StandardOutput, "probe failed")}",
        };
    }

    public WorkerRunControlResult Cancel(string runId, string reason)
    {
        return new WorkerRunControlResult
        {
            BackendId = BackendId,
            RunId = runId,
            Supported = false,
            Succeeded = false,
            Summary = "Codex CLI worker cancellation is not yet implemented.",
        };
    }
}
