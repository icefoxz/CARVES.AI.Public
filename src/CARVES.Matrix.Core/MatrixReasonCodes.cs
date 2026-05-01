namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static string ClassifyIssueCode(string code)
    {
        if (code is "manifest_missing" or "artifact_missing" or "summary_missing" or "required_artifact_entry_missing" or "trial_artifact_entry_missing" or "summary_source_manifest_missing"
            || code is "shield_evaluation_source_missing" or "shield_evaluation_source_manifest_missing"
            || code.StartsWith("summary_source_manifest_entry_missing:", StringComparison.Ordinal)
            || code.StartsWith("trial_artifact_manifest_entry_missing:", StringComparison.Ordinal)
            || code.StartsWith("shield_evaluation_source_manifest_entry_missing:", StringComparison.Ordinal))
        {
            return "missing_artifact";
        }

        if (code is "artifact_hash_mismatch" or "artifact_size_mismatch" or "summary_artifact_manifest_sha256_mismatch" or "trial_result_artifact_hash_mismatch"
            || code.StartsWith("summary_source_manifest_hash_mismatch:", StringComparison.Ordinal)
            || code.StartsWith("summary_source_manifest_size_mismatch:", StringComparison.Ordinal)
            || code.StartsWith("trial_artifact_manifest_hash_mismatch:", StringComparison.Ordinal)
            || code.StartsWith("trial_artifact_manifest_size_mismatch:", StringComparison.Ordinal)
            || code.StartsWith("shield_evaluation_source_manifest_hash_mismatch:", StringComparison.Ordinal)
            || code.StartsWith("shield_evaluation_source_manifest_size_mismatch:", StringComparison.Ordinal))
        {
            return "hash_mismatch";
        }

        if (code.StartsWith("privacy_", StringComparison.Ordinal))
        {
            return "privacy_violation";
        }

        if (code.StartsWith("trial_artifact_schema_invalid", StringComparison.Ordinal))
        {
            return "trial_artifact_schema_invalid";
        }

        if (code.StartsWith("trial_artifact_consistency_mismatch", StringComparison.Ordinal))
        {
            return "trial_artifact_consistency_mismatch";
        }

        if (code.StartsWith("trial_result_eligibility_", StringComparison.Ordinal))
        {
            return "trial_eligibility_violation";
        }

        if (code.StartsWith("trial_result_version_", StringComparison.Ordinal))
        {
            return "trial_version_mismatch";
        }

        if (code.StartsWith("trial_result_local_score_", StringComparison.Ordinal))
        {
            return "trial_score_mismatch";
        }

        if (code == "shield_score_unverified"
            || code.StartsWith("shield_evidence_hash_", StringComparison.Ordinal))
        {
            return "unverified_score";
        }

        if (code == "unsupported_schema")
        {
            return "unsupported_version";
        }

        return "schema_mismatch";
    }

    private static IReadOnlyList<string> DistinctReasonCodes(IEnumerable<string> codes)
    {
        return codes
            .Select(ClassifyIssueCode)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();
    }
}
