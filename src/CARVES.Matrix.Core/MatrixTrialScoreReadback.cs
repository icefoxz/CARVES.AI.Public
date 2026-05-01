using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static TrialLocalScoreReadback? ReadTrialLocalScore(string trialResultPath)
    {
        if (!File.Exists(trialResultPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(trialResultPath));
            if (!document.RootElement.TryGetProperty("local_score", out var score)
                || score.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new TrialLocalScoreReadback(
                GetString(score, "profile_id") ?? string.Empty,
                GetString(score, "profile_version") ?? string.Empty,
                GetString(score, "profile_name") ?? string.Empty,
                GetString(score, "score_status") ?? string.Empty,
                GetInt(score, "aggregate_score"),
                GetInt(score, "max_score") ?? 100,
                ReadTrialDimensionScores(score),
                ReadTrialScoreReasonCodes(score));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<TrialLocalDimensionScoreReadback> ReadTrialDimensionScores(JsonElement score)
    {
        if (!score.TryGetProperty("dimension_scores", out var dimensions) || dimensions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return dimensions
            .EnumerateArray()
            .Where(dimension => dimension.ValueKind == JsonValueKind.Object)
            .Select(dimension => new TrialLocalDimensionScoreReadback(
                GetString(dimension, "dimension") ?? string.Empty,
                GetInt(dimension, "score"),
                GetInt(dimension, "max_score") ?? 10,
                GetString(dimension, "level") ?? string.Empty,
                GetStringArray(dimension, "reason_codes"),
                GetString(dimension, "explanation") ?? string.Empty))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadTrialScoreReasonCodes(JsonElement score)
    {
        if (!score.TryGetProperty("reason_explanations", out var reasons) || reasons.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return reasons
            .EnumerateArray()
            .Where(reason => reason.ValueKind == JsonValueKind.Object)
            .Select(reason => GetString(reason, "reason_code"))
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .Cast<string>()
            .ToArray();
    }
}
