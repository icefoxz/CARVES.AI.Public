using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static TimeoutPhaseEvidence DescribeTimeout(WorkerExecutionResult result, int timeoutSeconds)
    {
        var phase = DetermineTimeoutPhase(result);
        var observedEvents = result.Events
            .Where(item => item.EventType != WorkerEventType.RawError)
            .Select(item => item.EventType.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var workerCommands = result.CommandTrace
            .Where(item => string.Equals(item.Category, "worker", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var evidenceParts = new List<string>
        {
            $"Observed CLI events before timeout: {(observedEvents.Length == 0 ? "none" : string.Join(", ", observedEvents))}.",
            $"Worker commands recorded: {workerCommands.Length}.",
            $"Changed files observed: {result.ChangedFiles.Count}.",
        };

        var lastWorkerCommand = workerCommands.LastOrDefault();
        if (lastWorkerCommand is not null && lastWorkerCommand.Command.Count > 0)
        {
            evidenceParts.Add($"Last worker command: {string.Join(' ', lastWorkerCommand.Command)}.");
        }

        var nonConvergingRescanEvidence = DescribeNonConvergingControlPlaneRescan(workerCommands, result.ChangedFiles.Count);
        if (!string.IsNullOrWhiteSpace(nonConvergingRescanEvidence))
        {
            evidenceParts.Add(nonConvergingRescanEvidence);
        }

        var nonConvergingSourceExplorationEvidence = DescribeNonConvergingSourceExploration(workerCommands, result.ChangedFiles.Count);
        if (!string.IsNullOrWhiteSpace(nonConvergingSourceExplorationEvidence))
        {
            evidenceParts.Add(nonConvergingSourceExplorationEvidence);
        }

        var nonConvergingPostEditReadbackEvidence = DescribeNonConvergingPostEditReadback(workerCommands, result.ChangedFiles);
        if (!string.IsNullOrWhiteSpace(nonConvergingPostEditReadbackEvidence))
        {
            evidenceParts.Add(nonConvergingPostEditReadbackEvidence);
        }

        var summary = phase switch
        {
            "before_first_cli_event" => $"Codex CLI timed out after {timeoutSeconds} second(s) before the first meaningful CLI event.",
            "bootstrap" => $"Codex CLI timed out after {timeoutSeconds} second(s) during bootstrap before execution activity began.",
            _ => $"Codex CLI timed out after {timeoutSeconds} second(s) after execution activity began.",
        };
        var evidence = string.Join(" ", evidenceParts);
        return new TimeoutPhaseEvidence(
            phase,
            evidence,
            summary,
            $"{summary} {evidence}".Trim());
    }

    private static string? DescribeNonConvergingControlPlaneRescan(
        IReadOnlyList<CommandExecutionRecord> workerCommands,
        int changedFileCount)
    {
        if (changedFileCount != 0 || workerCommands.Count == 0)
        {
            return null;
        }

        var rescanCommands = workerCommands
            .Where(IsControlPlaneRescanCommand)
            .ToArray();
        if (rescanCommands.Length == 0)
        {
            return null;
        }

        if (workerCommands.Any(LooksLikeWriteCommand))
        {
            return null;
        }

        var targets = rescanCommands
            .SelectMany(ExtractControlPlaneTargets)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var targetSummary = targets.Length == 0 ? "control-plane truth" : string.Join(", ", targets);
        return $"Non-converging read-only control-plane rescan detected: {rescanCommands.Length} of {workerCommands.Count} worker commands re-enumerated {targetSummary} with zero changed files.";
    }

    private static string? DescribeNonConvergingSourceExploration(
        IReadOnlyList<CommandExecutionRecord> workerCommands,
        int changedFileCount)
    {
        if (changedFileCount != 0 || workerCommands.Count == 0)
        {
            return null;
        }

        if (workerCommands.Any(LooksLikeWriteCommand))
        {
            return null;
        }

        var explorationCommands = workerCommands
            .Where(IsBroadSourceExplorationCommand)
            .ToArray();
        if (explorationCommands.Length < 3)
        {
            return null;
        }

        var targets = explorationCommands
            .SelectMany(ExtractSourceExplorationTargets)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var targetSummary = targets.Length == 0
            ? "source modules"
            : string.Join(", ", targets.Take(5));
        if (targets.Length > 5)
        {
            targetSummary += ", ...";
        }

        return $"Non-converging source exploration detected: {explorationCommands.Length} of {workerCommands.Count} worker commands kept widening source inspection with zero changed files ({targetSummary}).";
    }

    private static string? DescribeNonConvergingPostEditReadback(
        IReadOnlyList<CommandExecutionRecord> workerCommands,
        IReadOnlyList<string> changedFiles)
    {
        if (changedFiles.Count == 0 || workerCommands.Count == 0)
        {
            return null;
        }

        if (!workerCommands.Any(LooksLikeWriteCommand))
        {
            return null;
        }

        var tailWindow = Math.Min(3, workerCommands.Count);
        var readbackCommands = workerCommands
            .Skip(workerCommands.Count - tailWindow)
            .Where(record => LooksLikeReadbackCommand(record, changedFiles))
            .ToArray();
        if (readbackCommands.Length == 0)
        {
            return null;
        }

        var rereadTargets = changedFiles
            .Where(path => readbackCommands.Any(record => CommandMentionsPath(record, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (rereadTargets.Length == 0)
        {
            return null;
        }

        var targetSummary = string.Join(", ", rereadTargets.Take(5));
        if (rereadTargets.Length > 5)
        {
            targetSummary += ", ...";
        }

        return $"Non-converging post-edit readback detected: {readbackCommands.Length} worker commands reread changed files after edits were already observed ({targetSummary}).";
    }

    private static bool IsControlPlaneRescanCommand(CommandExecutionRecord record)
    {
        var commandText = string.Join(' ', record.Command);
        var touchesControlPlaneTruth = commandText.Contains(".ai/tasks/cards", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains(".ai/tasks/nodes", StringComparison.OrdinalIgnoreCase);
        if (!touchesControlPlaneTruth)
        {
            return false;
        }

        return commandText.Contains("Get-ChildItem", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("rg --files", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Select-String", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Get-Content", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExtractControlPlaneTargets(CommandExecutionRecord record)
    {
        var commandText = string.Join(' ', record.Command);
        if (commandText.Contains(".ai/tasks/cards", StringComparison.OrdinalIgnoreCase))
        {
            yield return ".ai/tasks/cards";
        }

        if (commandText.Contains(".ai/tasks/nodes", StringComparison.OrdinalIgnoreCase))
        {
            yield return ".ai/tasks/nodes";
        }
    }

    private static bool IsBroadSourceExplorationCommand(CommandExecutionRecord record)
    {
        var commandText = string.Join(' ', record.Command);
        if (commandText.Contains(".ai/tasks/cards", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains(".ai/tasks/nodes", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var targets = ExtractSourceExplorationTargets(record);
        if (targets.Length == 0)
        {
            return false;
        }

        if (LooksLikeSearchCommand(commandText))
        {
            return true;
        }

        if (!LooksLikeReadCommand(commandText))
        {
            return false;
        }

        return targets.Length > 1
            || commandText.Contains("&&", StringComparison.Ordinal)
            || commandText.Contains("---FILE---", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ExtractSourceExplorationTargets(CommandExecutionRecord record)
    {
        return record.Command
            .Select(NormalizePathToken)
            .Where(IsSourceExplorationPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool LooksLikeReadbackCommand(CommandExecutionRecord record, IReadOnlyList<string> changedFiles)
    {
        var commandText = string.Join(' ', record.Command);
        if (!LooksLikeReadCommand(commandText))
        {
            return false;
        }

        return changedFiles.Any(path => CommandMentionsPath(record, path));
    }

    private static bool LooksLikeReadCommand(string commandText)
    {
        return commandText.Contains("sed -n", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("cat ", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("head ", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("tail ", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Get-Content", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeSearchCommand(string commandText)
    {
        return commandText.Contains("rg -n", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("rg --files", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Select-String", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathToken(string token)
    {
        return token.Trim('\'', '"');
    }

    private static bool IsSourceExplorationPath(string token)
    {
        return token.StartsWith("src/", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("tests/", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("docs/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool CommandMentionsPath(CommandExecutionRecord record, string path)
    {
        var commandText = string.Join(' ', record.Command);
        return commandText.Contains(path, StringComparison.OrdinalIgnoreCase)
            || commandText.Contains(path.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeWriteCommand(CommandExecutionRecord record)
    {
        var commandText = string.Join(' ', record.Command);
        return commandText.Contains("Set-Content", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Add-Content", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("New-Item", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Remove-Item", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Move-Item", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("Copy-Item", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("git apply", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("apply_patch", StringComparison.OrdinalIgnoreCase)
            || commandText.Contains("write_text(", StringComparison.Ordinal)
            || commandText.Contains("write_bytes(", StringComparison.Ordinal);
    }

    private static string DetermineTimeoutPhase(WorkerExecutionResult result)
    {
        if (HasExecutionActivity(result))
        {
            return "execution_started";
        }

        if (result.Events.Any(item => item.EventType is WorkerEventType.RunStarted or WorkerEventType.TurnStarted))
        {
            return "bootstrap";
        }

        return "before_first_cli_event";
    }

    private static bool HasExecutionActivity(WorkerExecutionResult result)
    {
        if (result.CommandTrace.Any(item => string.Equals(item.Category, "worker", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (result.ChangedFiles.Count > 0)
        {
            return true;
        }

        return result.Events.Any(item => item.EventType is WorkerEventType.CommandExecuted
            or WorkerEventType.FileEditObserved
            or WorkerEventType.FinalSummary
            or WorkerEventType.PermissionRequested
            or WorkerEventType.ApprovalWait
            or WorkerEventType.TurnCompleted
            or WorkerEventType.TurnFailed);
    }

    private static string InferFailureReason(string stdout, string stderr, int exitCode, WorkerExecutionStatus status)
    {
        return status == WorkerExecutionStatus.ApprovalWait
            ? FirstNonEmpty(stderr, stdout, "Codex CLI is waiting for approval.")
            : FirstNonEmpty(stderr, stdout, $"Codex CLI exited with code {exitCode}.");
    }
}
