using System.Text.Json;
using Carves.Runtime.Application.Shield;

namespace Carves.Shield.Core;

public static class ShieldCliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static int Run(IReadOnlyList<string> arguments)
    {
        var parsed = ParseStandaloneArguments(arguments);
        if (!string.IsNullOrWhiteSpace(parsed.Error))
        {
            Console.Error.WriteLine(parsed.Error);
            return 2;
        }

        var repoRoot = ResolveRepoRoot(parsed.RepoRootOverride);
        if (repoRoot is null)
        {
            Console.Error.WriteLine("Not a git repository.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Next:");
            Console.Error.WriteLine("  run git init or switch to a project folder");
            return 1;
        }

        return Run(repoRoot, parsed.CommandArguments, commandName: "carves-shield");
    }

    public static int Run(string repoRoot, IReadOnlyList<string> arguments, string commandName = "carves shield")
    {
        if (arguments.Count == 0)
        {
            return WriteShieldUsage(commandName);
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "evaluate" => RunShieldEvaluate(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "badge" => RunShieldBadge(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "challenge" => RunShieldChallenge(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "help" or "--help" or "-h" => WriteShieldUsage(commandName, exitCode: 0),
            _ => WriteShieldUsage(commandName),
        };
    }

    private static int RunShieldEvaluate(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var outputRaw = ResolveOption(arguments, "--output") ?? "combined";
        if (!TryParseOutput(outputRaw, out var output))
        {
            Console.Error.WriteLine("Invalid --output value. Use lite, standard, or combined.");
            return 2;
        }

        var evidencePath = ResolveEvidencePath(arguments);
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            WriteShieldEvaluateUsage(commandName);
            return 2;
        }

        var result = new ShieldEvaluationService().EvaluateFile(repoRoot, evidencePath, output);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            WriteShieldEvaluateText(result);
        }

        return result.IsOk ? 0 : 1;
    }

    private static int RunShieldChallenge(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var challengePackPath = ResolveChallengePackPath(arguments);
        if (string.IsNullOrWhiteSpace(challengePackPath))
        {
            WriteShieldChallengeUsage(commandName);
            return 2;
        }

        var result = new ShieldLiteChallengeRunner().RunFile(repoRoot, challengePackPath);
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else
        {
            WriteShieldChallengeText(result);
        }

        return result.IsPassed ? 0 : 1;
    }

    private static int RunShieldBadge(string repoRoot, IReadOnlyList<string> arguments, string commandName)
    {
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        var outputPath = ResolveOption(arguments, "--output");
        var evidencePath = ResolveEvidencePath(arguments);
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            WriteShieldBadgeUsage(commandName);
            return 2;
        }

        var result = new ShieldBadgeService().CreateFromEvidenceFile(repoRoot, evidencePath, outputPath);
        if (result.IsOk && result.Badge is not null && !string.IsNullOrWhiteSpace(outputPath))
        {
            if (!TryWriteBadgeOutput(repoRoot, outputPath, result.Badge.Svg, out var error))
            {
                var writeFailure = result with
                {
                    Status = ShieldEvaluationStatuses.InvalidInput,
                    Badge = null,
                    Errors =
                    [
                        new ShieldEvaluationError(
                            "badge_write_failed",
                            error ?? "Badge output could not be written.",
                            ["argument.output"]),
                    ],
                };
                if (json)
                {
                    Console.WriteLine(JsonSerializer.Serialize(writeFailure, JsonOptions));
                }
                else
                {
                    WriteShieldBadgeText(writeFailure);
                }

                return 1;
            }
        }

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
        }
        else if (result.IsOk && result.Badge is not null && string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(result.Badge.Svg);
        }
        else
        {
            WriteShieldBadgeText(result);
        }

        return result.IsOk ? 0 : 1;
    }

    private static int WriteShieldUsage(string commandName, int exitCode = 2)
    {
        var output = exitCode == 0 ? Console.Out : Console.Error;
        output.WriteLine($"Usage: {commandName} <evaluate|badge|challenge> ...");
        output.WriteLine($"       {commandName} evaluate <evidence-path> [--json] [--output <lite|standard|combined>]");
        output.WriteLine($"       {commandName} badge <evidence-path> [--json] [--output <svg-path>]");
        output.WriteLine($"       {commandName} challenge <challenge-pack-path> [--json]");
        if (string.Equals(commandName, "carves-shield", StringComparison.Ordinal))
        {
            output.WriteLine("       Optional: --repo-root <path>");
        }

        output.WriteLine("       Local-only Shield self-check; no source, raw diff, prompt, secret, or credential upload.");
        return exitCode;
    }

    private static void WriteShieldEvaluateUsage(string commandName)
    {
        Console.Error.WriteLine($"Usage: {commandName} evaluate <evidence-path> [--json] [--output <lite|standard|combined>]");
    }

    private static void WriteShieldBadgeUsage(string commandName)
    {
        Console.Error.WriteLine($"Usage: {commandName} badge <evidence-path> [--json] [--output <svg-path>]");
    }

    private static void WriteShieldChallengeUsage(string commandName)
    {
        Console.Error.WriteLine($"Usage: {commandName} challenge <challenge-pack-path> [--json]");
    }

    private static void WriteShieldEvaluateText(ShieldEvaluationResult result)
    {
        Console.WriteLine("CARVES Shield evaluate");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Posture: {result.EvaluationPosture}");
        Console.WriteLine($"Privacy: {result.PrivacyPosture}");
        Console.WriteLine($"Certification: {result.Certification.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(result.ConsumedEvidenceSha256))
        {
            Console.WriteLine($"Evidence SHA-256: {result.ConsumedEvidenceSha256}");
        }

        if (result.Standard is not null)
        {
            Console.WriteLine($"Standard: {result.Standard.Label}");
            foreach (var dimension in result.Standard.Dimensions.Values)
            {
                Console.WriteLine($"  - {dimension.Dimension}: {dimension.Level} ({dimension.Band})");
            }
        }

        if (result.Lite is not null)
        {
            Console.WriteLine($"Lite: {result.Lite.Score}/100 ({result.Lite.Band})");
            foreach (var contribution in result.Lite.DimensionContributions)
            {
                Console.WriteLine($"  - {contribution.Key}: level {contribution.Value.StandardLevel}, weight {contribution.Value.Weight}, points {contribution.Value.Points}");
            }
        }

        WriteErrors(result.Errors);
    }

    private static void WriteShieldChallengeText(ShieldLiteChallengeRunResult result)
    {
        Console.WriteLine("CARVES Shield challenge");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Summary label: {result.SummaryLabel}");
        Console.WriteLine($"Local-only: {result.LocalOnly.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Public claims: none");
        Console.WriteLine($"Cases: {result.PassedCount}/{result.CaseCount} passed");
        Console.WriteLine($"Pass rate: {result.PassRate:0.####}");
        foreach (var challengeCase in result.Results)
        {
            Console.WriteLine($"  - {challengeCase.CaseId} {challengeCase.ChallengeKind}: {challengeCase.Status}");
            if (challengeCase.Issues.Count > 0)
            {
                Console.WriteLine($"    issues: {string.Join(", ", challengeCase.Issues)}");
            }
        }
    }

    private static void WriteShieldBadgeText(ShieldBadgeResult result)
    {
        Console.WriteLine("CARVES Shield badge");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Self-check: {result.SelfCheck.ToString().ToLowerInvariant()}");
        Console.WriteLine($"Certification: {result.Certification.ToString().ToLowerInvariant()}");
        if (result.Badge is not null)
        {
            Console.WriteLine($"Message: {result.Badge.Message}");
            Console.WriteLine($"Color: {result.Badge.ColorName}");
            Console.WriteLine($"Standard: {result.Badge.StandardCompact}");
            Console.WriteLine($"Markdown: {result.Badge.Markdown}");
            if (!string.IsNullOrWhiteSpace(result.OutputPath))
            {
                Console.WriteLine($"Wrote: {result.OutputPath}");
            }
        }

        WriteErrors(result.Errors);
    }

    private static void WriteErrors(IReadOnlyList<ShieldEvaluationError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine("Errors:");
        foreach (var error in errors)
        {
            Console.WriteLine($"  - {error.Code}: {error.Message}");
            if (error.EvidenceRefs.Count > 0)
            {
                Console.WriteLine($"    refs: {string.Join(", ", error.EvidenceRefs)}");
            }
        }
    }

    private static bool TryParseOutput(string value, out ShieldEvaluationOutput output)
    {
        output = value.ToLowerInvariant() switch
        {
            "lite" => ShieldEvaluationOutput.Lite,
            "standard" => ShieldEvaluationOutput.Standard,
            "combined" => ShieldEvaluationOutput.Combined,
            _ => ShieldEvaluationOutput.Combined,
        };

        return value.Equals("lite", StringComparison.OrdinalIgnoreCase)
            || value.Equals("standard", StringComparison.OrdinalIgnoreCase)
            || value.Equals("combined", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static string? ResolveEvidencePath(IReadOnlyList<string> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(argument, "--output", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return null;
    }

    private static string? ResolveChallengePackPath(IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            if (string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return argument;
            }
        }

        return null;
    }

    private static bool TryWriteBadgeOutput(string repoRoot, string outputPath, string content, out string? error)
    {
        error = null;
        try
        {
            var resolvedPath = Path.IsPathRooted(outputPath)
                ? outputPath
                : Path.GetFullPath(Path.Combine(repoRoot, outputPath));
            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedPath, content);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ShieldCliArguments ParseStandaloneArguments(IReadOnlyList<string> arguments)
    {
        string? repoRoot = null;
        var remaining = new List<string>();

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--repo-root", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count || string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return new ShieldCliArguments(null, [], "--repo-root requires a value.");
                }

                repoRoot = Path.GetFullPath(arguments[index + 1]);
                index++;
                continue;
            }

            remaining.Add(argument);
        }

        return new ShieldCliArguments(repoRoot, remaining, null);
    }

    private static string? ResolveRepoRoot(string? explicitRepoRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRepoRoot))
        {
            var path = Path.GetFullPath(explicitRepoRoot);
            return Directory.Exists(path) ? path : null;
        }

        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (IsGitRepository(current.FullName))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    private static bool IsGitRepository(string path)
    {
        var gitPath = Path.Combine(Path.GetFullPath(path), ".git");
        return Directory.Exists(gitPath) || File.Exists(gitPath);
    }

    private sealed record ShieldCliArguments(
        string? RepoRootOverride,
        IReadOnlyList<string> CommandArguments,
        string? Error);
}
