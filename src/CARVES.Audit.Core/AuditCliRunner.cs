using System.Text.Json;

namespace Carves.Audit.Core;

public static class AuditCliRunner
{
    private static readonly string[] ProtectedOutputPrefixes =
    [
        ".git/",
        ".ai/tasks/",
        ".ai/memory/",
        ".ai/runtime/guard/",
        ".ai/handoff/",
    ];

    public static int Run(IReadOnlyList<string> arguments)
    {
        return Run(Directory.GetCurrentDirectory(), arguments, commandName: "carves-audit");
    }

    public static int Run(string workingDirectory, IReadOnlyList<string> arguments)
    {
        return Run(workingDirectory, arguments, commandName: "carves audit");
    }

    private static int Run(string workingDirectory, IReadOnlyList<string> arguments, string commandName)
    {
        if (arguments.Count == 0)
        {
            return WriteUsage(commandName);
        }

        var command = arguments[0].ToLowerInvariant();
        return command switch
        {
            "summary" => RunSummary(workingDirectory, arguments.Skip(1).ToArray()),
            "timeline" => RunTimeline(workingDirectory, arguments.Skip(1).ToArray()),
            "explain" => RunExplain(workingDirectory, arguments.Skip(1).ToArray(), commandName),
            "evidence" => RunEvidence(workingDirectory, arguments.Skip(1).ToArray()),
            "help" or "--help" or "-h" => WriteUsage(commandName, exitCode: 0),
            _ => WriteUsage(commandName),
        };
    }

    private static int RunSummary(string workingDirectory, IReadOnlyList<string> arguments)
    {
        if (!TryParseOptions(workingDirectory, arguments, out var options, out var json, out _))
        {
            return 2;
        }

        var service = new AuditService();
        var result = service.BuildSummary(options);
        WriteResult(result, json, WriteSummaryText);
        return string.Equals(result.ConfidencePosture, "input_error", StringComparison.Ordinal) ? 1 : 0;
    }

    private static int RunTimeline(string workingDirectory, IReadOnlyList<string> arguments)
    {
        if (!TryParseOptions(workingDirectory, arguments, out var options, out var json, out _))
        {
            return 2;
        }

        var service = new AuditService();
        var result = service.BuildTimeline(options);
        WriteResult(result, json, WriteTimelineText);
        return string.Equals(result.ConfidencePosture, "input_error", StringComparison.Ordinal) ? 1 : 0;
    }

    private static int RunExplain(string workingDirectory, IReadOnlyList<string> arguments, string commandName)
    {
        if (arguments.Count == 0 || arguments[0].StartsWith("--", StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"Usage: {commandName} explain <id> [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
            return 2;
        }

        var id = arguments[0];
        if (!TryParseOptions(workingDirectory, arguments.Skip(1).ToArray(), out var options, out var json, out _))
        {
            return 2;
        }

        var service = new AuditService();
        var result = service.Explain(options, id);
        WriteResult(result, json, WriteExplainText);
        return string.Equals(result.ConfidencePosture, "input_error", StringComparison.Ordinal)
               || !result.Found
               || result.Ambiguous
            ? 1
            : 0;
    }

    private static int RunEvidence(string workingDirectory, IReadOnlyList<string> arguments)
    {
        if (!TryParseOptions(workingDirectory, arguments, out var options, out var json, out var outputPath, allowOutput: true))
        {
            return 2;
        }

        var reader = new AuditInputReader();
        var snapshot = reader.Read(options);
        var result = new AuditEvidenceService(reader).BuildEvidence(options, snapshot);
        var payload = JsonSerializer.Serialize(result, AuditJsonContracts.EvidenceJsonOptions);
        if (!string.IsNullOrWhiteSpace(outputPath) && !TryWriteOutput(options.WorkingDirectory, outputPath, payload, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        if (json || string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(payload);
        }
        else
        {
            WriteEvidenceText(result, outputPath);
        }

        return snapshot.HasFatalInputError ? 1 : 0;
    }

    private static bool TryParseOptions(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        out AuditInputOptions options,
        out bool json,
        out string? outputPath,
        bool allowOutput = false)
    {
        json = false;
        outputPath = null;
        var guardDecisionPath = AuditInputReader.DefaultGuardDecisionPath;
        var guardDecisionPathExplicit = false;
        var handoffPacketPaths = new List<string>();
        var handoffPacketPathsExplicit = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (string.Equals(argument, "--guard-decisions", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadOptionValue(arguments, ref index, argument, out guardDecisionPath))
                {
                    options = EmptyOptions(workingDirectory);
                    return false;
                }

                guardDecisionPathExplicit = true;
                continue;
            }

            if (string.Equals(argument, "--handoff", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadOptionValue(arguments, ref index, argument, out var handoffPacketPath))
                {
                    options = EmptyOptions(workingDirectory);
                    return false;
                }

                handoffPacketPaths.Add(handoffPacketPath);
                handoffPacketPathsExplicit = true;
                continue;
            }

            if (allowOutput && string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryReadOptionValue(arguments, ref index, argument, out outputPath))
                {
                    options = EmptyOptions(workingDirectory);
                    return false;
                }

                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unknown option: {argument}");
                options = EmptyOptions(workingDirectory);
                return false;
            }

            Console.Error.WriteLine($"Unexpected argument: {argument}");
            options = EmptyOptions(workingDirectory);
            return false;
        }

        options = new AuditInputOptions(
            workingDirectory,
            guardDecisionPath,
            guardDecisionPathExplicit,
            handoffPacketPaths,
            handoffPacketPathsExplicit);
        return true;
    }

    private static bool TryReadOptionValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option,
        out string value)
    {
        if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
        {
            Console.Error.WriteLine($"{option} requires a value.");
            value = string.Empty;
            return false;
        }

        value = arguments[index + 1];
        index++;
        return true;
    }

    private static AuditInputOptions EmptyOptions(string workingDirectory)
    {
        return new AuditInputOptions(workingDirectory, AuditInputReader.DefaultGuardDecisionPath, false, [], false);
    }

    private static bool TryWriteOutput(string workingDirectory, string outputPath, string payload, out string? error)
    {
        error = null;
        try
        {
            if (!TryResolveSafeEvidenceOutputPath(workingDirectory, outputPath, out var resolvedPath, out error))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedPath, payload + Environment.NewLine);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = $"Evidence output could not be written: {ex.Message}";
            return false;
        }
    }

    private static bool TryResolveSafeEvidenceOutputPath(
        string workingDirectory,
        string outputPath,
        out string resolvedPath,
        out string? error)
    {
        resolvedPath = string.Empty;
        error = null;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            error = "Evidence output path cannot be empty.";
            return false;
        }

        var repositoryRoot = Path.GetFullPath(workingDirectory);
        var repositoryRootWithSeparator = EnsureTrailingSeparator(repositoryRoot);
        resolvedPath = Path.GetFullPath(Path.IsPathRooted(outputPath)
            ? outputPath
            : Path.Combine(repositoryRoot, outputPath));
        if (!resolvedPath.StartsWith(repositoryRootWithSeparator, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(resolvedPath, repositoryRoot, StringComparison.OrdinalIgnoreCase))
        {
            error = "Evidence output path must stay inside the repository. Use .carves/shield-evidence.json or another non-truth path.";
            return false;
        }

        var relativePath = Path.GetRelativePath(repositoryRoot, resolvedPath).Replace('\\', '/');
        if (relativePath.StartsWith("../", StringComparison.Ordinal)
            || string.Equals(relativePath, "..", StringComparison.Ordinal))
        {
            error = "Evidence output path must stay inside the repository. Use .carves/shield-evidence.json or another non-truth path.";
            return false;
        }

        if (ProtectedOutputPrefixes.Any(prefix => relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"Evidence output path '{relativePath}' is protected. Write generated evidence under .carves/ or artifacts/ instead.";
            return false;
        }

        return true;
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    private static void WriteResult<T>(T result, bool json, Action<T> textWriter)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, AuditJsonContracts.JsonOptions));
            return;
        }

        textWriter(result);
    }

    private static void WriteSummaryText(AuditSummaryResult result)
    {
        Console.WriteLine("CARVES Audit summary");
        Console.WriteLine($"Posture: {result.ConfidencePosture}");
        Console.WriteLine($"Events: {result.EventCount}");
        Console.WriteLine($"Guard: total={result.Guard.TotalCount}, allow={result.Guard.AllowCount}, review={result.Guard.ReviewCount}, block={result.Guard.BlockCount}");
        Console.WriteLine($"Guard input: {result.Guard.InputStatus} ({result.Guard.InputPath})");
        Console.WriteLine($"Latest Guard run: {result.Guard.LatestRunId ?? "(none)"}");
        Console.WriteLine($"Handoff: supplied={result.Handoff.SuppliedPacketCount}, loaded={result.Handoff.LoadedPacketCount}, errors={result.Handoff.InputErrorCount}");
        Console.WriteLine($"Latest Handoff: {result.Handoff.LatestHandoffId ?? "(none)"}");
    }

    private static void WriteTimelineText(AuditTimelineResult result)
    {
        Console.WriteLine("CARVES Audit timeline");
        Console.WriteLine($"Posture: {result.ConfidencePosture}");
        Console.WriteLine($"Events: {result.EventCount}");
        foreach (var item in result.Events)
        {
            Console.WriteLine($"- {item.OccurredAtUtc:O} | {item.SourceProduct} | {item.SubjectId} | {item.Status} | {item.Summary ?? "(none)"}");
        }
    }

    private static void WriteExplainText(AuditExplainResult result)
    {
        Console.WriteLine("CARVES Audit explain");
        Console.WriteLine($"Id: {result.Id}");
        Console.WriteLine($"Found: {result.Found.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Ambiguous: {result.Ambiguous.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Posture: {result.ConfidencePosture}");
        foreach (var match in result.Matches)
        {
            Console.WriteLine($"- {match.SourceProduct} | {match.SubjectId} | {match.Status} | {match.Summary ?? "(none)"}");
        }
    }

    private static void WriteEvidenceText(ShieldEvidenceDocument result, string outputPath)
    {
        Console.WriteLine("CARVES Audit evidence");
        Console.WriteLine($"Schema: {result.SchemaVersion}");
        Console.WriteLine($"Evidence: {result.EvidenceId}");
        Console.WriteLine($"Mode: {result.ModeHint}");
        Console.WriteLine($"Wrote: {outputPath}");
        Console.WriteLine($"Guard enabled: {result.Dimensions.Guard.Enabled.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Handoff enabled: {result.Dimensions.Handoff.Enabled.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Audit enabled: {result.Dimensions.Audit.Enabled.ToString().ToLowerInvariant()}");
        if (result.Provenance.Warnings.Count > 0)
        {
            Console.WriteLine($"Warnings: {string.Join(", ", result.Provenance.Warnings)}");
        }
    }

    private static int WriteUsage(string commandName, int exitCode = 2)
    {
        var output = exitCode == 0 ? Console.Out : Console.Error;
        output.WriteLine($"Usage: {commandName} summary [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
        output.WriteLine($"       {commandName} timeline [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
        output.WriteLine($"       {commandName} explain <id> [--json] [--guard-decisions <path>] [--handoff <packet-path>]...");
        output.WriteLine($"       {commandName} evidence [--json] [--output <path>] [--guard-decisions <path>] [--handoff <packet-path>]...");
        output.WriteLine($"       Defaults: Guard {AuditInputReader.DefaultGuardDecisionPath}; Handoff {AuditInputReader.DefaultHandoffPacketPath}.");
        output.WriteLine("       Read-only Audit Alpha; evidence output must stay inside the repo and outside .git/.ai truth paths.");
        return exitCode;
    }
}
