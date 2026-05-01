namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string ResolveVerifyPosture(
        MatrixArtifactManifestVerificationResult manifestVerification,
        IReadOnlyList<MatrixVerifyIssue> issues)
    {
        if (issues.Count == 0)
        {
            return "verified";
        }

        if (!manifestVerification.IsVerified)
        {
            return manifestVerification.VerificationPosture;
        }

        if (issues.Any(issue => issue.Scope == "required_artifact"))
        {
            return "required_artifact_failed";
        }

        if (issues.Any(IsTrialArtifactIssue))
        {
            return "trial_artifact_failed";
        }

        if (issues.Any(issue => issue.Scope == "summary"))
        {
            return "summary_consistency_failed";
        }

        if (issues.Any(issue => issue.Scope == "shield_evaluation"))
        {
            return "shield_evaluation_failed";
        }

        return "verification_failed";
    }

    private static MatrixVerifyTrustChainHardening BuildVerifyTrustChainHardening(
        MatrixArtifactManifestVerificationResult manifestVerification,
        MatrixVerifyRequiredArtifacts requiredArtifacts,
        MatrixVerifyTrialArtifacts trialArtifacts,
        MatrixVerifyShieldEvaluation shieldEvaluation,
        MatrixVerifyProofSummary proofSummary,
        IReadOnlyList<MatrixVerifyIssue> issues)
    {
        var gates = new[]
        {
            BuildTrustChainGate(
                "manifest_integrity",
                manifestVerification.IsVerified,
                manifestVerification.IsVerified
                    ? "Manifest hashes, sizes, privacy flags, and artifact file presence verified."
                    : $"Manifest verification posture is {manifestVerification.VerificationPosture}.",
                issues.Where(issue => issue.Scope is "manifest" or "artifact")),
            BuildTrustChainGate(
                "required_artifacts",
                requiredArtifacts.MissingCount == 0 && requiredArtifacts.MismatchCount == 0,
                requiredArtifacts.MissingCount == 0 && requiredArtifacts.MismatchCount == 0
                    ? "All required artifact entries are present with expected path, schema, and producer metadata."
                    : $"Required artifact check found {requiredArtifacts.MissingCount} missing and {requiredArtifacts.MismatchCount} mismatched entries.",
                issues.Where(issue => issue.Scope == "required_artifact")),
            BuildTrustChainGate(
                "trial_artifacts",
                IsTrialArtifactGateSatisfied(trialArtifacts),
                DescribeTrialArtifactGate(trialArtifacts),
                issues.Where(IsTrialArtifactIssue)),
            BuildTrustChainGate(
                "shield_score",
                shieldEvaluation.ScoreVerified,
                shieldEvaluation.ScoreVerified
                    ? "Shield evaluation status, certification posture, Standard label, and Lite score fields are verified."
                    : "Shield evaluation score readback is missing, incomplete, or not in an ok local self-check posture.",
                issues.Where(issue => issue.Scope == "shield_evaluation" || issue.ArtifactKind == "shield_evaluation")),
            BuildTrustChainGate(
                "summary_consistency",
                proofSummary.Present && proofSummary.Consistent,
                proofSummary.Present && proofSummary.Consistent
                    ? "Matrix proof summary references the current manifest hash, posture, and issue count."
                    : "Matrix proof summary is missing or inconsistent with the current manifest verification.",
                issues.Where(issue => issue.Scope == "summary")),
        };

        return new MatrixVerifyTrustChainHardening(
            gates.All(gate => gate.Satisfied),
            "matrix_verifier",
            gates);
    }

    private static bool IsTrialArtifactGateSatisfied(MatrixVerifyTrialArtifacts trialArtifacts)
    {
        if (!trialArtifacts.Required
            && (string.Equals(trialArtifacts.Mode, "not_present", StringComparison.Ordinal)
                || string.Equals(trialArtifacts.Mode, "loose_files_not_manifested", StringComparison.Ordinal)))
        {
            return true;
        }

        return trialArtifacts.Verified;
    }

    private static string DescribeTrialArtifactGate(MatrixVerifyTrialArtifacts trialArtifacts)
    {
        if (!trialArtifacts.Required && string.Equals(trialArtifacts.Mode, "not_present", StringComparison.Ordinal))
        {
            return "Agent Trial artifacts are not present; non-trial Matrix compatibility mode applies.";
        }

        if (!trialArtifacts.Required && string.Equals(trialArtifacts.Mode, "loose_files_not_manifested", StringComparison.Ordinal))
        {
            return $"Loose Agent Trial files were detected outside manifest coverage ({trialArtifacts.LooseFileCount}); ordinary verify treats them as unclaimed compatibility readback.";
        }

        if (trialArtifacts.Verified)
        {
            return "Agent Trial artifacts are manifest-covered and verified.";
        }

        return $"Agent Trial artifact check found {trialArtifacts.MissingCount} missing, {trialArtifacts.MismatchCount} mismatched, and {trialArtifacts.LooseFileCount} loose files.";
    }

    private static bool IsTrialArtifactIssue(MatrixVerifyIssue issue)
    {
        if (string.Equals(issue.Scope, "trial_artifact", StringComparison.Ordinal))
        {
            return true;
        }

        return MatrixArtifactManifestWriter.TrialArtifacts.Any(requirement =>
            string.Equals(requirement.ArtifactKind, issue.ArtifactKind, StringComparison.Ordinal));
    }

    private static MatrixVerifyTrustChainGate BuildTrustChainGate(
        string gateId,
        bool satisfied,
        string reason,
        IEnumerable<MatrixVerifyIssue> issues)
    {
        return new MatrixVerifyTrustChainGate(
            gateId,
            satisfied,
            reason,
            issues
                .Select(issue => issue.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray());
    }
}
