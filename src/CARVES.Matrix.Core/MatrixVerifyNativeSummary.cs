using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static bool VerifyNativeProofSummary(
        string artifactRoot,
        List<MatrixVerifyIssue> issues,
        string summaryRelativePath,
        JsonElement root,
        MatrixArtifactManifestVerificationResult manifestVerification)
    {
        var requirement = MatrixArtifactManifestWriter.DefaultRequiredArtifacts.Single(
            artifact => string.Equals(artifact.ArtifactKind, "matrix_summary", StringComparison.Ordinal));
        if (!TryReadVerifiedSummarySourceDocument(
                artifactRoot,
                issues,
                summaryRelativePath,
                requirement,
                manifestVerification,
                "summary_native",
                out var document,
                out var consistent))
        {
            return false;
        }

        using (document)
        {
            var source = document.RootElement;
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.proof_role", GetString(source, "proof_role"), GetString(root, "native", "proof_role"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.scoring_owner", GetString(source, "scoring_owner"), GetString(root, "native", "scoring_owner"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.alters_shield_score", FormatBool(GetBool(source, "alters_shield_score")), FormatBool(GetBool(root, "native", "alters_shield_score")));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.shield_status", GetString(source, "shield", "status"), GetString(root, "native", "shield_status"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.shield_standard_label", GetString(source, "shield", "standard_label"), GetString(root, "native", "shield_standard_label"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.lite_score", GetInt(source, "shield", "lite_score")?.ToString(), GetInt(root, "native", "lite_score")?.ToString());
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.consumed_shield_evidence_sha256", GetString(source, "shield", "consumed_evidence_sha256"), GetString(root, "native", "consumed_shield_evidence_sha256"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.guard_decision_artifact", "project/decisions.jsonl", GetString(root, "native", "guard_decision_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.handoff_packet_artifact", "project/handoff.json", GetString(root, "native", "handoff_packet_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.consumed_shield_evidence_artifact", "project/shield-evidence.json", GetString(root, "native", "consumed_shield_evidence_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.shield_evaluation_artifact", "project/shield-evaluate.json", GetString(root, "native", "shield_evaluation_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.shield_badge_json_artifact", "project/shield-badge.json", GetString(root, "native", "shield_badge_json_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.shield_badge_svg_artifact", "project/shield-badge.svg", GetString(root, "native", "shield_badge_svg_artifact"));
            consistent &= VerifySummaryField(issues, summaryRelativePath, "native.matrix_summary_artifact", "project/matrix-summary.json", GetString(root, "native", "matrix_summary_artifact"));
            return consistent;
        }
    }
}
