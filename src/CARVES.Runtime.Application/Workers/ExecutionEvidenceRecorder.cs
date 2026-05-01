using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Workers;

public sealed class ExecutionEvidenceRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly ControlPlanePaths paths;
    private readonly IGitClient? gitClient;

    public ExecutionEvidenceRecorder(ControlPlanePaths paths, IGitClient? gitClient = null)
    {
        this.paths = paths;
        this.gitClient = gitClient;
    }

    public ExecutionEvidence Record(TaskRunReport report)
    {
        var result = report.WorkerExecution;
        var runRoot = Path.Combine(paths.WorkerExecutionArtifactsRoot, result.RunId);
        Directory.CreateDirectory(runRoot);

        var workerTrace = result.CommandTrace;
        var validationTrace = report.Validation.CommandResults;
        var allTrace = workerTrace.Concat(validationTrace).ToArray();

        var declaredScopeFiles = report.Request.Task.Scope
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var workerEffectiveChangedFiles = result.ChangedFiles
            .Concat(result.ObservedChangedFiles)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var filesRead = workerEffectiveChangedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var filesWritten = (report.Patch.Paths.Count == 0 ? workerEffectiveChangedFiles : report.Patch.Paths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var commands = allTrace
            .Select(item => string.Join(' ', item.Command))
            .Where(command => !string.IsNullOrWhiteSpace(command))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var commandLogPath = WriteText(runRoot, "command.log", BuildCommandLog(allTrace));
        var buildLogPath = WriteText(runRoot, "build.log", BuildFilteredLog(allTrace, "build"));
        var testLogPath = WriteText(runRoot, "test.log", BuildFilteredLog(allTrace, "test"));
        var patchPath = WriteText(runRoot, "patch.diff", BuildPatchArtifact(report, filesWritten));

        var artifacts = new List<string>();
        AddArtifact(artifacts, commandLogPath);
        AddArtifact(artifacts, buildLogPath);
        AddArtifact(artifacts, testLogPath);
        AddArtifact(artifacts, patchPath);

        var completeness = ResolveCompleteness(commands, filesWritten, artifacts);
        var artifactHashes = BuildArtifactHashes(commandLogPath, buildLogPath, testLogPath, patchPath);
        var commandTraceHash = commandLogPath is null ? null : ComputeFileHash(commandLogPath);
        var patchHash = patchPath is null ? null : ComputeFileHash(patchPath);
        var evidencePath = Path.Combine(runRoot, "evidence.json");
        var evidence = new ExecutionEvidence
        {
            RunId = result.RunId,
            TaskId = report.TaskId,
            WorkerId = string.IsNullOrWhiteSpace(result.AdapterId) ? report.Session.WorkerAdapterName : result.AdapterId,
            StartedAt = result.StartedAt,
            EndedAt = result.CompletedAt,
            EvidenceSource = allTrace.Length > 0 || filesWritten.Length > 0
                ? ExecutionEvidenceSource.Host
                : ExecutionEvidenceSource.Synthetic,
            DeclaredScopeFiles = declaredScopeFiles,
            FilesRead = filesRead,
            FilesWritten = filesWritten,
            CommandsExecuted = commands,
            RepoRoot = paths.RepoRoot,
            WorktreePath = NormalizePath(report.WorktreePath ?? report.Session.WorktreeRoot),
            BaseCommit = string.IsNullOrWhiteSpace(report.Session.CurrentCommit) ? null : report.Session.CurrentCommit,
            RequestedThreadId = report.Session.RequestedWorkerThreadId,
            ThreadId = report.Session.WorkerThreadId ?? result.ThreadId,
            ThreadContinuity = report.Session.WorkerThreadContinuity == WorkerThreadContinuity.None
                ? result.ThreadContinuity
                : report.Session.WorkerThreadContinuity,
            EvidencePath = ToRepoRelativePath(evidencePath),
            BuildOutputRef = ToRepoRelativePath(buildLogPath),
            TestOutputRef = ToRepoRelativePath(testLogPath),
            CommandLogRef = ToRepoRelativePath(commandLogPath),
            PatchRef = ToRepoRelativePath(patchPath),
            Artifacts = artifacts
                .Select(ToRepoRelativePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray()!,
            ArtifactHashes = artifactHashes,
            ExitStatus = workerTrace.LastOrDefault(item => !item.Skipped)?.ExitCode,
            EvidenceCompleteness = completeness,
            EvidenceStrength = ResolveStrength(
                completeness,
                commandLogPath,
                buildLogPath,
                testLogPath,
                patchPath,
                report,
                commands,
                filesWritten),
            CommandTraceHash = commandTraceHash,
            PatchHash = patchHash,
        };

        File.WriteAllText(evidencePath, JsonSerializer.Serialize(evidence, JsonOptions));
        return evidence;
    }

    private static ExecutionEvidenceCompleteness ResolveCompleteness(
        IReadOnlyCollection<string> commands,
        IReadOnlyCollection<string> filesWritten,
        IReadOnlyCollection<string> artifacts)
    {
        if (commands.Count == 0 && filesWritten.Count == 0 && artifacts.Count == 0)
        {
            return ExecutionEvidenceCompleteness.Missing;
        }

        if (commands.Count == 0 || artifacts.Count == 0)
        {
            return ExecutionEvidenceCompleteness.Partial;
        }

        return ExecutionEvidenceCompleteness.Complete;
    }

    private static ExecutionEvidenceStrength ResolveStrength(
        ExecutionEvidenceCompleteness completeness,
        string? commandLogPath,
        string? buildLogPath,
        string? testLogPath,
        string? patchPath,
        TaskRunReport report,
        IReadOnlyCollection<string> commands,
        IReadOnlyCollection<string> filesWritten)
    {
        if (completeness == ExecutionEvidenceCompleteness.Missing)
        {
            return ExecutionEvidenceStrength.Missing;
        }

        var hasCommandTrace = !string.IsNullOrWhiteSpace(commandLogPath) && commands.Count > 0;
        var hasPatchArtifact = filesWritten.Count == 0 || !string.IsNullOrWhiteSpace(patchPath);
        var hasBuildArtifact = !ContainsKeyword(commands, "build") || !string.IsNullOrWhiteSpace(buildLogPath);
        var hasTestArtifact = !ContainsKeyword(commands, "test") || !string.IsNullOrWhiteSpace(testLogPath);
        var hasExitStatus = report.WorkerExecution.CommandTrace.Count == 0 || report.WorkerExecution.CommandTrace.Any(item => !item.Skipped);

        if (!(hasCommandTrace && hasPatchArtifact && hasBuildArtifact && hasTestArtifact && hasExitStatus))
        {
            return ExecutionEvidenceStrength.Observed;
        }

        var hasReplayInputs =
            !string.IsNullOrWhiteSpace(report.Session.CurrentCommit)
            && !string.IsNullOrWhiteSpace(report.WorktreePath ?? report.Session.WorktreeRoot)
            && !string.IsNullOrWhiteSpace(commandLogPath)
            && (!filesWritten.Any() || !string.IsNullOrWhiteSpace(patchPath));

        return hasReplayInputs
            ? ExecutionEvidenceStrength.Replayable
            : ExecutionEvidenceStrength.Verifiable;
    }

    private string? WriteText(string directory, string fileName, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string BuildCommandLog(IReadOnlyList<CommandExecutionRecord> trace)
    {
        if (trace.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var item in trace)
        {
            builder.AppendLine($"> {string.Join(' ', item.Command)}");
            builder.AppendLine($"category: {item.Category}");
            builder.AppendLine($"exit_code: {item.ExitCode}");
            builder.AppendLine($"working_directory: {item.WorkingDirectory}");
            if (!string.IsNullOrWhiteSpace(item.StandardOutput))
            {
                builder.AppendLine("stdout:");
                builder.AppendLine(item.StandardOutput);
            }

            if (!string.IsNullOrWhiteSpace(item.StandardError))
            {
                builder.AppendLine("stderr:");
                builder.AppendLine(item.StandardError);
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string BuildFilteredLog(IReadOnlyList<CommandExecutionRecord> trace, string keyword)
    {
        var relevant = trace
            .Where(item =>
                item.Command.Any(part => part.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || item.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (relevant.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            relevant.Select(item =>
            {
                var builder = new StringBuilder();
                builder.AppendLine($"> {string.Join(' ', item.Command)}");
                if (!string.IsNullOrWhiteSpace(item.StandardOutput))
                {
                    builder.AppendLine(item.StandardOutput);
                }

                if (!string.IsNullOrWhiteSpace(item.StandardError))
                {
                    builder.AppendLine(item.StandardError);
                }

                return builder.ToString().Trim();
            }));
    }

    private string BuildPatchArtifact(TaskRunReport report, IReadOnlyList<string> filesWritten)
    {
        if (filesWritten.Count == 0 && report.Patch.FilesChanged == 0)
        {
            return string.Empty;
        }

        var worktreePath = report.WorktreePath ?? report.Session.WorktreeRoot;
        if (!string.IsNullOrWhiteSpace(worktreePath)
            && gitClient is not null
            && gitClient.IsRepository(worktreePath))
        {
            var diff = gitClient.TryGetUncommittedDiff(worktreePath, filesWritten);
            if (!string.IsNullOrWhiteSpace(diff))
            {
                return diff.Trim();
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine("# Synthetic Patch Artifact");
        builder.AppendLine($"files_changed: {report.Patch.FilesChanged}");
        builder.AppendLine($"lines_added: {report.Patch.LinesAdded}");
        builder.AppendLine($"lines_removed: {report.Patch.LinesRemoved}");
        builder.AppendLine($"estimated: {report.Patch.Estimated}");
        builder.AppendLine();
        foreach (var path in filesWritten)
        {
            builder.AppendLine($"diff --carves a/{path} b/{path}");
            builder.AppendLine($"--- a/{path}");
            builder.AppendLine($"+++ b/{path}");
            builder.AppendLine("@@");
            builder.AppendLine($"+ host recorded a file change for {path}, but no git diff was available in this execution context");
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyDictionary<string, string> BuildArtifactHashes(params string?[] artifactPaths)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in artifactPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            hashes[Path.GetFileName(path!)] = ComputeFileHash(path!);
        }

        return hashes;
    }

    private static string ComputeFileHash(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static bool ContainsKeyword(IEnumerable<string> commands, string keyword)
    {
        return commands.Any(command => command.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private void AddArtifact(List<string> artifacts, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            artifacts.Add(path);
        }
    }

    private string? ToRepoRelativePath(string? absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return null;
        }

        return Path.GetRelativePath(paths.RepoRoot, absolutePath)
            .Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string? NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? null
            : path.Replace(Path.DirectorySeparatorChar, '/');
    }
}
