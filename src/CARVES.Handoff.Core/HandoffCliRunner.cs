using System.Text.Json;

namespace Carves.Handoff.Core;

public static class HandoffCliRunner
{
    public static int Run(IReadOnlyList<string> arguments)
    {
        var parsed = ParseStandaloneArguments(arguments);
        if (!string.IsNullOrWhiteSpace(parsed.Error))
        {
            Console.Error.WriteLine(parsed.Error);
            return 2;
        }

        return Run(parsed.RepoRoot, parsed.CommandArguments, commandName: "carves-handoff");
    }

    public static int Run(string repoRoot, IReadOnlyList<string> arguments)
    {
        return Run(repoRoot, arguments, commandName: "carves handoff");
    }

    private static int Run(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        if (arguments.Count == 0)
        {
            return WriteHandoffUsage(commandName);
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "inspect" => RunHandoffInspect(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "draft" => RunHandoffDraft(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "next" => RunHandoffNext(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "help" or "--help" or "-h" => WriteHandoffUsage(commandName, exitCode: 0),
            _ => WriteHandoffUsage(commandName),
        };
    }

    private static int RunHandoffInspect(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        if (!TryParseHandoffPacketPath(arguments, $"{commandName} inspect [packet-path] [--json]", out var packetPath, out var json))
        {
            return 2;
        }

        var result = new HandoffInspectionService().Inspect(repoRoot, packetPath);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, HandoffJsonContracts.JsonOptions));
        }
        else
        {
            WriteHandoffInspectionText(result);
        }

        return result.Readiness.Decision is "ready" or "done" ? 0 : 1;
    }

    private static int RunHandoffDraft(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        if (!TryParseHandoffPacketPath(arguments, $"{commandName} draft [packet-path] [--json]", out var packetPath, out var json))
        {
            return 2;
        }

        var result = new HandoffDraftService().Draft(repoRoot, packetPath);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, HandoffJsonContracts.JsonOptions));
        }
        else
        {
            WriteHandoffDraftText(result);
        }

        return IsSuccessfulDraft(result) ? 0 : 1;
    }

    private static int RunHandoffNext(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        if (!TryParseHandoffPacketPath(arguments, $"{commandName} next [packet-path] [--json]", out var packetPath, out var json))
        {
            return 2;
        }

        var result = new HandoffProjectionService().Project(repoRoot, packetPath);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, HandoffJsonContracts.JsonOptions));
        }
        else
        {
            WriteHandoffNextText(result);
        }

        return result.Action is "continue" or "no_action" ? 0 : 1;
    }

    private static HandoffCliArguments ParseStandaloneArguments(IReadOnlyList<string> arguments)
    {
        var repoRoot = ResolveDefaultRepoRoot();
        var remaining = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--repo-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return new HandoffCliArguments(repoRoot, [], "--repo-root requires a value.");
                }

                repoRoot = Path.GetFullPath(arguments[index + 1]);
                index++;
                continue;
            }

            remaining.Add(argument);
        }

        return new HandoffCliArguments(repoRoot, remaining, null);
    }

    private static string ResolveDefaultRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (IsGitRepository(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static bool IsGitRepository(string path)
    {
        var gitPath = Path.Combine(Path.GetFullPath(path), ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private static bool TryParseHandoffPacketPath(
        IReadOnlyList<string> arguments,
        string usage,
        out string packetPath,
        out bool json)
    {
        packetPath = string.Empty;
        json = false;
        foreach (var argument in arguments)
        {
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
            {
                json = true;
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"Unknown option: {argument}");
                Console.Error.WriteLine($"Usage: {usage}");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(packetPath))
            {
                Console.Error.WriteLine($"Usage: {usage}");
                Console.Error.WriteLine("       Exactly one packet path is allowed.");
                return false;
            }

            packetPath = argument;
        }

        if (string.IsNullOrWhiteSpace(packetPath))
        {
            packetPath = HandoffDefaults.DefaultPacketPath;
        }

        return true;
    }

    private static int WriteHandoffUsage(string commandName, int exitCode = 2)
    {
        var output = exitCode == 0 ? Console.Out : Console.Error;
        output.WriteLine($"Usage: {commandName} inspect [packet-path] [--json]");
        output.WriteLine($"       {commandName} draft [packet-path] [--json]");
        output.WriteLine($"       {commandName} next [packet-path] [--json]");
        if (string.Equals(commandName, "carves-handoff", StringComparison.Ordinal))
        {
            output.WriteLine("       Optional: --repo-root <path> (default: nearest git repository or current directory)");
        }

        output.WriteLine($"       Default packet path: {HandoffDefaults.DefaultPacketPath}");
        output.WriteLine("       Handoff draft writes only low-confidence skeletons and refuses overwrite.");
        output.WriteLine("       Handoff next is a read-only raw projection.");
        output.WriteLine("       Completed packets return no_action.");
        if (string.Equals(commandName, "carves handoff", StringComparison.Ordinal))
        {
            output.WriteLine("       Run `carves help handoff` for exit codes and product boundaries.");
        }

        return exitCode;
    }

    private static bool IsSuccessfulDraft(HandoffDraftResult result)
    {
        if (!result.Written || result.Inspection is null)
        {
            return false;
        }

        return result.Inspection.Readiness.Decision is "ready" or "operator_review_required" or "blocked";
    }

    private static void WriteHandoffInspectionText(HandoffInspectionResult result)
    {
        Console.WriteLine("CARVES Handoff inspect");
        Console.WriteLine($"Packet: {result.PacketPath}");
        Console.WriteLine($"Inspection: {result.InspectionStatus}");
        Console.WriteLine($"Readiness: {result.Readiness.Decision}");
        Console.WriteLine($"Reason: {result.Readiness.Reason}");
        Console.WriteLine($"Handoff: {result.HandoffId ?? "(none)"}");
        Console.WriteLine($"Resume status: {result.ResumeStatus ?? "(none)"}");
        Console.WriteLine($"Confidence: {result.Confidence ?? "(none)"}");
        Console.WriteLine($"Context refs: {result.ContextRefs.Count}");
        Console.WriteLine($"Evidence refs: {result.EvidenceRefs.Count}");
        Console.WriteLine($"Decision refs: {result.DecisionRefs.Count}");
        Console.WriteLine($"Must not repeat: {result.MustNotRepeat.Count}");
        Console.WriteLine($"Blocked reasons: {result.BlockedReasons.Count}");

        WriteDiagnostics(result.Diagnostics);
    }

    private static void WriteHandoffDraftText(HandoffDraftResult result)
    {
        Console.WriteLine("CARVES Handoff draft");
        Console.WriteLine($"Packet: {result.PacketPath}");
        Console.WriteLine($"Draft: {result.DraftStatus}");
        Console.WriteLine($"Written: {result.Written}");
        Console.WriteLine($"Handoff: {result.HandoffId ?? "(none)"}");

        if (result.Inspection is not null)
        {
            Console.WriteLine($"Inspection: {result.Inspection.InspectionStatus}");
            Console.WriteLine($"Readiness: {result.Inspection.Readiness.Decision}");
            Console.WriteLine($"Reason: {result.Inspection.Readiness.Reason}");
        }

        WriteDiagnostics(result.Diagnostics);
    }

    private static void WriteHandoffNextText(HandoffProjectionResult result)
    {
        Console.WriteLine("CARVES Handoff next");
        Console.WriteLine($"Packet: {result.PacketPath}");
        Console.WriteLine($"Action: {result.Action}");
        Console.WriteLine($"Inspection: {result.InspectionStatus}");
        Console.WriteLine($"Readiness: {result.Readiness.Decision}");
        Console.WriteLine($"Reason: {result.Readiness.Reason}");
        Console.WriteLine($"Handoff: {result.HandoffId ?? "(none)"}");
        Console.WriteLine($"Resume status: {result.ResumeStatus ?? "(none)"}");
        Console.WriteLine($"Confidence: {result.Confidence ?? "(none)"}");
        Console.WriteLine($"Objective: {result.CurrentObjective ?? "(none)"}");
        Console.WriteLine($"Context refs: {result.ContextRefs.Count}");
        Console.WriteLine($"Evidence refs: {result.EvidenceRefs.Count}");
        Console.WriteLine($"Decision refs: {result.DecisionRefs.Count}");
        Console.WriteLine($"Must not repeat: {result.MustNotRepeat.Count}");
        Console.WriteLine($"Blocked reasons: {result.BlockedReasons.Count}");

        if (result.RecommendedNextAction is not null)
        {
            Console.WriteLine($"Next action: {result.RecommendedNextAction.Action}");
        }

        WriteDiagnostics(result.Diagnostics);
    }

    private static void WriteDiagnostics(IReadOnlyList<HandoffInspectionDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Diagnostics:");
        foreach (var diagnostic in diagnostics)
        {
            Console.WriteLine($"  - {diagnostic.Severity}: {diagnostic.Code} | {diagnostic.Message}");
        }
    }

    private sealed record HandoffCliArguments(
        string RepoRoot,
        IReadOnlyList<string> CommandArguments,
        string? Error);
}
