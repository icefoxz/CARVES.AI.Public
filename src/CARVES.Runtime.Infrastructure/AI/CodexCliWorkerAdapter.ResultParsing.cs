using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private WorkerExecutionResult ParseResult(
        WorkerExecutionRequest request,
        string requestPreview,
        string requestHash,
        string stdout,
        string stderr,
        int exitCode)
    {
        var adapterContext = new WorkerExecutionAdapterContext(
            BackendId,
            ProviderId,
            AdapterId,
            selectionReason,
            ProtocolFamily,
            RequestFamily);
        var lines = stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        var events = new List<WorkerEvent>();
        var commandTrace = new List<CommandExecutionRecord>();
        var changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var permissionRequests = new List<WorkerPermissionRequest>();
        string? runId = null;
        string? summary = null;
        string? rationale = null;
        var model = string.IsNullOrWhiteSpace(request.ModelOverride) ? "codex-cli" : request.ModelOverride;
        string? responsePreview = null;
        string? responseHash = null;
        int? inputTokens = null;
        int? outputTokens = null;
        string? threadId = null;
        var startedAt = DateTimeOffset.UtcNow;
        var completedAt = DateTimeOffset.UtcNow;
        WorkerExecutionStatus? explicitStatus = null;
        WorkerFailureKind failureKind = WorkerFailureKind.None;
        var failureReason = string.Empty;

        foreach (var line in lines)
        {
            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
                if (string.IsNullOrWhiteSpace(type))
                {
                    continue;
                }

                var rootChangedFilePaths = ExtractChangedFilePaths(root);
                if (rootChangedFilePaths.Count > 0)
                {
                    runId ??= $"worker-run-{Guid.NewGuid():N}";
                    AddChangedFileEvents(
                        request,
                        runId,
                        rootChangedFilePaths,
                        "codex_cli_changed_files",
                        line,
                        changedFiles,
                        events);
                }

                switch (type)
                {
                    case "thread.started":
                        threadId = TryGetString(root, "thread_id");
                        runId ??= threadId ?? $"worker-run-{Guid.NewGuid():N}";
                        events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.RunStarted, "Codex CLI thread started.", "thread", null, null, line));
                        break;
                    case "turn.started":
                        runId ??= $"worker-run-{Guid.NewGuid():N}";
                        startedAt = DateTimeOffset.UtcNow;
                        events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.TurnStarted, "Codex CLI turn started.", "turn", null, null, line));
                        break;
                    case "turn.completed":
                        runId ??= $"worker-run-{Guid.NewGuid():N}";
                        completedAt = DateTimeOffset.UtcNow;
                        explicitStatus ??= WorkerExecutionStatus.Succeeded;
                        if (root.TryGetProperty("usage", out var usage))
                        {
                            inputTokens = TryGetInt(usage, "input_tokens");
                            outputTokens = TryGetInt(usage, "output_tokens");
                        }

                        events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.TurnCompleted, "Codex CLI turn completed.", "turn", null, null, line));
                        break;
                    case "turn.failed":
                        runId ??= $"worker-run-{Guid.NewGuid():N}";
                        completedAt = DateTimeOffset.UtcNow;
                        explicitStatus = WorkerExecutionStatus.Failed;
                        failureKind = WorkerFailureKind.TaskLogicFailed;
                        failureReason = FirstNonEmpty(TryGetString(root, "message"), "Codex CLI reported a failed turn.");
                        events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.TurnFailed, failureReason, "turn", null, null, line));
                        break;
                    case "error":
                        runId ??= $"worker-run-{Guid.NewGuid():N}";
                        explicitStatus ??= WorkerExecutionStatus.Failed;
                        failureKind = ClassifyFailure(line, stderr, exitCode);
                        failureReason = FirstNonEmpty(TryGetString(root, "message"), line, stderr, "Codex CLI returned an error.");
                        events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.RawError, failureReason, "error", null, null, line));
                        break;
                    default:
                        if (!type.StartsWith("item.", StringComparison.Ordinal))
                        {
                            break;
                        }

                        runId ??= $"worker-run-{Guid.NewGuid():N}";
                        HandleItemEvent(
                            request,
                            runId,
                            type,
                            root,
                            line,
                            events,
                            commandTrace,
                            changedFiles,
                            permissionRequests,
                            ref summary,
                            ref rationale,
                            ref responsePreview,
                            ref responseHash,
                            ref explicitStatus,
                            ref failureKind,
                            ref failureReason);
                        break;
                }
            }
            catch (JsonException)
            {
                runId ??= $"worker-run-{Guid.NewGuid():N}";
                events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.RawError, line, "stdout", null, null, line));
                if (explicitStatus is null && exitCode != 0)
                {
                    explicitStatus = WorkerExecutionStatus.Failed;
                    failureKind = WorkerFailureKind.InvalidOutput;
                    failureReason = "Codex CLI emitted non-JSON worker output.";
                }
            }
            finally
            {
                document?.Dispose();
            }
        }

        runId ??= $"worker-run-{Guid.NewGuid():N}";
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.RawError, stderr.Trim(), "stderr", null, null, stderr.Trim()));
        }

        var finalStatus = explicitStatus ?? InferStatus(exitCode, failureKind, permissionRequests);
        if (failureKind == WorkerFailureKind.None && finalStatus != WorkerExecutionStatus.Succeeded)
        {
            failureKind = ClassifyFailure(stdout, stderr, exitCode);
        }

        if (string.IsNullOrWhiteSpace(failureReason) && finalStatus != WorkerExecutionStatus.Succeeded)
        {
            failureReason = InferFailureReason(stdout, stderr, exitCode, finalStatus);
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = finalStatus == WorkerExecutionStatus.Succeeded
                ? "Codex CLI worker completed."
                : FirstNonEmpty(failureReason, "Codex CLI worker did not complete successfully.");
        }

        return WorkerExecutionResultFactory.Create(
            request,
            adapterContext,
            new WorkerExecutionResultDetails
            {
                RunId = runId,
                Status = finalStatus,
                FailureKind = failureKind,
                FailureLayer = ResolveFailureLayer(failureKind, stdout, stderr, commandTrace, changedFiles.Count),
                Retryable = IsRetryable(failureKind),
                Configured = true,
                Model = model,
                RequestId = request.RequestId,
                ThreadId = threadId,
                RequestPreview = requestPreview,
                RequestHash = requestHash,
                Summary = summary,
                Rationale = rationale,
                FailureReason = string.IsNullOrWhiteSpace(failureReason) ? null : failureReason,
                ResponsePreview = responsePreview,
                ResponseHash = responseHash,
                ChangedFiles = changedFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray(),
                Events = events.ToArray(),
                PermissionRequests = permissionRequests.ToArray(),
                CommandTrace = commandTrace.ToArray(),
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                StartedAt = startedAt,
                CompletedAt = completedAt,
            });
    }

    private static void HandleItemEvent(
        WorkerExecutionRequest request,
        string runId,
        string type,
        JsonElement root,
        string rawLine,
        List<WorkerEvent> events,
        List<CommandExecutionRecord> commandTrace,
        HashSet<string> changedFiles,
        List<WorkerPermissionRequest> permissionRequests,
        ref string? summary,
        ref string? rationale,
        ref string? responsePreview,
        ref string? responseHash,
        ref WorkerExecutionStatus? explicitStatus,
        ref WorkerFailureKind failureKind,
        ref string failureReason)
    {
        if (!root.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var itemType = TryGetString(item, "type") ?? "item";
        var itemStatus = TryGetString(item, "status");
        AddChangedFileEvents(
            request,
            runId,
            ExtractChangedFilePaths(item),
            "codex_cli_item_changed_files",
            rawLine,
            changedFiles,
            events);
        switch (itemType)
        {
            case "agent_message":
            {
                var text = ExtractAgentMessageText(item);
                summary = FirstNonEmpty(text, summary);
                rationale ??= text;
                responsePreview = text?.Length > 160 ? text[..160] : text;
                responseHash = string.IsNullOrWhiteSpace(text)
                    ? responseHash
                    : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
                events.Add(BuildEvent(request.TaskId, runId, WorkerEventType.FinalSummary, FirstNonEmpty(text, "Codex CLI emitted an agent message."), itemType, null, null, rawLine));
                break;
            }
            case "command_execution":
            {
                var commandText = FirstNonEmpty(
                    TryGetString(item, "command"),
                    item.TryGetProperty("command", out var commandProperty) && commandProperty.ValueKind == JsonValueKind.Array
                        ? string.Join(' ', commandProperty.EnumerateArray().Select(value => value.ToString()))
                        : null);
                var exitCode = TryGetInt(item, "exit_code");
                var aggregatedOutput = TryGetString(item, "aggregated_output");
                var errorText = TryGetString(item, "error");
                events.Add(BuildEvent(
                    request.TaskId,
                    runId,
                    WorkerEventType.CommandExecuted,
                    FirstNonEmpty(commandText, "Codex CLI executed a command."),
                    itemType,
                    commandText,
                    exitCode,
                    rawLine));
                if (type == "item.completed")
                {
                    var nonFatalProbeFailure = IsNonFatalCommandProbeFailure(commandText, exitCode, aggregatedOutput, errorText);
                    commandTrace.Add(new CommandExecutionRecord(
                        SplitCommand(commandText),
                        exitCode ?? 0,
                        aggregatedOutput ?? string.Empty,
                        errorText ?? string.Empty,
                        string.Equals(itemStatus, "failed", StringComparison.OrdinalIgnoreCase) && !nonFatalProbeFailure,
                        request.WorktreeRoot,
                        "worker",
                        DateTimeOffset.UtcNow));

                    foreach (var inferredPath in ExtractShellChangedFiles(commandText))
                    {
                        if (!changedFiles.Add(inferredPath))
                        {
                            continue;
                        }

                        events.Add(BuildEvent(
                            request.TaskId,
                            runId,
                            WorkerEventType.FileEditObserved,
                            $"Codex CLI shell command wrote '{inferredPath}'.",
                            "command_inferred_file_change",
                            commandText,
                            exitCode,
                            rawLine,
                            inferredPath));
                    }

                    if (string.Equals(itemStatus, "failed", StringComparison.OrdinalIgnoreCase) && !nonFatalProbeFailure)
                    {
                        explicitStatus ??= InferFailedStatus(aggregatedOutput, errorText);
                        failureKind = ClassifyFailure(aggregatedOutput, errorText, exitCode ?? 1);
                        failureReason = FirstNonEmpty(aggregatedOutput, errorText, failureReason);
                    }
                }

                break;
            }
            case "file_change":
            {
                if (item.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        var path = TryGetString(change, "path");
                        if (string.IsNullOrWhiteSpace(path))
                        {
                            continue;
                        }

                        changedFiles.Add(path);
                        events.Add(BuildEvent(
                            request.TaskId,
                            runId,
                            WorkerEventType.FileEditObserved,
                            $"Codex CLI reported a file change: {path}.",
                            itemType,
                            null,
                            null,
                            rawLine,
                            path));
                    }
                }

                break;
            }
            case "permission_request":
            case "approval_request":
            {
                explicitStatus = WorkerExecutionStatus.ApprovalWait;
                failureKind = WorkerFailureKind.ApprovalRequired;
                failureReason = FirstNonEmpty(TryGetString(item, "message"), TryGetString(item, "prompt"), "Codex CLI is waiting for approval.");
                var eventType = itemType == "approval_request" ? WorkerEventType.ApprovalWait : WorkerEventType.PermissionRequested;
                events.Add(BuildEvent(request.TaskId, runId, eventType, failureReason, itemType, null, null, rawLine));
                permissionRequests.Add(new WorkerPermissionRequest
                {
                    RunId = runId,
                    TaskId = request.TaskId,
                    BackendId = "codex_cli",
                    ProviderId = "codex",
                    AdapterId = nameof(CodexCliWorkerAdapter),
                    ProfileId = request.Profile.ProfileId,
                    ScopeSummary = request.Profile.WorkspaceBoundary,
                    Summary = failureReason,
                    RawPrompt = failureReason,
                    RawPayload = rawLine,
                });
                break;
            }
        }
    }

}
