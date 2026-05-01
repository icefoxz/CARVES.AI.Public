using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static TrialHistoryEntry BuildTrialHistoryEntry(
        string bundleRoot,
        string? runId,
        MatrixVerifyResult verification)
    {
        var trialResultPath = Path.Combine(bundleRoot, "trial", "carves-agent-trial-result.json");
        if (!File.Exists(trialResultPath))
        {
            throw new InvalidDataException("Local trial result is missing: trial/carves-agent-trial-result.json");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(trialResultPath));
        var root = document.RootElement;
        var localScore = ReadTrialLocalScore(trialResultPath);
        var recordedAt = GetString(root, "created_at") ?? DateTimeOffset.UtcNow.ToString("O");
        var identity = new TrialHistoryIdentity(
            GetString(root, "suite_id") ?? "unknown",
            GetString(root, "pack_id") ?? "unknown",
            GetString(root, "pack_version") ?? "unknown",
            GetString(root, "task_id") ?? "unknown",
            GetString(root, "task_version") ?? "unknown",
            GetString(root, "prompt_id") ?? "unknown",
            GetString(root, "prompt_version") ?? "unknown",
            GetString(root, "scoring_profile_id") ?? localScore?.ProfileId ?? "unknown",
            GetString(root, "scoring_profile_version") ?? localScore?.ProfileVersion ?? "unknown");
        var entryRunId = SanitizeRunId(string.IsNullOrWhiteSpace(runId)
            ? BuildDefaultRunId(recordedAt, trialResultPath)
            : runId);

        return new TrialHistoryEntry(
            TrialHistoryEntrySchemaVersion,
            entryRunId,
            recordedAt,
            "local_only",
            verification.Status,
            verification.IsVerified,
            identity,
            BuildHistoryScore(localScore),
            [
                "trial/carves-agent-trial-result.json",
                "matrix-artifact-manifest.json",
                "matrix-proof-summary.json"
            ],
            new TrialHistoryArtifactAnchors(
                HashFileOrMissing(Path.Combine(bundleRoot, "matrix-artifact-manifest.json")),
                HashFileOrMissing(trialResultPath)),
            [
                "local_history_only",
                "not_server_receipt",
                "not_leaderboard_eligible",
                "not_certification"
            ]);
    }

    private static TrialHistoryCompareResult BuildTrialHistoryComparison(TrialHistoryEntry baseline, TrialHistoryEntry target)
    {
        var reasonCodes = CompareIdentity(baseline.Identity, target.Identity);
        var directComparable = reasonCodes.Count == 0;
        var comparisonMode = directComparable ? "direct" : "trend_only";
        return new TrialHistoryCompareResult(
            TrialHistoryCompareSchemaVersion,
            "compare",
            "compared",
            Offline: true,
            ServerSubmission: false,
            baseline.RunId,
            target.RunId,
            comparisonMode,
            directComparable,
            reasonCodes,
            directComparable
                ? "Runs use the same suite, pack, task, prompt, and scoring profile; score deltas are directly comparable."
                : "Runs differ by version, prompt, task, pack, or scoring profile; score deltas are shown only as trend-only movement.",
            MoveScore(baseline.Score.AggregateScore, target.Score.AggregateScore),
            BuildDimensionMovements(baseline.Score.Dimensions, target.Score.Dimensions),
            [
                "local_history_only",
                "not_server_receipt",
                "not_leaderboard_eligible",
                "not_certification"
            ]);
    }

    private static TrialHistoryScore BuildHistoryScore(TrialLocalScoreReadback? score)
    {
        return new TrialHistoryScore(
            score?.ScoreStatus ?? "unavailable",
            score?.AggregateScore,
            score?.MaxScore ?? 100,
            score?.Dimensions
                .Select(dimension => new TrialHistoryDimensionScore(
                    dimension.Dimension,
                    dimension.Score,
                    dimension.MaxScore,
                    dimension.Level,
                    dimension.ReasonCodes))
                .ToArray() ?? [],
            score?.ReasonCodes ?? []);
    }

    private static IReadOnlyList<string> CompareIdentity(TrialHistoryIdentity baseline, TrialHistoryIdentity target)
    {
        var reasons = new List<string>();
        AddMismatch(reasons, "suite_id", baseline.SuiteId, target.SuiteId);
        AddMismatch(reasons, "pack_id", baseline.PackId, target.PackId);
        AddMismatch(reasons, "pack_version", baseline.PackVersion, target.PackVersion);
        AddMismatch(reasons, "task_id", baseline.TaskId, target.TaskId);
        AddMismatch(reasons, "task_version", baseline.TaskVersion, target.TaskVersion);
        AddMismatch(reasons, "prompt_id", baseline.PromptId, target.PromptId);
        AddMismatch(reasons, "prompt_version", baseline.PromptVersion, target.PromptVersion);
        AddMismatch(reasons, "scoring_profile_id", baseline.ScoringProfileId, target.ScoringProfileId);
        AddMismatch(reasons, "scoring_profile_version", baseline.ScoringProfileVersion, target.ScoringProfileVersion);
        return reasons;
    }

    private static void AddMismatch(List<string> reasons, string field, string baseline, string target)
    {
        if (!string.Equals(baseline, target, StringComparison.Ordinal))
        {
            reasons.Add(field + "_mismatch");
        }
    }

    private static IReadOnlyList<TrialHistoryDimensionMovement> BuildDimensionMovements(
        IReadOnlyList<TrialHistoryDimensionScore> baseline,
        IReadOnlyList<TrialHistoryDimensionScore> target)
    {
        var names = baseline.Select(dimension => dimension.Dimension)
            .Concat(target.Select(dimension => dimension.Dimension))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
        return names
            .Select(name =>
            {
                var left = baseline.FirstOrDefault(dimension => string.Equals(dimension.Dimension, name, StringComparison.Ordinal));
                var right = target.FirstOrDefault(dimension => string.Equals(dimension.Dimension, name, StringComparison.Ordinal));
                return new TrialHistoryDimensionMovement(
                    name,
                    left?.Score,
                    right?.Score,
                    MoveScore(left?.Score, right?.Score).Delta,
                    left?.Level ?? "missing",
                    right?.Level ?? "missing");
            })
            .ToArray();
    }

    private static TrialHistoryScoreMovement MoveScore(int? baseline, int? target)
    {
        return new TrialHistoryScoreMovement(baseline, target, baseline.HasValue && target.HasValue ? target.Value - baseline.Value : null);
    }

    private static string BuildDefaultRunId(string createdAt, string trialResultPath)
    {
        var stamp = DateTimeOffset.TryParse(createdAt, out var parsed)
            ? parsed.UtcDateTime.ToString("yyyyMMddTHHmmssZ")
            : DateTimeOffset.UtcNow.UtcDateTime.ToString("yyyyMMddTHHmmssZ");
        var hash = HashFileOrMissing(trialResultPath);
        return stamp + "-" + hash.Replace("sha256:", string.Empty, StringComparison.Ordinal)[..12];
    }

    private static string HashFileOrMissing(string path)
    {
        if (!File.Exists(path))
        {
            return AgentTrialLocalJson.MissingArtifactHash;
        }

        using var stream = File.OpenRead(path);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
