using System.Text;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private const string TrialResultCardRelativePath = "matrix-agent-trial-result-card.md";

    private static TrialResultCardReadback BuildTrialResultCard(
        string bundleRoot,
        TrialLocalScoreReadback? localScore,
        TrialCollectionReadback? Collection,
        TrialVerificationReadback? Verification,
        bool writeCardFile)
    {
        var evidenceRefs = new[]
        {
            "trial/carves-agent-trial-result.json",
            "matrix-artifact-manifest.json",
            "matrix-proof-summary.json",
        };
        var labels = new[]
        {
            "local-only",
            "unsubmitted",
            "non-certified",
        };
        var markdown = BuildTrialResultCardMarkdown(localScore, Collection, Verification, evidenceRefs);
        var cardPath = writeCardFile ? TrialResultCardRelativePath : null;
        if (writeCardFile)
        {
            File.WriteAllText(Path.Combine(bundleRoot, TrialResultCardRelativePath), markdown);
        }

        return new TrialResultCardReadback(
            "CARVES Agent Trial Local Result",
            cardPath,
            evidenceRefs,
            labels,
            markdown);
    }

    private static string BuildTrialResultCardMarkdown(
        TrialLocalScoreReadback? localScore,
        TrialCollectionReadback? Collection,
        TrialVerificationReadback? Verification,
        IReadOnlyList<string> evidenceRefs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CARVES Agent Trial Local Result");
        builder.AppendLine();
        builder.AppendLine("Mode: local only. This checks this computer only; no upload, no certification, no leaderboard.");
        builder.AppendLine($"Final result: {FormatTrialResultCardFinalResult(Verification)}");
        builder.AppendLine($"Final score: {FormatTrialResultCardFinalScore(localScore, Verification)}");
        builder.AppendLine($"Local dimension score: {FormatTrialResultCardLocalScore(localScore)}");
        builder.AppendLine($"Collection: {Collection?.LocalCollectionStatus ?? "not collected by this command"}");
        builder.AppendLine($"Verification: {FormatTrialResultCardVerification(Verification)}");
        if (!IsTrialResultCardVerified(Verification))
        {
            builder.AppendLine("Attention: verification is not complete, so the local dimension score is diagnostic only.");
        }

        builder.AppendLine();
        builder.AppendLine("Dimension Scores:");
        builder.AppendLine();
        builder.AppendLine("Color bands: RED 0-4, YELLOW 5-8, GREEN 9-10.");
        builder.AppendLine();
        builder.AppendLine("| Dimension | Score | Status |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var dimension in localScore?.Dimensions ?? [])
        {
            builder.AppendLine($"| {FormatTrialDimensionShortLabel(dimension.Dimension)} | {FormatColoredDimensionScore(dimension)} | {FormatTrialDimensionStatus(dimension)} |");
        }

        if (localScore is null || localScore.Dimensions.Count == 0)
        {
            builder.AppendLine("| not available | " + ScoreBadge("RED not scored") + " | score evidence was not found in trial/carves-agent-trial-result.json |");
        }

        builder.AppendLine();
        builder.AppendLine("Technical detail:");
        builder.AppendLine($"- Scoring profile: {FormatTrialResultCardProfile(localScore)}");
        foreach (var dimension in localScore?.Dimensions ?? [])
        {
            builder.AppendLine($"- {dimension.Dimension}: level={dimension.Level}; reasons={FormatReasonList(dimension.ReasonCodes)}");
        }

        builder.AppendLine();
        builder.AppendLine("Evidence:");
        foreach (var evidenceRef in evidenceRefs)
        {
            builder.AppendLine($"- [{evidenceRef}]({evidenceRef})");
        }

        builder.AppendLine();
        builder.AppendLine("Non-claims:");
        builder.AppendLine("- This is not a certification.");
        builder.AppendLine("- This is not a server-accepted leaderboard result.");
        builder.AppendLine("- This is not a general intelligence or production-safety score.");
        builder.AppendLine("- This does not prove the local machine was tamper-proof.");
        return builder.ToString();
    }

    private static string FormatTrialResultCardFinalResult(TrialVerificationReadback? verification)
    {
        if (IsTrialResultCardVerified(verification))
        {
            return ScoreBadge("GREEN VERIFIED");
        }

        if (verification is null)
        {
            return ScoreBadge("YELLOW NOT VERIFIED") + " (verification not run by this command)";
        }

        return ScoreBadge("RED UNVERIFIED")
            + $" ({verification.Status}; trial_artifacts_verified={FormatBool(verification.TrialArtifactsVerified)})";
    }

    private static string FormatTrialResultCardFinalScore(TrialLocalScoreReadback? localScore, TrialVerificationReadback? verification)
    {
        if (!IsTrialResultCardVerified(verification))
        {
            return ScoreBadge("RED not verified") + " (fix verification before treating this as a final score)";
        }

        return FormatTrialResultCardLocalScore(localScore);
    }

    private static string FormatTrialResultCardLocalScore(TrialLocalScoreReadback? localScore)
    {
        if (localScore is null)
        {
            return ScoreBadge("RED not available");
        }

        return localScore.AggregateScore.HasValue
            ? $"{FormatColoredScore(localScore.AggregateScore.Value, localScore.MaxScore)} ({localScore.ScoreStatus})"
            : $"{ScoreBadge("RED not scored")} ({localScore.ScoreStatus})";
    }

    private static string FormatTrialResultCardProfile(TrialLocalScoreReadback? localScore)
    {
        return localScore is null
            ? "not available"
            : $"{localScore.ProfileId} {localScore.ProfileVersion}";
    }

    private static string FormatTrialResultCardVerification(TrialVerificationReadback? verification)
    {
        if (verification is null)
        {
            return "not run by this command";
        }

        return $"{verification.Status}; trial_artifacts_verified={FormatBool(verification.TrialArtifactsVerified)}";
    }

    private static string FormatColoredDimensionScore(TrialLocalDimensionScoreReadback dimension)
    {
        return dimension.Score.HasValue
            ? FormatColoredScore(dimension.Score.Value, dimension.MaxScore)
            : ScoreBadge("RED not scored");
    }

    private static string FormatColoredScore(int score, int maxScore)
    {
        var band = ResolveScoreBandLabel(score, maxScore);
        return ScoreBadge($"{band} {score}/{maxScore}");
    }

    private static string ResolveScoreBandLabel(int score, int maxScore)
    {
        var normalized = maxScore <= 0
            ? 0
            : (int)Math.Round(score * 10.0 / maxScore, MidpointRounding.AwayFromZero);

        if (normalized >= 9)
        {
            return "GREEN";
        }

        if (normalized >= 5)
        {
            return "YELLOW";
        }

        return "RED";
    }

    private static string ScoreBadge(string text)
    {
        return text;
    }

    private static bool IsTrialResultCardVerified(TrialVerificationReadback? verification)
    {
        return verification is not null
            && verification.TrialArtifactsVerified
            && string.Equals(verification.Status, "verified", StringComparison.Ordinal);
    }

    private static string FormatReasonList(IReadOnlyList<string> reasons)
    {
        return reasons.Count == 0 ? "none" : string.Join(",", reasons);
    }
}
