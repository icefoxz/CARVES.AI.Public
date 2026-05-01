using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int RunTrialRecord(MatrixTrialOptions options)
    {
        var bundleRoot = Path.GetFullPath(options.BundleRoot!);
        var historyRoot = Path.GetFullPath(options.HistoryRoot!);
        RejectHistoryInsideBundle(historyRoot, bundleRoot);

        var verification = BuildVerifyResult(bundleRoot, requireTrial: true);
        var entry = BuildTrialHistoryEntry(bundleRoot, options.RunId, verification);
        var entryRef = WriteTrialHistoryEntry(historyRoot, entry);
        var result = new TrialHistoryRecordResult(
            TrialHistoryRecordSchemaVersion,
            "record",
            "recorded",
            Offline: true,
            ServerSubmission: false,
            entryRef,
            entry,
            [
                "local_history_only",
                "not_server_receipt",
                "not_leaderboard_eligible",
                "not_certification"
            ]);

        WriteTrialHistoryRecord(result, options.Json);
        return 0;
    }

    private static int RunTrialCompare(MatrixTrialOptions options)
    {
        var historyRoot = ResolveTrialCompareHistoryRoot(options);
        var baseline = ReadTrialHistoryEntry(historyRoot, options.BaselineRunId!);
        var target = ReadTrialHistoryEntry(historyRoot, options.TargetRunId!);
        var result = BuildTrialHistoryComparison(baseline, target);
        WriteTrialHistoryCompare(result, options.Json);
        return 0;
    }

    private static string WriteTrialHistoryEntry(string historyRoot, TrialHistoryEntry entry)
    {
        var runsRoot = Path.Combine(historyRoot, "runs");
        Directory.CreateDirectory(runsRoot);
        var entryRef = "runs/" + entry.RunId + ".json";
        var entryPath = Path.Combine(runsRoot, entry.RunId + ".json");
        if (File.Exists(entryPath))
        {
            throw new InvalidOperationException("Local history entry already exists. Choose a different --run-id.");
        }

        File.WriteAllText(entryPath, JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine);
        return entryRef;
    }

    private static TrialHistoryEntry ReadTrialHistoryEntry(string historyRoot, string runId)
    {
        var normalizedRunId = SanitizeRunId(runId);
        var entryPath = Path.Combine(historyRoot, "runs", normalizedRunId + ".json");
        if (!File.Exists(entryPath))
        {
            throw new FileNotFoundException("Local history entry was not found.", "runs/" + normalizedRunId + ".json");
        }

        var entry = JsonSerializer.Deserialize<TrialHistoryEntry>(File.ReadAllText(entryPath), JsonOptions)
            ?? throw new InvalidDataException("Local history entry is empty.");
        if (!string.Equals(entry.SchemaVersion, TrialHistoryEntrySchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported local history entry schema version.");
        }

        return entry;
    }

    private static void WriteTrialHistoryRecord(TrialHistoryRecordResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        Console.WriteLine("Agent Trial local history record");
        Console.WriteLine($"Status: {result.Status}");
        Console.WriteLine($"Run id: {result.Entry.RunId}");
        Console.WriteLine($"History entry: {result.HistoryEntryRef}");
        Console.WriteLine($"Score: {FormatNullableScore(result.Entry.Score.AggregateScore, result.Entry.Score.MaxScore)}");
        Console.WriteLine($"Matrix verified: {result.Entry.MatrixVerified.ToString().ToLowerInvariant()}");
        Console.WriteLine("Non-claims: local history only; not a server receipt; not certification.");
    }

    private static void WriteTrialHistoryCompare(TrialHistoryCompareResult result, bool json)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return;
        }

        Console.WriteLine("Agent Trial local history compare");
        Console.WriteLine($"Mode: {result.ComparisonMode}");
        Console.WriteLine($"Baseline: {result.BaselineRunId}");
        Console.WriteLine($"Target: {result.TargetRunId}");
        Console.WriteLine($"Aggregate: {FormatMovement(result.AggregateScore)}");
        if (result.ReasonCodes.Count > 0)
        {
            Console.WriteLine($"Reason codes: {string.Join(", ", result.ReasonCodes)}");
        }

        Console.WriteLine(result.Explanation);
        Console.WriteLine("Dimensions:");
        foreach (var dimension in result.Dimensions)
        {
            Console.WriteLine($"- {dimension.Dimension}: {FormatMovement(new TrialHistoryScoreMovement(dimension.Baseline, dimension.Target, dimension.Delta))}");
        }
    }

    private static string FormatMovement(TrialHistoryScoreMovement movement)
    {
        var delta = movement.Delta.HasValue ? movement.Delta.Value.ToString("+#;-#;0") : "n/a";
        return $"{FormatNullableScore(movement.Baseline, null)} -> {FormatNullableScore(movement.Target, null)} ({delta})";
    }

    private static string FormatNullableScore(int? score, int? maxScore)
    {
        if (!score.HasValue)
        {
            return "not scored";
        }

        return maxScore.HasValue ? $"{score.Value}/{maxScore.Value}" : score.Value.ToString();
    }

    private static void RejectHistoryInsideBundle(string historyRoot, string bundleRoot)
    {
        if (IsPathInsideRoot(historyRoot, bundleRoot))
        {
            throw new InvalidOperationException("History root must be outside the verified bundle root.");
        }
    }

    private static bool IsPathInsideRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }

    private static string SanitizeRunId(string value)
    {
        var sanitized = new string(value.Trim()
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '-')
            .ToArray()).Trim('-', '_', '.');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            throw new ArgumentException("Run id must contain at least one letter or digit.", nameof(value));
        }

        return sanitized.Length > 80 ? sanitized[..80] : sanitized;
    }
}
