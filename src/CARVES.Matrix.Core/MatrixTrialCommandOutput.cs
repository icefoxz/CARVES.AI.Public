using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void WriteTrialResult(TrialCommandResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        if (result.Collection is null && result.Verification is null && result.LocalScore is null)
        {
            WriteTrialSetupResult(result);
            return;
        }

        Console.WriteLine("CARVES Agent Trial local score");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        Console.WriteLine("Meaning: checks this folder and writes a local score.");
        Console.WriteLine($"Result: {result.Status}");
        if (!string.IsNullOrWhiteSpace(result.WorkspaceRoot))
        {
            Console.WriteLine($"Workspace: {result.WorkspaceRoot}");
        }

        if (result.Collection is not null)
        {
            Console.WriteLine($"Collection: {result.Collection.LocalCollectionStatus}");
            if (result.Collection.FailureReasons.Count > 0)
            {
                Console.WriteLine($"Collection reasons: {string.Join(", ", result.Collection.FailureReasons)}");
            }
        }

        if (result.LocalScore is not null)
        {
            Console.WriteLine($"Final result: {FormatTrialResultPlain(result.Verification)}");
            Console.WriteLine($"Final score: {FormatTrialFinalScorePlain(result.LocalScore, result.Verification)}");
            Console.WriteLine($"Local dimension score: {FormatTrialLocalScorePlain(result.LocalScore)}");
            WriteTrialDimensionSummary(result.LocalScore.Dimensions, Console.Out);
        }

        if (result.Verification is not null)
        {
            Console.WriteLine($"Verification: {result.Verification.Status}");
            Console.WriteLine($"Trial artifacts: {(result.Verification.TrialArtifactsVerified ? "verified" : "not verified")}");
            if (result.Verification.ReasonCodes.Count > 0)
            {
                Console.WriteLine($"Verification reasons: {string.Join(", ", result.Verification.ReasonCodes)}");
            }
        }

        WriteTrialDiagnostics(result.Diagnostics, Console.Out);

        Console.WriteLine("Files:");
        if (result.ResultCard?.CardPath is not null)
        {
            Console.WriteLine($"- Result card: {Path.Combine(result.BundleRoot, result.ResultCard.CardPath)}");
        }

        Console.WriteLine($"- Submit bundle: {result.BundleRoot}");
        if (!string.IsNullOrWhiteSpace(result.EvidenceRoot))
        {
            Console.WriteLine($"- Agent evidence: {result.EvidenceRoot}");
        }

        Console.WriteLine($"- Verify again (developer): {FormatTrialVerifyCommandForDisplay(result.BundleRoot, result.VerifyCommand)}");

        if (result.ResultCard is not null)
        {
            Console.WriteLine();
            WriteResultCardMarkdownToConsole(result.ResultCard.Markdown);
        }

        Console.WriteLine("Next: run RESULT.cmd/result.sh to read this again, or RESET.cmd/reset.sh before testing another agent in the same package.");
    }

    private static void WriteTrialSetupResult(TrialCommandResult result)
    {
        Console.WriteLine($"CARVES Agent Trial local {result.Command}");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        Console.WriteLine($"Status: {result.Status}");
        if (!string.IsNullOrWhiteSpace(result.WorkspaceRoot))
        {
            Console.WriteLine($"Workspace: {result.WorkspaceRoot}");
        }

        Console.WriteLine($"Trial bundle root: {result.BundleRoot}");
        Console.WriteLine($"Verify: {result.VerifyCommand}");
        Console.WriteLine("Steps:");
        foreach (var step in result.Steps)
        {
            Console.WriteLine($"- {step}");
        }

        Console.WriteLine("Offline: yes; server submission: no.");
    }

    private static void WriteTrialDiagnostics(IReadOnlyList<TrialDiagnosticReadback> diagnostics, TextWriter writer)
    {
        if (diagnostics.Count == 0)
        {
            return;
        }

        writer.WriteLine("Diagnostics:");
        foreach (var diagnostic in diagnostics)
        {
            writer.WriteLine($"- [{diagnostic.Category}/{diagnostic.Code}] {diagnostic.Message}");
            writer.WriteLine($"  Evidence: {diagnostic.EvidenceRef}");
            if (!string.IsNullOrWhiteSpace(diagnostic.CommandRef))
            {
                writer.WriteLine($"  Command: {diagnostic.CommandRef}");
            }

            writer.WriteLine($"  Next: {diagnostic.NextStep}");
        }
    }

    private static string FormatTrialVerifyCommandForDisplay(string bundleRoot, string fallbackCommand)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var packageLocalWindowsScorer = Path.Combine(currentDirectory, "tools", "carves", "carves.exe");
        var packageLocalPosixScorer = Path.Combine(currentDirectory, "tools", "carves", "carves");

        if (OperatingSystem.IsWindows() && File.Exists(packageLocalWindowsScorer))
        {
            return $"tools\\carves\\carves.exe test verify {QuoteForDisplay(ToCurrentDirectoryRelativePath(bundleRoot))} --trial --json";
        }

        if (!OperatingSystem.IsWindows() && File.Exists(packageLocalPosixScorer))
        {
            return $"./tools/carves/carves test verify {QuoteForDisplay(ToCurrentDirectoryRelativePath(bundleRoot))} --trial --json";
        }

        return fallbackCommand;
    }

    private static string ToCurrentDirectoryRelativePath(string path)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var relativePath = Path.GetRelativePath(currentDirectory, Path.GetFullPath(path));
        return relativePath.StartsWith("..", StringComparison.Ordinal)
            ? path
            : relativePath;
    }

    private static void WriteTrialDimensionSummary(IReadOnlyList<TrialLocalDimensionScoreReadback> dimensions, TextWriter writer)
    {
        if (dimensions.Count == 0)
        {
            writer.WriteLine("Dimensions: not available");
            return;
        }

        writer.WriteLine("Dimensions:");
        foreach (var dimension in dimensions)
        {
            writer.WriteLine($"- {FormatTrialDimensionShortLabel(dimension.Dimension)}: {FormatDimensionScorePlain(dimension)} {FormatTrialDimensionStatus(dimension)}");
        }
    }

    private static string FormatTrialDimensionShortLabel(string dimension)
    {
        return dimension switch
        {
            "reviewability" => "Review",
            "traceability" => "Trace",
            "explainability" => "Explain",
            "report_honesty" => "Honesty",
            "constraint" => "Boundary",
            "reproducibility" => "Re-run",
            _ => dimension
        };
    }

    private static string FormatTrialDimensionStatus(TrialLocalDimensionScoreReadback dimension)
    {
        if (!dimension.Score.HasValue || dimension.Score.Value == 0 || dimension.Level is "failed" or "unavailable")
        {
            return "FAIL";
        }

        if (dimension.Score.Value < dimension.MaxScore || dimension.Level == "weak")
        {
            return "WEAK";
        }

        return "OK";
    }

    private static string FormatTrialResultPlain(TrialVerificationReadback? verification)
    {
        if (IsTrialResultCardVerified(verification))
        {
            return "GREEN VERIFIED";
        }

        if (verification is null)
        {
            return "YELLOW NOT VERIFIED (verification not run by this command)";
        }

        return $"RED UNVERIFIED ({verification.Status}; trial_artifacts_verified={FormatBool(verification.TrialArtifactsVerified)})";
    }

    private static string FormatTrialFinalScorePlain(TrialLocalScoreReadback localScore, TrialVerificationReadback? verification)
    {
        if (!IsTrialResultCardVerified(verification))
        {
            return "RED not verified (fix verification before treating this as a final score)";
        }

        return FormatTrialLocalScorePlain(localScore);
    }

    private static string FormatTrialLocalScorePlain(TrialLocalScoreReadback localScore)
    {
        return localScore.AggregateScore.HasValue
            ? $"{FormatScorePlain(localScore.AggregateScore.Value, localScore.MaxScore)} ({localScore.ScoreStatus})"
            : $"RED not scored ({localScore.ScoreStatus})";
    }

    private static string FormatDimensionScorePlain(TrialLocalDimensionScoreReadback dimension)
    {
        return dimension.Score.HasValue
            ? FormatScorePlain(dimension.Score.Value, dimension.MaxScore)
            : "RED not scored";
    }

    private static string FormatScorePlain(int score, int maxScore)
    {
        var band = ResolveScoreBandLabel(score, maxScore);
        return $"{band} {score}/{maxScore}";
    }

    private static void WriteResultCardMarkdownToConsole(string markdown)
    {
        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            WriteColorizedResultCardLine(line);
        }
    }

    private static void WriteColorizedResultCardLine(string line)
    {
        if (Console.IsOutputRedirected || !TryResolveResultCardLineColor(line, out var color))
        {
            Console.WriteLine(line);
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(line);
        Console.ForegroundColor = previous;
    }

    private static bool TryResolveResultCardLineColor(string line, out ConsoleColor color)
    {
        if (line.Contains("RED ", StringComparison.Ordinal))
        {
            color = ConsoleColor.Red;
            return true;
        }

        if (line.Contains("YELLOW ", StringComparison.Ordinal))
        {
            color = ConsoleColor.Yellow;
            return true;
        }

        if (line.Contains("GREEN ", StringComparison.Ordinal))
        {
            color = ConsoleColor.Green;
            return true;
        }

        color = default;
        return false;
    }
}
