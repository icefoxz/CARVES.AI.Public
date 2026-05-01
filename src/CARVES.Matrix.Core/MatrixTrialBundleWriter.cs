using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private sealed record TrialBundleWriteResult(string BundleRoot, string ManifestPath, string ProofSummaryPath);

    private static TrialBundleWriteResult WriteLocalTrialBundle(
        string workspaceRoot,
        string bundleRoot,
        DateTimeOffset createdAt)
    {
        var fullWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var fullBundleRoot = Path.GetFullPath(bundleRoot);
        Directory.CreateDirectory(fullBundleRoot);

        WriteLocalTrialProjectArtifacts(fullBundleRoot);
        CopyLocalTrialArtifact(fullWorkspaceRoot, fullBundleRoot, ".carves/trial/task-contract.json", "trial/task-contract.json");
        CopyLocalTrialArtifact(fullWorkspaceRoot, fullBundleRoot, "artifacts/agent-report.json", "trial/agent-report.json");
        CopyLocalTrialArtifact(fullWorkspaceRoot, fullBundleRoot, "artifacts/diff-scope-summary.json", "trial/diff-scope-summary.json");
        CopyLocalTrialArtifact(fullWorkspaceRoot, fullBundleRoot, "artifacts/test-evidence.json", "trial/test-evidence.json");
        CopyLocalTrialArtifact(fullWorkspaceRoot, fullBundleRoot, "artifacts/carves-agent-trial-result.json", "trial/carves-agent-trial-result.json");

        var entries = MatrixArtifactManifestWriter.DefaultRequiredArtifacts
            .Select(requirement => ToManifestEntry(requirement, required: true))
            .Concat(MatrixArtifactManifestWriter.TrialArtifacts.Select(requirement => ToManifestEntry(requirement, required: true)));
        var manifestPath = MatrixArtifactManifestWriter.WriteManifest(fullBundleRoot, entries, createdAt);
        var summaryPath = WriteLocalTrialProofSummary(fullBundleRoot, manifestPath);
        return new TrialBundleWriteResult(fullBundleRoot, manifestPath, summaryPath);
    }

    private static void WriteLocalTrialProjectArtifacts(string bundleRoot)
    {
        WriteBundleFile(bundleRoot, "project/decisions.jsonl", """{"decision":"local_trial_readback","schema_version":"guard-decision-jsonl"}""" + Environment.NewLine);
        WriteBundleFile(bundleRoot, "project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1","status":"local_trial_readback"}""");
        WriteBundleFile(bundleRoot, "project/shield-evidence.json", """{"schema_version":"shield-evidence.v0","status":"local_trial_readback"}""");

        var evidencePath = Path.Combine(bundleRoot, "project", "shield-evidence.json");
        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(evidencePath);
        WriteBundleFile(
            bundleRoot,
            "project/shield-evaluate.json",
            $$"""
            {
              "schema_version": "shield-evaluate.v0",
              "status": "ok",
              "certification": false,
              "consumed_evidence_sha256": "{{evidenceSha256}}",
              "standard": {
                "label": "CARVES G1.H1.A1 /1d PASS"
              },
              "lite": {
                "score": 50,
                "band": "disciplined"
              }
            }
            """);
        WriteBundleFile(bundleRoot, "project/shield-badge.json", """{"schema_version":"shield-badge.v0","status":"local_trial_readback"}""");
        WriteBundleFile(bundleRoot, "project/shield-badge.svg", "<svg></svg>");
        WriteBundleFile(bundleRoot, "project/matrix-summary.json", BuildLocalTrialMatrixSummary(evidenceSha256));
    }

    private static string WriteLocalTrialProofSummary(string bundleRoot, string manifestPath)
    {
        var manifestRelativePath = MatrixArtifactManifestWriter.DefaultManifestFileName;
        var manifestSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath, bundleRoot);
        var issues = CreateManifestIssues(manifestVerification);
        var requiredArtifacts = VerifyRequiredArtifacts(manifestPath, issues);
        var trialArtifacts = VerifyTrialArtifacts(bundleRoot, manifestVerification, issues, requireTrial: false);
        var shieldEvaluation = VerifyShieldEvaluation(bundleRoot, manifestVerification, issues);
        var trustChainHardening = BuildVerifyTrustChainHardening(
            manifestVerification,
            requiredArtifacts,
            trialArtifacts,
            shieldEvaluation,
            new MatrixVerifyProofSummary("matrix-proof-summary.json", Present: true, Consistent: true),
            issues);

        using var matrixSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundleRoot, "project", "matrix-summary.json")));
        var matrixSummaryRoot = matrixSummaryDocument.RootElement;
        var summary = new
        {
            schema_version = "matrix-proof-summary.v0",
            smoke = "matrix_native_minimal_proof_lane",
            shell = "carves-matrix",
            proof_mode = MatrixProofSummaryPublicContract.NativeMinimalProofMode,
            proof_capabilities = new
            {
                proof_lane = MatrixProofSummaryPublicContract.NativeMinimalProofMode,
                execution_backend = MatrixProofSummaryPublicContract.NativeMinimalExecutionBackend,
                coverage = new
                {
                    project_mode = true,
                    packaged_install = false,
                    full_release = false,
                },
                requirements = new
                {
                    powershell = false,
                    source_checkout = false,
                    dotnet_sdk = true,
                    git = true,
                },
            },
            artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            artifact_manifest = new
            {
                path = manifestRelativePath,
                schema_version = MatrixArtifactManifestWriter.ManifestSchemaVersion,
                sha256 = manifestSha256,
                verification_posture = manifestVerification.VerificationPosture,
                issue_count = manifestVerification.Issues.Count,
            },
            trust_chain_hardening = trustChainHardening,
            native = new
            {
                passed = true,
                proof_role = GetString(matrixSummaryRoot, "proof_role"),
                scoring_owner = GetString(matrixSummaryRoot, "scoring_owner"),
                alters_shield_score = GetBool(matrixSummaryRoot, "alters_shield_score"),
                shield_status = GetString(matrixSummaryRoot, "shield", "status"),
                shield_standard_label = GetString(matrixSummaryRoot, "shield", "standard_label"),
                lite_score = GetInt(matrixSummaryRoot, "shield", "lite_score"),
                consumed_shield_evidence_sha256 = GetString(matrixSummaryRoot, "shield", "consumed_evidence_sha256"),
                guard_decision_artifact = "project/decisions.jsonl",
                handoff_packet_artifact = "project/handoff.json",
                consumed_shield_evidence_artifact = "project/shield-evidence.json",
                shield_evaluation_artifact = "project/shield-evaluate.json",
                shield_badge_json_artifact = "project/shield-badge.json",
                shield_badge_svg_artifact = "project/shield-badge.svg",
                matrix_summary_artifact = "project/matrix-summary.json",
                artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            },
            privacy = new
            {
                summary_only = true,
                source_upload_required = false,
                raw_diff_upload_required = false,
                prompt_upload_required = false,
                model_response_upload_required = false,
                secrets_required = false,
                hosted_api_required = false,
            },
            public_claims = new
            {
                certification = false,
                hosted_verification = false,
                public_leaderboard = false,
                os_sandbox_claim = false,
            },
        };

        var summaryPath = Path.Combine(bundleRoot, "matrix-proof-summary.json");
        File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, JsonOptions));
        return summaryPath;
    }

    private static MatrixArtifactManifestEntryInput ToManifestEntry(MatrixArtifactManifestRequirement requirement, bool required)
    {
        return new MatrixArtifactManifestEntryInput(
            requirement.ArtifactKind,
            requirement.Path,
            requirement.SchemaVersion,
            requirement.Producer,
            required);
    }

    private static void CopyLocalTrialArtifact(
        string workspaceRoot,
        string bundleRoot,
        string sourceRelativePath,
        string destinationRelativePath)
    {
        var sourcePath = Path.Combine(workspaceRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var destinationPath = Path.Combine(bundleRoot, destinationRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(sourcePath, destinationPath, overwrite: true);
    }

    private static void WriteBundleFile(string bundleRoot, string relativePath, string content)
    {
        var path = Path.Combine(bundleRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string BuildLocalTrialMatrixSummary(string evidenceSha256)
    {
        var summary = new
        {
            schema_version = "matrix-summary.v0",
            proof_role = "local_trial_bundle",
            proof_mode = MatrixProofSummaryPublicContract.NativeMinimalProofMode,
            scoring_owner = "shield",
            alters_shield_score = false,
            artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            shield = new
            {
                status = "ok",
                standard_label = "CARVES G1.H1.A1 /1d PASS",
                lite_score = 50,
                consumed_evidence_sha256 = evidenceSha256,
            },
        };
        return JsonSerializer.Serialize(summary, JsonOptions);
    }
}
