namespace Carves.Matrix.Core;

internal sealed record MatrixProofSummaryObjectContract(
    string ContractName,
    string JsonPath,
    IReadOnlyList<string> FieldNames);

internal sealed record MatrixProofSummaryPublicContractModel(
    IReadOnlyList<string> BaseTopLevelFields,
    IReadOnlyList<string> PrivacyFalseFieldNames,
    IReadOnlyList<string> PublicClaimFalseFieldNames,
    MatrixProofSummaryObjectContract ArtifactManifest,
    MatrixProofSummaryObjectContract ProofCapabilities,
    MatrixProofSummaryObjectContract ProofCapabilityCoverage,
    MatrixProofSummaryObjectContract ProofCapabilityRequirements,
    MatrixProofSummaryObjectContract Privacy,
    MatrixProofSummaryObjectContract PublicClaims,
    MatrixProofSummaryObjectContract TrustChainHardening,
    MatrixProofSummaryObjectContract TrustChainGate,
    MatrixProofSummaryObjectContract Native,
    MatrixProofSummaryObjectContract Project,
    MatrixProofSummaryObjectContract ProjectTrustChainHardening,
    MatrixProofSummaryObjectContract Packaged,
    MatrixProofSummaryObjectContract PackagedTrustChainHardening)
{
    public IReadOnlyList<string> NativeMinimalTopLevelFields { get; } =
        Combine(BaseTopLevelFields, Fields("native"));

    public IReadOnlyList<string> FullReleaseTopLevelFields { get; } =
        Combine(BaseTopLevelFields, Fields("project", "packaged"));

    public IReadOnlyList<string> AllTopLevelFields { get; } =
        Combine(BaseTopLevelFields, Fields("native", "project", "packaged"));

    public IReadOnlyList<string> GetTopLevelFields(string? proofMode)
    {
        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.NativeMinimalProofMode, StringComparison.Ordinal))
        {
            return NativeMinimalTopLevelFields;
        }

        if (string.Equals(proofMode, MatrixProofSummaryPublicContract.FullReleaseProofMode, StringComparison.Ordinal))
        {
            return FullReleaseTopLevelFields;
        }

        return AllTopLevelFields;
    }

    private static IReadOnlyList<string> Fields(params string[] fields)
    {
        return Array.AsReadOnly(fields);
    }

    private static IReadOnlyList<string> Combine(params IReadOnlyList<string>[] groups)
    {
        return Array.AsReadOnly(groups.SelectMany(group => group).ToArray());
    }
}

internal static class MatrixProofSummaryPublicContract
{
    public const string NativeMinimalProofMode = "native_minimal";
    public const string FullReleaseProofMode = "full_release";
    public const string NativeFullReleaseProofLane = "native_full_release";
    public const string NativeMinimalExecutionBackend = "dotnet_runner_chain";
    public const string FullReleaseExecutionBackend = "powershell_release_units";
    public const string NativeFullReleaseExecutionBackend = "dotnet_full_release_runner_chain";

    public static MatrixProofSummaryPublicContractModel Model { get; } = BuildModel();

    private static MatrixProofSummaryPublicContractModel BuildModel()
    {
        var privacyFalseFields = Fields(
            "source_upload_required",
            "raw_diff_upload_required",
            "prompt_upload_required",
            "model_response_upload_required",
            "secrets_required",
            "hosted_api_required");

        var publicClaimFalseFields = Fields(
            "certification",
            "hosted_verification",
            "public_leaderboard",
            "os_sandbox_claim");

        var fullReleaseTrustChainEvidence = Object(
            "full_release_evidence_fields",
            "trust_chain_hardening",
            "audit_evidence_integrity",
            "guard_deletion_replacement_honesty",
            "shield_evidence_contract_alignment",
            "guard_audit_store_multiprocess_durability",
            "handoff_completed_state_semantics",
            "matrix_shield_proof_bridge_claim_boundary",
            "large_log_streaming_output_boundaries",
            "handoff_reference_freshness_portability",
            "usability_coverage_cleanup",
            "release_checkpoint",
            "public_rating_claim",
            "public_rating_claims_allowed");

        return new MatrixProofSummaryPublicContractModel(
            BaseTopLevelFields: Fields(
                "schema_version",
                "smoke",
                "shell",
                "proof_mode",
                "proof_capabilities",
                "artifact_root",
                "artifact_manifest",
                "trust_chain_hardening",
                "privacy",
                "public_claims"),
            PrivacyFalseFieldNames: privacyFalseFields,
            PublicClaimFalseFieldNames: publicClaimFalseFields,
            ArtifactManifest: Object(
                "artifact_manifest_reference",
                "artifact_manifest",
                "path",
                "schema_version",
                "sha256",
                "verification_posture",
                "issue_count"),
            ProofCapabilities: Object(
                "proof_capabilities",
                "proof_capabilities",
                "proof_lane",
                "execution_backend",
                "coverage",
                "requirements"),
            ProofCapabilityCoverage: Object(
                "proof_capability_coverage",
                "proof_capabilities.coverage",
                "project_mode",
                "packaged_install",
                "full_release"),
            ProofCapabilityRequirements: Object(
                "proof_capability_requirements",
                "proof_capabilities.requirements",
                "powershell",
                "source_checkout",
                "dotnet_sdk",
                "git"),
            Privacy: new MatrixProofSummaryObjectContract(
                "summary_privacy",
                "privacy",
                Combine(Fields("summary_only"), privacyFalseFields)),
            PublicClaims: new MatrixProofSummaryObjectContract(
                "public_claims",
                "public_claims",
                publicClaimFalseFields),
            TrustChainHardening: Object(
                "verifier_trust_chain",
                "trust_chain_hardening",
                "gates_satisfied",
                "computed_by",
                "gates"),
            TrustChainGate: Object(
                "verifier_gate",
                "trust_chain_hardening.gates[]",
                "gate_id",
                "satisfied",
                "reason",
                "issue_codes",
                "reason_codes"),
            Native: Object(
                "native_summary",
                "native",
                "passed",
                "proof_role",
                "scoring_owner",
                "alters_shield_score",
                "shield_status",
                "shield_standard_label",
                "lite_score",
                "consumed_shield_evidence_sha256",
                "guard_decision_artifact",
                "handoff_packet_artifact",
                "consumed_shield_evidence_artifact",
                "shield_evaluation_artifact",
                "shield_badge_json_artifact",
                "shield_badge_svg_artifact",
                "matrix_summary_artifact",
                "artifact_root"),
            Project: Object(
                "full_release_project_summary",
                "project",
                "passed",
                "guard_run_id",
                "shield_status",
                "shield_standard_label",
                "lite_score",
                "consumed_shield_evidence_sha256",
                "proof_role",
                "scoring_owner",
                "alters_shield_score",
                "consumed_shield_evidence_artifact",
                "shield_evaluation_artifact",
                "shield_badge_json_artifact",
                "shield_badge_svg_artifact",
                "trust_chain_hardening",
                "artifact_root"),
            ProjectTrustChainHardening: fullReleaseTrustChainEvidence with { JsonPath = "project.trust_chain_hardening" },
            Packaged: Object(
                "full_release_packaged_summary",
                "packaged",
                "passed",
                "guard_version",
                "handoff_version",
                "audit_version",
                "shield_version",
                "matrix_version",
                "guard_run_id",
                "shield_status",
                "shield_standard_label",
                "lite_score",
                "consumed_shield_evidence_sha256",
                "proof_role",
                "scoring_owner",
                "alters_shield_score",
                "consumed_shield_evidence_artifact",
                "shield_evaluation_artifact",
                "shield_badge_json_artifact",
                "shield_badge_svg_artifact",
                "trust_chain_hardening",
                "artifact_root"),
            PackagedTrustChainHardening: fullReleaseTrustChainEvidence with { JsonPath = "packaged.trust_chain_hardening" });
    }

    private static MatrixProofSummaryObjectContract Object(
        string contractName,
        string jsonPath,
        params string[] fieldNames)
    {
        return new MatrixProofSummaryObjectContract(contractName, jsonPath, Array.AsReadOnly(fieldNames));
    }

    private static IReadOnlyList<string> Fields(params string[] fields)
    {
        return Array.AsReadOnly(fields);
    }

    private static IReadOnlyList<string> Combine(params IReadOnlyList<string>[] groups)
    {
        return Array.AsReadOnly(groups.SelectMany(group => group).ToArray());
    }
}
