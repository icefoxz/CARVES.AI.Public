using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed class CodexWorkerAdapter : IWorkerAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string selectionReason;

    public CodexWorkerAdapter(string selectionReason)
    {
        this.selectionReason = selectionReason;
    }

    public string AdapterId => nameof(CodexWorkerAdapter);

    public string BackendId => "codex_sdk";

    public string ProviderId => "codex";

    public bool IsConfigured => ResolveBridgeScript() is not null && HasAuthCredential();

    public bool IsRealAdapter => true;

    public string SelectionReason => selectionReason;

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
        var bridgeScript = ResolveBridgeScript();
        var hasAuthCredential = HasAuthCredential();
        return new WorkerBackendHealthSummary
        {
            State = bridgeScript is null || !hasAuthCredential ? WorkerBackendHealthState.Unavailable : WorkerBackendHealthState.Healthy,
            Summary = bridgeScript is null
                ? "Codex SDK bridge script is not configured."
                : !hasAuthCredential
                    ? "OPENAI_API_KEY or CODEX_API_KEY is required for Codex SDK worker execution."
                    : $"Codex SDK bridge script is available at '{bridgeScript}'.",
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
            Summary = "Codex SDK worker cancellation is not yet implemented.",
        };
    }

    public WorkerExecutionResult Execute(WorkerExecutionRequest request)
    {
        var bridgeScript = ResolveBridgeScript();
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();
        var requestPreview = request.Input.Length > 160 ? request.Input[..160] : request.Input;
        if (bridgeScript is null)
        {
            return WorkerExecutionResult.Blocked(
                request.TaskId,
                BackendId,
                ProviderId,
                AdapterId,
                request.Profile,
                WorkerFailureKind.EnvironmentBlocked,
                "Codex SDK bridge script is not configured or installed.",
                requestPreview,
                requestHash);
        }

        if (!HasAuthCredential())
        {
            return WorkerExecutionResult.Blocked(
                request.TaskId,
                BackendId,
                ProviderId,
                AdapterId,
                request.Profile,
                WorkerFailureKind.EnvironmentBlocked,
                "OPENAI_API_KEY or CODEX_API_KEY is required for Codex SDK worker execution.",
                requestPreview,
                requestHash);
        }

        var payload = new CodexBridgeRequest
        {
            RequestId = request.RequestId,
            TaskId = request.TaskId,
            Title = request.Title,
            Description = request.Description,
            Instructions = request.Instructions,
            Input = request.Input,
            WorktreeRoot = request.WorktreeRoot,
            RepoRoot = request.RepoRoot,
            BaseCommit = request.BaseCommit,
            PriorThreadId = request.PriorThreadId,
            Model = request.ModelOverride,
            TimeoutSeconds = request.TimeoutSeconds,
            SandboxMode = MapSandboxMode(request.Profile.SandboxMode),
            ApprovalMode = MapApprovalMode(request.Profile.ApprovalMode),
            NetworkAccessEnabled = request.Profile.NetworkAccessEnabled,
            AllowedFiles = request.AllowedFiles.ToArray(),
            AllowedCommandPrefixes = request.Profile.AllowedCommandPrefixes.ToArray(),
            DeniedCommandPrefixes = request.Profile.DeniedCommandPrefixes.ToArray(),
            ValidationCommands = request.ValidationCommands.Select(command => command.ToArray()).ToArray(),
            Metadata = request.Metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = "node",
            WorkingDirectory = Path.GetDirectoryName(bridgeScript)!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add(bridgeScript);

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return WorkerExecutionResult.Blocked(
                request.TaskId,
                BackendId,
                ProviderId,
                AdapterId,
                request.Profile,
                WorkerFailureKind.EnvironmentBlocked,
                "Failed to start Codex worker bridge process.",
                requestPreview,
                requestHash);
        }

        process.StandardInput.Write(JsonSerializer.Serialize(payload, JsonOptions));
        process.StandardInput.Close();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            return WorkerExecutionResult.Blocked(
                request.TaskId,
                BackendId,
                ProviderId,
                AdapterId,
                request.Profile,
                WorkerFailureKind.EnvironmentBlocked,
                string.IsNullOrWhiteSpace(stderr) ? $"Codex worker bridge exited with code {process.ExitCode}." : stderr.Trim(),
                requestPreview,
                requestHash);
        }

        var response = JsonSerializer.Deserialize<CodexBridgeResponse>(stdout, JsonOptions)
            ?? throw new InvalidOperationException("Codex worker bridge returned an empty response.");

        return new WorkerExecutionResult
        {
            RunId = string.IsNullOrWhiteSpace(response.RunId) ? $"worker-run-{Guid.NewGuid():N}" : response.RunId,
            TaskId = request.TaskId,
            BackendId = BackendId,
            ProviderId = ProviderId,
            AdapterId = AdapterId,
            AdapterReason = selectionReason,
            ProfileId = request.Profile.ProfileId,
            TrustedProfile = request.Profile.Trusted,
            Status = ParseStatus(response.Status),
            FailureKind = ParseFailureKind(response.FailureKind),
            Retryable = response.Retryable,
            Configured = true,
            Model = response.Model ?? request.ModelOverride ?? "codex-default",
            RequestId = response.RequestId,
            PriorThreadId = response.RequestedPriorThreadId ?? request.PriorThreadId,
            ThreadId = ResolveThreadId(response),
            ThreadContinuity = ParseThreadContinuity(response.ThreadContinuity, request.PriorThreadId, response),
            RequestPreview = requestPreview,
            RequestHash = requestHash,
            Summary = response.Summary ?? "(no summary)",
            Rationale = response.Rationale,
            FailureReason = response.FailureReason,
            ChangedFiles = response.ChangedFiles ?? Array.Empty<string>(),
            CommandTrace = (response.CommandTrace ?? Array.Empty<CodexBridgeCommandTrace>())
                .Select(trace => new CommandExecutionRecord(
                    trace.Command ?? Array.Empty<string>(),
                    trace.ExitCode,
                    trace.StandardOutput ?? string.Empty,
                    trace.StandardError ?? string.Empty,
                    false,
                    trace.WorkingDirectory ?? request.WorktreeRoot,
                    trace.Category ?? "worker",
                    trace.CapturedAt ?? DateTimeOffset.UtcNow))
                .ToArray(),
            Events = (response.Events ?? Array.Empty<CodexBridgeEvent>())
                .Select(MapEvent)
                .ToArray(),
            StartedAt = response.StartedAt ?? DateTimeOffset.UtcNow,
            CompletedAt = response.CompletedAt ?? DateTimeOffset.UtcNow,
        };
    }

    private static WorkerEvent MapEvent(CodexBridgeEvent item)
    {
        return new WorkerEvent
        {
            RunId = item.RunId ?? string.Empty,
            TaskId = item.TaskId ?? string.Empty,
            EventType = item.EventType?.ToLowerInvariant() switch
            {
                "run_started" => WorkerEventType.RunStarted,
                "turn_started" => WorkerEventType.TurnStarted,
                "turn_completed" => WorkerEventType.TurnCompleted,
                "turn_failed" => WorkerEventType.TurnFailed,
                "command_executed" => WorkerEventType.CommandExecuted,
                "file_edit_observed" => WorkerEventType.FileEditObserved,
                "validation_observed" => WorkerEventType.ValidationObserved,
                "permission_requested" => WorkerEventType.PermissionRequested,
                "approval_wait" => WorkerEventType.ApprovalWait,
                "final_summary" => WorkerEventType.FinalSummary,
                _ => WorkerEventType.RawError,
            },
            Summary = item.Summary ?? "(no summary)",
            ItemType = item.ItemType,
            CommandText = item.CommandText,
            FilePath = item.FilePath,
            ExitCode = item.ExitCode,
            RawPayload = item.RawPayload,
            Attributes = item.Attributes ?? new Dictionary<string, string>(StringComparer.Ordinal),
            OccurredAt = item.OccurredAt ?? DateTimeOffset.UtcNow,
        };
    }

    private static WorkerExecutionStatus ParseStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "succeeded" => WorkerExecutionStatus.Succeeded,
            "failed" => WorkerExecutionStatus.Failed,
            "blocked" => WorkerExecutionStatus.Blocked,
            "timed_out" => WorkerExecutionStatus.TimedOut,
            "cancelled" => WorkerExecutionStatus.Cancelled,
            "aborted" => WorkerExecutionStatus.Aborted,
            "approval_wait" => WorkerExecutionStatus.ApprovalWait,
            _ => WorkerExecutionStatus.Skipped,
        };
    }

    private static WorkerFailureKind ParseFailureKind(string? failureKind)
    {
        return failureKind?.ToLowerInvariant() switch
        {
            "transient_infra" => WorkerFailureKind.TransientInfra,
            "environment_blocked" => WorkerFailureKind.EnvironmentBlocked,
            "launch_failure" => WorkerFailureKind.LaunchFailure,
            "attach_failure" => WorkerFailureKind.AttachFailure,
            "wrapper_failure" => WorkerFailureKind.WrapperFailure,
            "artifact_failure" => WorkerFailureKind.ArtifactFailure,
            "build_failure" => WorkerFailureKind.BuildFailure,
            "test_failure" => WorkerFailureKind.TestFailure,
            "contract_failure" => WorkerFailureKind.ContractFailure,
            "patch_failure" => WorkerFailureKind.PatchFailure,
            "policy_denied" => WorkerFailureKind.PolicyDenied,
            "task_logic_failed" => WorkerFailureKind.TaskLogicFailed,
            "timeout" => WorkerFailureKind.Timeout,
            "cancelled" => WorkerFailureKind.Cancelled,
            "aborted" => WorkerFailureKind.Aborted,
            "invalid_output" => WorkerFailureKind.InvalidOutput,
            "approval_required" => WorkerFailureKind.ApprovalRequired,
            _ => WorkerFailureKind.Unknown,
        };
    }

    private static WorkerThreadContinuity ParseThreadContinuity(
        string? threadContinuity,
        string? requestedPriorThreadId,
        CodexBridgeResponse response)
    {
        return threadContinuity?.ToLowerInvariant() switch
        {
            "new_thread" => WorkerThreadContinuity.NewThread,
            "resumed_thread" => WorkerThreadContinuity.ResumedThread,
            _ when !string.IsNullOrWhiteSpace(requestedPriorThreadId)
                && string.Equals(requestedPriorThreadId, ResolveThreadId(response), StringComparison.Ordinal)
                => WorkerThreadContinuity.ResumedThread,
            _ when !string.IsNullOrWhiteSpace(ResolveThreadId(response))
                => WorkerThreadContinuity.NewThread,
            _ => WorkerThreadContinuity.None,
        };
    }

    private static string? ResolveThreadId(CodexBridgeResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.ThreadId))
        {
            return response.ThreadId;
        }

        return response.Events?
            .Select(item =>
            {
                if (item.Attributes is not null && item.Attributes.TryGetValue("thread_id", out var threadId))
                {
                    return threadId;
                }

                return (string?)null;
            })
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
    }

    private static string? ResolveBridgeScript()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CARVES_CODEX_BRIDGE_SCRIPT");
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        var configuredRepoRoot = Environment.GetEnvironmentVariable("CARVES_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRepoRoot))
        {
            var repoCandidate = Path.Combine(configuredRepoRoot, "scripts", "codex-worker-bridge", "bridge.mjs");
            if (File.Exists(repoCandidate))
            {
                return repoCandidate;
            }
        }

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var candidate = Path.Combine(repoRoot, "scripts", "codex-worker-bridge", "bridge.mjs");
        return File.Exists(candidate) ? candidate : null;
    }

    private static bool HasAuthCredential()
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"))
               || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_API_KEY"));
    }

    private static string MapSandboxMode(WorkerSandboxMode mode)
    {
        return mode switch
        {
            WorkerSandboxMode.ReadOnly => "read-only",
            WorkerSandboxMode.WorkspaceWrite => "workspace-write",
            WorkerSandboxMode.DangerFullAccess => "danger-full-access",
            _ => "workspace-write",
        };
    }

    private static string MapApprovalMode(WorkerApprovalMode mode)
    {
        return mode switch
        {
            WorkerApprovalMode.Never => "never",
            WorkerApprovalMode.OnRequest => "on-request",
            WorkerApprovalMode.OnFailure => "on-failure",
            WorkerApprovalMode.Untrusted => "untrusted",
            _ => "untrusted",
        };
    }

    private sealed class CodexBridgeRequest
    {
        public string RequestId { get; init; } = string.Empty;
        public string TaskId { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Instructions { get; init; } = string.Empty;
        public string Input { get; init; } = string.Empty;
        public string RepoRoot { get; init; } = string.Empty;
        public string WorktreeRoot { get; init; } = string.Empty;
        public string BaseCommit { get; init; } = string.Empty;
        public string? PriorThreadId { get; init; }
        public string? Model { get; init; }
        public int TimeoutSeconds { get; init; }
        public string SandboxMode { get; init; } = "workspace-write";
        public string ApprovalMode { get; init; } = "untrusted";
        public bool NetworkAccessEnabled { get; init; }
        public string[] AllowedFiles { get; init; } = Array.Empty<string>();
        public string[] AllowedCommandPrefixes { get; init; } = Array.Empty<string>();
        public string[] DeniedCommandPrefixes { get; init; } = Array.Empty<string>();
        public string[][] ValidationCommands { get; init; } = Array.Empty<string[]>();
        public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class CodexBridgeResponse
    {
        public string? RunId { get; init; }
        public string? RequestId { get; init; }
        public string? RequestedPriorThreadId { get; init; }
        public string? ThreadId { get; init; }
        public string? ThreadContinuity { get; init; }
        public string? Status { get; init; }
        public string? FailureKind { get; init; }
        public bool Retryable { get; init; }
        public string? Summary { get; init; }
        public string? Rationale { get; init; }
        public string? FailureReason { get; init; }
        public string? Model { get; init; }
        public string[]? ChangedFiles { get; init; }
        public CodexBridgeEvent[]? Events { get; init; }
        public CodexBridgeCommandTrace[]? CommandTrace { get; init; }
        public DateTimeOffset? StartedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; init; }
    }

    private sealed class CodexBridgeEvent
    {
        public string? RunId { get; init; }
        public string? TaskId { get; init; }
        public string? EventType { get; init; }
        public string? Summary { get; init; }
        public string? ItemType { get; init; }
        public string? CommandText { get; init; }
        public string? FilePath { get; init; }
        public int? ExitCode { get; init; }
        public string? RawPayload { get; init; }
        public Dictionary<string, string>? Attributes { get; init; }
        public DateTimeOffset? OccurredAt { get; init; }
    }

    private sealed class CodexBridgeCommandTrace
    {
        public string[]? Command { get; init; }
        public int ExitCode { get; init; }
        public string? StandardOutput { get; init; }
        public string? StandardError { get; init; }
        public string? WorkingDirectory { get; init; }
        public string? Category { get; init; }
        public DateTimeOffset? CapturedAt { get; init; }
    }
}
