using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static void ValidateTrialResultLocalScore(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        JsonElement root)
    {
        ValidateTrialLocalScoreString(
            issues,
            requirement,
            "local_score.profile_id",
            GetString(root, "scoring_profile_id") ?? AgentTrialVersionContract.ScoringProfileId,
            GetString(root, "local_score", "profile_id"));
        ValidateTrialLocalScoreString(
            issues,
            requirement,
            "local_score.profile_version",
            GetString(root, "scoring_profile_version") ?? AgentTrialVersionContract.ScoringProfileVersion,
            GetString(root, "local_score", "profile_version"));

        var scoreStatus = GetString(root, "local_score", "score_status");
        var aggregateScore = GetInt(root, "local_score", "aggregate_score");
        var localCollectionStatus = GetString(root, "local_collection_status");
        var missingArtifacts = GetStringArray(root, "missing_required_artifacts");
        var suppressionReasons = GetStringArray(root, "local_score", "suppression_reasons");

        if (string.Equals(localCollectionStatus, "failed_closed", StringComparison.Ordinal)
            && !string.Equals(scoreStatus, "not_scored_failed_closed", StringComparison.Ordinal))
        {
            AddTrialLocalScoreIssue(
                issues,
                requirement,
                "trial_result_local_score_status_mismatch",
                "local_score.score_status:not_scored_failed_closed",
                scoreStatus);
        }

        if (missingArtifacts.Count > 0
            && string.Equals(scoreStatus, "scored", StringComparison.Ordinal))
        {
            AddTrialLocalScoreIssue(
                issues,
                requirement,
                "trial_result_local_score_status_mismatch",
                "local_score.score_status:not_scored_missing_evidence",
                scoreStatus);
        }

        if (!string.Equals(scoreStatus, "scored", StringComparison.Ordinal)
            && aggregateScore.HasValue)
        {
            AddTrialLocalScoreIssue(
                issues,
                requirement,
                "trial_result_local_score_aggregate_not_suppressed",
                "local_score.aggregate_score:null",
                aggregateScore.Value.ToString());
        }

        if (string.Equals(scoreStatus, "scored", StringComparison.Ordinal)
            && !aggregateScore.HasValue)
        {
            AddTrialLocalScoreIssue(
                issues,
                requirement,
                "trial_result_local_score_aggregate_missing",
                "local_score.aggregate_score",
                null);
        }

        foreach (var missingArtifact in missingArtifacts)
        {
            var expectedReason = $"missing_required_artifact:{missingArtifact}";
            if (!suppressionReasons.Contains(expectedReason, StringComparer.Ordinal))
            {
                AddTrialLocalScoreIssue(
                    issues,
                    requirement,
                    "trial_result_local_score_suppression_missing",
                    expectedReason,
                    FormatStringArray(suppressionReasons));
            }
        }

        var nonClaims = GetStringArray(root, "local_score", "non_claims");
        foreach (var requiredNonClaim in new[] { "local_only_score", "not_server_accepted", "not_certification", "not_model_intelligence_score" })
        {
            if (!nonClaims.Contains(requiredNonClaim, StringComparer.Ordinal))
            {
                AddTrialLocalScoreIssue(
                    issues,
                    requirement,
                    $"trial_result_local_score_non_claim_missing:{requiredNonClaim}",
                    requiredNonClaim,
                    FormatStringArray(nonClaims));
            }
        }
    }

    private static void ValidateTrialLocalScoreString(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        string fieldPath,
        string expected,
        string? actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
        {
            return;
        }

        AddTrialLocalScoreIssue(
            issues,
            requirement,
            $"trial_result_local_score_field_mismatch:{fieldPath}",
            $"{fieldPath}:{expected}",
            actual is null ? null : $"{fieldPath}:{actual}");
    }

    private static void AddTrialLocalScoreIssue(
        List<MatrixVerifyIssue> issues,
        MatrixArtifactManifestRequirement requirement,
        string code,
        string expected,
        string? actual)
    {
        issues.Add(new MatrixVerifyIssue(
            "trial_artifact",
            requirement.ArtifactKind,
            requirement.Path,
            code,
            expected,
            actual));
    }
}
