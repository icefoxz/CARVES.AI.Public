using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter : IWorkerAdapter
{
    private readonly string selectionReason;
    private static readonly string[] SupportedReasoningEfforts = ["low", "medium", "high"];
    private const string ProtocolFamily = "local_cli";
    private const string RequestFamily = "delegated_worker_launch";

    public CodexCliWorkerAdapter(string selectionReason)
    {
        this.selectionReason = selectionReason;
    }

    public string AdapterId => nameof(CodexCliWorkerAdapter);

    public string BackendId => "codex_cli";

    public string ProviderId => "codex";

    public bool IsConfigured => TryResolveCodexCommand() is not null;

    public bool IsRealAdapter => true;

    public string SelectionReason => selectionReason;

    public WorkerExecutionResult Execute(WorkerExecutionRequest request)
    {
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Input))).ToLowerInvariant();
        var requestPreview = request.Input.Length > 160 ? request.Input[..160] : request.Input;
        var command = TryResolveCodexCommand();
        if (command is null)
        {
            return WorkerExecutionResult.Blocked(
                request.TaskId,
                BackendId,
                ProviderId,
                AdapterId,
                request.Profile,
                WorkerFailureKind.EnvironmentBlocked,
                "Codex CLI is not installed or CARVES_CODEX_CLI_PATH is invalid.",
                requestPreview,
                requestHash,
                failureLayer: WorkerFailureLayer.Environment,
                protocolFamily: ProtocolFamily,
                requestFamily: RequestFamily);
        }

        var launchContract = BuildLaunchContract(request);
        var preflight = RunPreflight(command, request, launchContract, requestPreview, requestHash);
        if (!preflight.Allowed)
        {
            return preflight.BlockedResult!;
        }

        var prompt = BuildPrompt(request);
        var startInfo = BuildStartInfo(command, request, launchContract);
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
                "Failed to start Codex CLI process.",
                requestPreview,
                requestHash,
                failureLayer: WorkerFailureLayer.Environment,
                protocolFamily: ProtocolFamily,
                requestFamily: RequestFamily,
                events: preflight.Events,
                commandTrace: preflight.CommandTrace);
        }

        var processResult = ExecuteProcess(process, prompt, request);
        var result = ParseResult(
            request,
            requestPreview,
            requestHash,
            processResult.StandardOutput,
            processResult.StandardError,
            processResult.ExitCode);
        var observedChangedFiles = ObserveActualChangedFiles(request.WorktreeRoot);
        if (processResult.TimedOut)
        {
            var timeoutEvidence = DescribeTimeout(result, processResult.TimeoutSeconds);
            result = result with
            {
                Status = WorkerExecutionStatus.Failed,
                FailureKind = WorkerFailureKind.Timeout,
                FailureLayer = WorkerFailureLayer.Transport,
                Retryable = true,
                Summary = timeoutEvidence.Summary,
                FailureReason = timeoutEvidence.FailureReason,
                TimeoutPhase = timeoutEvidence.Phase,
                TimeoutEvidence = timeoutEvidence.Evidence,
            };
        }

        return result with
        {
            ProtocolFamily = ProtocolFamily,
            RequestFamily = RequestFamily,
            ObservedChangedFiles = observedChangedFiles,
            Events = preflight.Events
                .Select(item => new WorkerEvent
                {
                    EventId = item.EventId,
                    RunId = result.RunId,
                    TaskId = item.TaskId,
                    EventType = item.EventType,
                    Summary = item.Summary,
                    ItemType = item.ItemType,
                    CommandText = item.CommandText,
                    FilePath = item.FilePath,
                    ExitCode = item.ExitCode,
                    RawPayload = item.RawPayload,
                    Attributes = item.Attributes,
                    OccurredAt = item.OccurredAt,
                })
                .Concat(result.Events)
                .Concat(BuildObservedChangedFileEvents(request.TaskId, result.RunId, observedChangedFiles, result.ChangedFiles))
                .ToArray(),
            CommandTrace = preflight.CommandTrace.Concat(result.CommandTrace).ToArray(),
        };
    }

    private sealed record CodexCommand(string FileName, IReadOnlyList<string> PrefixArguments, string CommandPath);

    private sealed record ProbeResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, int TimeoutSeconds);

    private sealed record TimeoutPhaseEvidence(string Phase, string Evidence, string Summary, string FailureReason);

    private sealed record PreflightResult(bool Allowed, WorkerExecutionResult? BlockedResult, IReadOnlyList<WorkerEvent> Events, IReadOnlyList<CommandExecutionRecord> CommandTrace);

    private sealed record ProbeCommandResult(IReadOnlyList<string> Command, int ExitCode, string StandardOutput, string StandardError)
    {
        public string RawPayload => JsonSerializer.Serialize(new
        {
            command = Command,
            exit_code = ExitCode,
            stdout = StandardOutput,
            stderr = StandardError,
        });

        public CommandExecutionRecord ToRecord(string workingDirectory, string category)
        {
            return new CommandExecutionRecord(Command, ExitCode, StandardOutput, StandardError, false, workingDirectory, category, DateTimeOffset.UtcNow);
        }
    }
}
