using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string TrialLatestPointerSchemaVersion = "matrix-agent-trial-latest-pointer.v0";
    private const string TrialLatestPointerFileName = "latest.json";

    private sealed record TrialLatestPointer(
        string SchemaVersion,
        string RunId,
        string CreatedAt,
        string UpdatedAt,
        string Status,
        string RunRoot,
        string WorkspaceRoot,
        string BundleRoot,
        string? ResultCardPath,
        string? HistoryEntryPath,
        bool NonAuthoritative,
        bool ManifestCovered,
        IReadOnlyList<string> NonClaims);

    private sealed record TrialLatestResult(
        string SchemaVersion,
        string Command,
        string Status,
        bool Offline,
        bool ServerSubmission,
        string LatestPointerPath,
        TrialLatestPointer Latest,
        string? ResultCardMarkdown,
        IReadOnlyList<string> NonClaims);

    private static int RunTrialLatest(MatrixTrialOptions options)
    {
        var trialRoot = ResolveTrialRoot(options);
        var latestPath = Path.Combine(trialRoot, TrialLatestPointerFileName);
        var latest = ReadTrialLatestPointer(latestPath);
        var result = new TrialLatestResult(
            TrialCommandSchemaVersion,
            "latest",
            "latest_found",
            Offline: true,
            ServerSubmission: false,
            latestPath,
            latest,
            ReadResultCardMarkdown(latest.ResultCardPath),
            BuildTrialLatestNonClaims());

        WriteTrialLatestResult(result, options.Json);
        return 0;
    }

    private static int RunTrialResult(MatrixTrialOptions options)
    {
        var portablePackage = TryResolvePortablePackageCollectPaths(options);
        if (portablePackage is not null
            && string.IsNullOrWhiteSpace(options.WorkspaceRoot)
            && string.IsNullOrWhiteSpace(options.TrialRoot)
            && string.IsNullOrWhiteSpace(options.HistoryRoot)
            && string.IsNullOrWhiteSpace(options.BundleRoot))
        {
            return RunPortablePackageResult(options, portablePackage);
        }

        return RunTrialLatest(options);
    }

    private static int RunPortablePackageResult(MatrixTrialOptions options, PortablePackageCollectPaths paths)
    {
        var cardPath = Path.Combine(paths.LocalResultsRoot, "matrix-agent-trial-result-card.md");
        if (!File.Exists(cardPath))
        {
            return WriteTrialFailure(
                "result",
                options.Json,
                [
                    new TrialDiagnosticReadback(
                        "trial_result_card_missing",
                        "user_setup",
                        "error",
                        "No previous local result card was found in this portable package.",
                        "results/local/matrix-agent-trial-result-card.md",
                        "carves test collect",
                        "Run SCORE.cmd or score.sh after the agent writes agent-workspace/artifacts/agent-report.json.",
                        ["portable_result_card_missing"])
                ]);
        }

        var markdown = File.ReadAllText(cardPath);
        if (options.Json)
        {
            var result = new
            {
                schema_version = TrialCommandSchemaVersion,
                command = "result",
                status = "result_found",
                offline = true,
                server_submission = false,
                result_card_path = cardPath,
                result_card_markdown = markdown,
            };
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        Console.WriteLine("CARVES Agent Trial local result");
        Console.WriteLine("Mode: local only (this computer only; no upload, no certification, no leaderboard).");
        WriteResultCardMarkdownToConsole(markdown);
        return 0;
    }

    private static string ResolveTrialVerifyBundleRoot(MatrixTrialOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.BundleRoot))
        {
            return Path.GetFullPath(options.BundleRoot);
        }

        var latest = ReadTrialLatestPointer(Path.Combine(ResolveTrialRoot(options), TrialLatestPointerFileName));
        if (string.IsNullOrWhiteSpace(latest.BundleRoot))
        {
            throw new InvalidDataException("Latest local trial pointer is missing bundle_root.");
        }

        return Path.GetFullPath(latest.BundleRoot);
    }

    private static string ResolveTrialCompareHistoryRoot(MatrixTrialOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.HistoryRoot))
        {
            return Path.GetFullPath(options.HistoryRoot);
        }

        var latest = ReadTrialLatestPointer(Path.Combine(ResolveTrialRoot(options), TrialLatestPointerFileName));
        if (string.IsNullOrWhiteSpace(latest.HistoryEntryPath))
        {
            throw new InvalidDataException("Latest local trial pointer is missing history_entry_path.");
        }

        var runsRoot = Path.GetDirectoryName(Path.GetFullPath(latest.HistoryEntryPath))
            ?? throw new InvalidDataException("Latest local trial pointer has an invalid history_entry_path.");
        return Path.GetDirectoryName(runsRoot)
            ?? throw new InvalidDataException("Latest local trial pointer has an invalid history_entry_path.");
    }

    private static void WriteTrialLatestPointer(TrialRunPaths paths, TrialOneCommandResult result)
    {
        if (IsPathInsideRoot(paths.LatestPointerPath, result.BundleRoot))
        {
            throw new InvalidOperationException("Latest pointer must stay outside the verified bundle root.");
        }

        var pointer = new TrialLatestPointer(
            TrialLatestPointerSchemaVersion,
            paths.RunId,
            paths.CreatedAt,
            DateTimeOffset.UtcNow.ToString("O"),
            result.Status,
            paths.RunRoot,
            paths.WorkspaceRoot,
            result.BundleRoot,
            ResolveResultCardPath(result),
            ResolveHistoryEntryPath(result),
            NonAuthoritative: true,
            ManifestCovered: false,
            BuildTrialLatestNonClaims());

        WriteJsonAtomically(paths.LatestPointerPath, pointer);
    }

    private static string ResolveTrialRoot(MatrixTrialOptions options)
    {
        return Path.GetFullPath(string.IsNullOrWhiteSpace(options.TrialRoot)
            ? Path.Combine(Directory.GetCurrentDirectory(), "carves-trials")
            : options.TrialRoot);
    }

    private static TrialLatestPointer ReadTrialLatestPointer(string latestPath)
    {
        if (!File.Exists(latestPath))
        {
            throw new FileNotFoundException("Latest local trial pointer was not found.", latestPath);
        }

        try
        {
            var latest = JsonSerializer.Deserialize<TrialLatestPointer>(File.ReadAllText(latestPath), JsonOptions)
                ?? throw new InvalidDataException("Latest local trial pointer is empty.");
            ValidateTrialLatestPointer(latest);
            return latest;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Latest local trial pointer is invalid JSON.", ex);
        }
    }

    private static void ValidateTrialLatestPointer(TrialLatestPointer latest)
    {
        if (!string.Equals(latest.SchemaVersion, TrialLatestPointerSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Unsupported latest local trial pointer schema version.");
        }

        if (!latest.NonAuthoritative || latest.ManifestCovered)
        {
            throw new InvalidDataException("Latest local trial pointer must be UX-only and outside Matrix manifest coverage.");
        }

        if (string.IsNullOrWhiteSpace(latest.RunId)
            || string.IsNullOrWhiteSpace(latest.BundleRoot)
            || string.IsNullOrWhiteSpace(latest.Status))
        {
            throw new InvalidDataException("Latest local trial pointer is missing required metadata.");
        }
    }

    private static void WriteJsonAtomically<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions) + Environment.NewLine);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static IReadOnlyList<string> BuildTrialLatestNonClaims()
    {
        return
        [
            "ux_pointer_only",
            "non_authoritative",
            "not_manifest_covered",
            "not_server_receipt",
            "not_leaderboard_eligible",
            "not_certification"
        ];
    }

    private static string? ResolveResultCardPath(TrialOneCommandResult result)
    {
        return string.IsNullOrWhiteSpace(result.ResultCard?.CardPath)
            ? null
            : Path.Combine(result.BundleRoot, result.ResultCard.CardPath);
    }

    private static string? ResolveHistoryEntryPath(TrialOneCommandResult result)
    {
        return string.IsNullOrWhiteSpace(result.HistoryEntryRef)
            ? null
            : Path.Combine(result.HistoryRoot, result.HistoryEntryRef);
    }

    private static string? ReadResultCardMarkdown(string? resultCardPath)
    {
        return string.IsNullOrWhiteSpace(resultCardPath) || !File.Exists(resultCardPath)
            ? null
            : File.ReadAllText(resultCardPath);
    }
}
