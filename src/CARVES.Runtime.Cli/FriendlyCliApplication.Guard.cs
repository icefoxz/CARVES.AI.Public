using System.Text.Json;
using Carves.Guard.Core;
using Carves.Runtime.Application.Guard;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunGuard(string repoRoot, IReadOnlyList<string> arguments, TransportPreference transport)
    {
        return GuardCliRunner.Run(
            repoRoot,
            arguments,
            new RuntimeGuardTaskRunner(transport),
            commandName: "carves guard",
            MapGuardTransport(transport));
    }

    private static GuardRuntimeTransportPreference MapGuardTransport(TransportPreference transport)
    {
        return transport switch
        {
            TransportPreference.Cold => GuardRuntimeTransportPreference.Cold,
            TransportPreference.Host => GuardRuntimeTransportPreference.Host,
            _ => GuardRuntimeTransportPreference.Auto,
        };
    }

    private sealed class RuntimeGuardTaskRunner : IGuardRuntimeTaskRunner
    {
        private readonly TransportPreference transport;

        public RuntimeGuardTaskRunner(TransportPreference transport)
        {
            this.transport = transport;
        }

        public GuardRuntimeExecutionResult Execute(GuardRuntimeTaskInvocation invocation)
        {
            if (transport == TransportPreference.Host)
            {
                var hostProjection = ResolveFriendlyHostProjection(invocation.RepoRoot);
                if (!hostProjection.HostRunning)
                {
                    return new GuardRuntimeExecutionResult(
                        invocation.TaskId,
                        Accepted: false,
                        Outcome: "host_unavailable",
                        TaskStatus: "unchanged",
                        Summary: string.IsNullOrWhiteSpace(hostProjection.Message)
                            ? "Host transport was requested, but the resident host was not safely available."
                            : hostProjection.Message,
                        FailureKind: "HostUnavailable",
                        RunId: null,
                        ExecutionRunId: null,
                        ChangedFiles: Array.Empty<string>(),
                        NextAction: ResolveFriendlyHostNextAction(hostProjection, "carves host ensure --json"));
                }
            }

            var hostArguments = new List<string>();
            if (transport == TransportPreference.Cold)
            {
                hostArguments.Add("--cold");
            }

            hostArguments.Add("task");
            hostArguments.Add("run");
            hostArguments.Add(invocation.TaskId);
            hostArguments.AddRange(invocation.Arguments);

            var result = HostProgramInvoker.Invoke(invocation.RepoRoot, hostArguments.ToArray());
            if (TryParseRuntimeExecutionResult(invocation.TaskId, result.CombinedOutput, out var execution))
            {
                return execution;
            }

            return new GuardRuntimeExecutionResult(
                invocation.TaskId,
                Accepted: false,
                Outcome: result.ExitCode == 0 ? "unparseable_runtime_output" : "failed",
                TaskStatus: "unknown",
                Summary: NormalizeRuntimeSummary(result.CombinedOutput),
                FailureKind: "RuntimeOutputParseFailed",
                RunId: null,
                ExecutionRunId: null,
                ChangedFiles: Array.Empty<string>(),
                NextAction: result.ExitCode == 0 ? "inspect runtime task output" : "inspect runtime task failure");
        }

        private static bool TryParseRuntimeExecutionResult(
            string taskId,
            string output,
            out GuardRuntimeExecutionResult execution)
        {
            execution = default!;
            var json = ExtractJsonObject(output);
            if (json is null)
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                execution = new GuardRuntimeExecutionResult(
                    ReadString(root, "task_id", taskId),
                    ReadBool(root, "accepted"),
                    ReadString(root, "outcome", "unknown"),
                    ReadString(root, "task_status", "unknown"),
                    ReadString(root, "summary", string.Empty),
                    ReadNullableString(root, "failure_kind"),
                    ReadNullableString(root, "run_id"),
                    ReadNullableString(root, "execution_run_id"),
                    ReadStringArray(root, "changed_files"),
                    ReadString(root, "next_action", string.Empty));
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string? ExtractJsonObject(string output)
        {
            var start = output.IndexOf('{');
            var end = output.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            return output[start..(end + 1)];
        }

        private static string ReadString(JsonElement element, string propertyName, string fallback)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? fallback
                : fallback;
        }

        private static string? ReadNullableString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        private static bool ReadBool(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;
        }

        private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                .Select(item => item.GetString()!)
                .ToArray();
        }

        private static string NormalizeRuntimeSummary(string output)
        {
            var normalized = output.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
            if (normalized.Length <= 500)
            {
                return normalized;
            }

            return string.Concat(normalized.AsSpan(0, 497), "...");
        }
    }
}
