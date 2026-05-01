using Carves.Audit.Core;
using Carves.Matrix.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Carves.Runtime.IntegrationTests;

public sealed class MatrixExtractionShellTests
{
    [Fact]
    public void MatrixProjects_DefineStandaloneShellPackageBoundary()
    {
        var repoRoot = LocateSourceRepoRoot();
        var coreProject = Path.Combine(repoRoot, "src", "CARVES.Matrix.Core", "Carves.Matrix.Core.csproj");
        var cliProject = Path.Combine(repoRoot, "src", "CARVES.Matrix.Cli", "Carves.Matrix.Cli.csproj");
        var readme = Path.Combine(repoRoot, "docs", "matrix", "README.md");

        Assert.True(File.Exists(coreProject));
        Assert.True(File.Exists(cliProject));
        Assert.True(File.Exists(readme));

        var core = File.ReadAllText(coreProject);
        var cli = File.ReadAllText(cliProject);
        Assert.Contains("<PackageId>CARVES.Matrix.Core</PackageId>", core, StringComparison.Ordinal);
        Assert.Contains("<PackageId>CARVES.Matrix.Cli</PackageId>", cli, StringComparison.Ordinal);
        Assert.Contains("<ToolCommandName>carves-matrix</ToolCommandName>", cli, StringComparison.Ordinal);
        Assert.Contains("docs\\matrix\\README.md", cli, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixRunner_DoesNotOwnPeerProductBusinessLogic()
    {
        var repoRoot = LocateSourceRepoRoot();
        var runner = ReadMatrixCoreSource(repoRoot, "MatrixCliRunner.cs");
        var coreSources = ReadMatrixCoreSources(repoRoot);

        Assert.Contains("carves-matrix", runner, StringComparison.Ordinal);
        Assert.Contains("matrix-e2e-smoke.ps1", coreSources, StringComparison.Ordinal);
        Assert.Contains("matrix-packaged-install-smoke.ps1", coreSources, StringComparison.Ordinal);
        Assert.Contains("\"verify\" => RunVerify", runner, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.v0", coreSources, StringComparison.Ordinal);
        Assert.Contains("shield_evaluation_artifact", coreSources, StringComparison.Ordinal);
        Assert.Contains("artifact_manifest", coreSources, StringComparison.Ordinal);
        Assert.Contains("WriteDefaultProofManifest", coreSources, StringComparison.Ordinal);
        Assert.Contains("ComputeFileSha256(manifestPath)", coreSources, StringComparison.Ordinal);
        Assert.Contains("VerifyManifest(manifestPath)", coreSources, StringComparison.Ordinal);
        Assert.Contains("verifyResult.IsVerified && verifyResult.TrustChainHardening.GatesSatisfied ? 0 : 1", coreSources, StringComparison.Ordinal);
        Assert.Contains("BuildVerifyResult(artifactRoot, requireTrial: false)", coreSources, StringComparison.Ordinal);
        Assert.Contains("verifyResult.TrustChainHardening", coreSources, StringComparison.Ordinal);
        Assert.Contains("trust_chain_hardening = trustChainHardening", coreSources, StringComparison.Ordinal);
        Assert.Contains("BuildVerifyTrustChainHardening", coreSources, StringComparison.Ordinal);
        Assert.Contains("consumed_shield_evidence_sha256", coreSources, StringComparison.Ordinal);
        Assert.Contains("matrix-artifact-manifest.json", coreSources, StringComparison.Ordinal);
        Assert.Contains("guard_decision", coreSources, StringComparison.Ordinal);
        Assert.Contains("CreateSummaryOnly", coreSources, StringComparison.Ordinal);
        Assert.Contains("private_payload_included", coreSources, StringComparison.Ordinal);
        Assert.Contains("privacy_gate_failed", coreSources, StringComparison.Ordinal);
        Assert.Contains("trust_chain_hardening", coreSources, StringComparison.Ordinal);
        Assert.Contains("guard_audit_store_multiprocess_durability", coreSources, StringComparison.Ordinal);
        Assert.Contains("handoff_completed_state_semantics", coreSources, StringComparison.Ordinal);
        Assert.Contains("large_log_streaming_output_boundaries", coreSources, StringComparison.Ordinal);
        Assert.Contains("release_checkpoint", coreSources, StringComparison.Ordinal);
        Assert.Contains("alters_shield_score", coreSources, StringComparison.Ordinal);
        Assert.Contains("BuildTrustChainHardeningEvidence", coreSources, StringComparison.Ordinal);
        Assert.DoesNotContain("GuardCheckService", coreSources, StringComparison.Ordinal);
        Assert.DoesNotContain("GuardDecisionReadService", coreSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldEvaluationService", coreSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldBadgeService", coreSources, StringComparison.Ordinal);
        Assert.DoesNotContain("Carves.Runtime.Application.Guard", coreSources, StringComparison.Ordinal);
        Assert.DoesNotContain("Carves.Runtime.Application.Shield", coreSources, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixCliRunner_IsDecomposedIntoCommandAndSupportComponents()
    {
        var repoRoot = LocateSourceRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "CARVES.Matrix.Core");
        var expectedCliRunnerFiles = new[]
        {
            "MatrixCliRunner.cs",
            "MatrixProofCommand.cs",
            "MatrixNativeProofCommand.cs",
            "MatrixNativeProofArtifacts.cs",
            "MatrixNativeProofFinalization.cs",
            "MatrixNativeProofFailure.cs",
            "MatrixNativeProofIO.cs",
            "MatrixNativeProofProductChain.cs",
            "MatrixNativeProofRepositorySetup.cs",
            "MatrixNativeProofStepRunner.cs",
            "MatrixNativeProofSummary.cs",
            "MatrixNativeProofWorkspace.cs",
            "MatrixVerifyCommand.cs",
            "MatrixVerifyArtifacts.cs",
            "MatrixVerifyOutput.cs",
            "MatrixVerifyShieldEvaluation.cs",
            "MatrixVerifySummary.cs",
            "MatrixVerifyNativeSummary.cs",
            "MatrixVerifyFullReleaseSummary.cs",
            "MatrixVerifiedArtifactReader.cs",
            "MatrixVerifiedArtifactManifestEntryReader.cs",
            "MatrixVerifySummaryTrustChain.cs",
            "MatrixVerifyIssueFactory.cs",
            "MatrixVerifyTrustChain.cs",
            "MatrixProofCapabilities.cs",
            "MatrixProofSummaryBuilder.cs",
            "MatrixProofSummaryPublicContractVerifier.cs",
            "MatrixProofSummaryTrustChainProjection.cs",
            "MatrixVerifyProofCapabilities.cs",
            "MatrixScriptCommand.cs",
            "MatrixProcessInvoker.cs",
            "MatrixArtifactLayout.cs",
            "MatrixJsonOutput.cs",
            "MatrixCliUsage.cs",
            "MatrixProofOptions.cs",
            "MatrixVerifyOptions.cs",
            "MatrixCliModels.cs",
        };
        var expectedSupportFiles = new[]
        {
            "MatrixArtifactManifestWriter.cs",
            "MatrixArtifactManifestRequirements.cs",
            "MatrixArtifactManifestMaterialization.cs",
            "MatrixArtifactPathPolicy.cs",
            "MatrixArtifactManifestVerifier.cs",
            "MatrixArtifactManifestPrivacy.cs",
            "MatrixArtifactManifestIO.cs",
            "MatrixArtifactManifestModels.cs",
            "MatrixProofSummaryPublicContract.cs",
        };

        foreach (var file in expectedCliRunnerFiles)
        {
            Assert.True(File.Exists(Path.Combine(coreRoot, file)), $"Missing Matrix CLI component: {file}");
            Assert.Contains("public static partial class MatrixCliRunner", File.ReadAllText(Path.Combine(coreRoot, file)), StringComparison.Ordinal);
        }
        foreach (var file in expectedSupportFiles)
        {
            Assert.True(File.Exists(Path.Combine(coreRoot, file)), $"Missing Matrix support component: {file}");
        }

        var runner = ReadMatrixCoreSource(repoRoot, "MatrixCliRunner.cs");
        Assert.DoesNotContain("BuildVerifyResult", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("RunNativeCliStep", runner, StringComparison.Ordinal);
        Assert.Contains("RunVerify", ReadMatrixCoreSource(repoRoot, "MatrixVerifyCommand.cs"), StringComparison.Ordinal);
        Assert.Contains("TryPrepareNativeProofRepository", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofRepositorySetup.cs"), StringComparison.Ordinal);
        Assert.Contains("TryRunNativeProofProductChain", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofProductChain.cs"), StringComparison.Ordinal);
        Assert.Contains("CompleteNativeMinimalProof", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofFinalization.cs"), StringComparison.Ordinal);
        Assert.Contains("WriteNativeProofArtifacts", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofArtifacts.cs"), StringComparison.Ordinal);
        Assert.Contains("RunNativeCliStep", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofStepRunner.cs"), StringComparison.Ordinal);
        Assert.Contains("WriteNativeProofFailure", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofFailure.cs"), StringComparison.Ordinal);
        Assert.Contains("ResolveNativeRelativePath", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofIO.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyShieldEvaluation", ReadMatrixCoreSource(repoRoot, "MatrixVerifyShieldEvaluation.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyFullReleaseProofSummary", ReadMatrixCoreSource(repoRoot, "MatrixVerifyFullReleaseSummary.cs"), StringComparison.Ordinal);
        Assert.Contains("TryReadVerifiedSummarySourceDocument", ReadMatrixCoreSource(repoRoot, "MatrixVerifiedArtifactReader.cs"), StringComparison.Ordinal);
        Assert.Contains("TryReadVerifiedShieldEvaluationDocument", ReadMatrixCoreSource(repoRoot, "MatrixVerifiedArtifactReader.cs"), StringComparison.Ordinal);
        Assert.Contains("TryGetVerifiedArtifactManifestEntry", ReadMatrixCoreSource(repoRoot, "MatrixVerifiedArtifactManifestEntryReader.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifySummaryTrustChainHardening", ReadMatrixCoreSource(repoRoot, "MatrixVerifySummaryTrustChain.cs"), StringComparison.Ordinal);
        Assert.Contains("MatrixProofSummaryPublicContractModel", ReadMatrixCoreSource(repoRoot, "MatrixProofSummaryPublicContract.cs"), StringComparison.Ordinal);
        Assert.Contains("ProofCapabilities", ReadMatrixCoreSource(repoRoot, "MatrixProofSummaryPublicContract.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyProofSummaryPublicContract", ReadMatrixCoreSource(repoRoot, "MatrixProofSummaryPublicContractVerifier.cs"), StringComparison.Ordinal);
        Assert.Contains("WriteManifest", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestWriter.cs"), StringComparison.Ordinal);
        Assert.Contains("DefaultRequiredArtifactRequirements", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestRequirements.cs"), StringComparison.Ordinal);
        Assert.Contains("PathEscapesRoot", ReadMatrixCoreSource(repoRoot, "MatrixArtifactPathPolicy.cs"), StringComparison.Ordinal);
        Assert.Contains("ValidatePrivacyFlags", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestPrivacy.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyManifestArtifact", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestVerifier.cs"), StringComparison.Ordinal);
        Assert.Contains("InvokeProcess", ReadMatrixCoreSource(repoRoot, "MatrixProcessInvoker.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStartInfo", ReadMatrixCoreSource(repoRoot, "MatrixScriptCommand.cs"), StringComparison.Ordinal);
        Assert.Contains("ResolveArtifactRoot", ReadMatrixCoreSource(repoRoot, "MatrixArtifactLayout.cs"), StringComparison.Ordinal);
        Assert.Contains("ParseJson", ReadMatrixCoreSource(repoRoot, "MatrixJsonOutput.cs"), StringComparison.Ordinal);
        Assert.Contains("MatrixOptions", ReadMatrixCoreSource(repoRoot, "MatrixProofOptions.cs"), StringComparison.Ordinal);
        Assert.Contains("MatrixVerifyOptions", ReadMatrixCoreSource(repoRoot, "MatrixVerifyOptions.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixProofScript_IsThinCliWrapper()
    {
        var repoRoot = LocateSourceRepoRoot();
        var proofScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-proof-lane.ps1"));

        Assert.Contains("CARVES.Matrix.Cli", proofScript, StringComparison.Ordinal);
        Assert.Contains("matrix_proof_lane is owned by CARVES.Matrix.Cli", proofScript, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-e2e-smoke.ps1", proofScript, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-packaged-install-smoke.ps1", proofScript, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixDocs_StateCompositionLayerNotFifthSafetyEngine()
    {
        var repoRoot = LocateSourceRepoRoot();
        var readme = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md"));
        var boundary = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "public-boundary.md"));

        Assert.Contains("Linux-native public artifact recheck path", readme, StringComparison.Ordinal);
        Assert.Contains("does not invoke `pwsh`", readme, StringComparison.Ordinal);

        foreach (var doc in new[] { readme, boundary })
        {
            Assert.Contains("composition", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("fifth safety", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Guard", doc, StringComparison.Ordinal);
            Assert.Contains("Handoff", doc, StringComparison.Ordinal);
            Assert.Contains("Audit", doc, StringComparison.Ordinal);
            Assert.Contains("Shield", doc, StringComparison.Ordinal);
            Assert.Contains("does not alter Shield scoring", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Shield evaluation", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MatrixRunner_HelpIsAvailable()
    {
        var exitCode = MatrixCliRunner.Run(["help"]);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void MatrixVerifyCommand_IsNativePathAndDoesNotInvokePowerShellScripts()
    {
        var repoRoot = LocateSourceRepoRoot();
        var verifyPath = ReadMatrixCoreSource(repoRoot, "MatrixVerifyCommand.cs");
        var usage = ReadMatrixCoreSource(repoRoot, "MatrixCliUsage.cs");

        Assert.Contains("BuildVerifyResult(options.ArtifactRoot!, options.RequireTrial)", verifyPath, StringComparison.Ordinal);
        Assert.Contains("MatrixArtifactManifestWriter.VerifyManifest", verifyPath, StringComparison.Ordinal);
        Assert.Contains("VerifyRequiredArtifacts", verifyPath, StringComparison.Ordinal);
        Assert.Contains("VerifyShieldEvaluation", verifyPath, StringComparison.Ordinal);
        Assert.Contains("VerifyProofSummary", verifyPath, StringComparison.Ordinal);
        Assert.Contains("BuildVerifyTrustChainHardening", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeScript", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeProcess", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveMatrixScript", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("pwsh", verifyPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrix-e2e-smoke.ps1", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-packaged-install-smoke.ps1", verifyPath, StringComparison.Ordinal);

        Assert.Contains("native .NET artifact recheck", usage, StringComparison.Ordinal);
        Assert.Contains("does not invoke PowerShell proof scripts", usage, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixVerifyCommand_ValidatesPassingBundleJson()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-pass-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(artifactRoot);

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(0, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("matrix-verify.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("verified", root.GetProperty("status").GetString());
            Assert.Equal("verified", root.GetProperty("verification_posture").GetString());
            Assert.Equal(0, root.GetProperty("exit_code").GetInt32());
            Assert.Equal(0, root.GetProperty("issue_count").GetInt32());
            Assert.Empty(root.GetProperty("reason_codes").EnumerateArray());
            Assert.True(root.GetProperty("summary").GetProperty("consistent").GetBoolean());
            Assert.True(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
            Assert.True(root.GetProperty("trust_chain_hardening").GetProperty("gates_satisfied").GetBoolean());
            Assert.Equal("matrix_verifier", root.GetProperty("trust_chain_hardening").GetProperty("computed_by").GetString());
            Assert.Contains(
                root.GetProperty("trust_chain_hardening").GetProperty("gates").EnumerateArray(),
                gate => gate.GetProperty("gate_id").GetString() == "manifest_integrity"
                        && gate.GetProperty("satisfied").GetBoolean());
            Assert.Equal(7, root.GetProperty("required_artifacts").GetProperty("expected_count").GetInt32());
            Assert.Equal(7, root.GetProperty("required_artifacts").GetProperty("present_count").GetInt32());
            Assert.Equal(
                MatrixArtifactManifestWriter.ComputeFileSha256(Path.Combine(artifactRoot, "matrix-artifact-manifest.json")),
                root.GetProperty("manifest").GetProperty("sha256").GetString());
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixVerifyCommand_FailsWhenRequiredArtifactFileIsMissing()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-missing-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(artifactRoot);
            File.Delete(Path.Combine(artifactRoot, "project", "shield-evaluate.json"));

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            Assert.Equal("failed", root.GetProperty("status").GetString());
            Assert.Equal("changed_or_substituted", root.GetProperty("verification_posture").GetString());
            Assert.Equal(1, root.GetProperty("exit_code").GetInt32());
            AssertContainsReasonCode(root, "missing_artifact");
            Assert.False(root.GetProperty("trust_chain_hardening").GetProperty("gates_satisfied").GetBoolean());
            Assert.Contains(
                root.GetProperty("trust_chain_hardening").GetProperty("gates").EnumerateArray(),
                gate => gate.GetProperty("gate_id").GetString() == "manifest_integrity"
                        && !gate.GetProperty("satisfied").GetBoolean()
                        && gate.GetProperty("issue_codes").EnumerateArray().Any(code => code.GetString() == "artifact_missing")
                        && gate.GetProperty("reason_codes").EnumerateArray().Any(code => code.GetString() == "missing_artifact"));
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "shield_evaluation"
                         && issue.GetProperty("code").GetString() == "artifact_missing"
                         && issue.GetProperty("reason_code").GetString() == "missing_artifact");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixVerifyCommand_ReportsHashMismatchReasonCode()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-hash-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(artifactRoot);
            File.AppendAllText(Path.Combine(artifactRoot, "project", "shield-evidence.json"), "\n{\"mutated\":true}");

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            AssertContainsReasonCode(root, "hash_mismatch");
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "audit_evidence"
                         && issue.GetProperty("code").GetString() == "artifact_hash_mismatch"
                         && issue.GetProperty("reason_code").GetString() == "hash_mismatch");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixVerifyCommand_ReportsSchemaMismatchReasonCode()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-schema-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(artifactRoot);
            var manifestPath = Path.Combine(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
            SetArtifactManifestField(manifestPath, "shield_evaluation", "schema_version", "shield-evaluate.v999");
            WriteMatrixProofSummary(artifactRoot);

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            AssertContainsReasonCode(root, "schema_mismatch");
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "shield_evaluation"
                         && issue.GetProperty("code").GetString() == "required_artifact_schema_mismatch"
                         && issue.GetProperty("reason_code").GetString() == "schema_mismatch");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixVerifyCommand_ReportsPrivacyViolationReasonCode()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-privacy-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(artifactRoot);
            var manifestPath = Path.Combine(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
            SetArtifactPrivacyFlag(manifestPath, "audit_evidence", "raw_diff_included", value: true);
            WriteMatrixProofSummary(artifactRoot);

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            AssertContainsReasonCode(root, "privacy_violation");
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "audit_evidence"
                         && issue.GetProperty("code").GetString() == "privacy_forbidden_flag_true:raw_diff_included"
                         && issue.GetProperty("reason_code").GetString() == "privacy_violation");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixVerifyCommand_ReportsUnverifiedScoreReasonCode()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-score-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(
                artifactRoot,
                """
                {
                  "schema_version": "shield-evaluate.v0",
                  "status": "invalid_input",
                  "certification": false
                }
                """);

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            AssertContainsReasonCode(root, "unverified_score");
            Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
            Assert.Contains(
                root.GetProperty("trust_chain_hardening").GetProperty("gates").EnumerateArray(),
                gate => gate.GetProperty("gate_id").GetString() == "shield_score"
                        && !gate.GetProperty("satisfied").GetBoolean()
                        && gate.GetProperty("reason_codes").EnumerateArray().Any(code => code.GetString() == "unverified_score"));
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "shield_evaluation"
                         && issue.GetProperty("code").GetString() == "shield_score_unverified"
                         && issue.GetProperty("reason_code").GetString() == "unverified_score");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixVerifyCommand_ReportsUnsupportedVersionReasonCode()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-verify-version-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            CreateVerifiableMatrixBundle(artifactRoot);
            var manifestPath = Path.Combine(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
            SetManifestSchemaVersion(manifestPath, "matrix-artifact-manifest.v999");
            WriteMatrixProofSummary(artifactRoot);

            var result = RunMatrixCli("verify", artifactRoot, "--json");

            Assert.Equal(1, result.ExitCode);
            using var document = JsonDocument.Parse(result.StandardOutput);
            var root = document.RootElement;
            AssertContainsReasonCode(root, "unsupported_version");
            Assert.Contains(
                root.GetProperty("issues").EnumerateArray(),
                issue => issue.GetProperty("artifact_kind").GetString() == "manifest"
                         && issue.GetProperty("code").GetString() == "unsupported_schema"
                         && issue.GetProperty("reason_code").GetString() == "unsupported_version");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixArtifactManifestWriter_EmitsRequiredFieldsAndSummaryOnlyPrivacy()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-manifest-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteArtifact(artifactRoot, "project/decisions.jsonl", """{"decision":"allow"}""");
            WriteArtifact(artifactRoot, "project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1"}""");
            WriteArtifact(artifactRoot, "project/shield-evidence.json", """{"schema_version":"shield-evidence.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-evaluate.json", """{"schema_version":"shield-evaluate.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-badge.json", """{"schema_version":"shield-badge.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-badge.svg", "<svg></svg>");
            WriteArtifact(artifactRoot, "project/matrix-summary.json", """{"smoke":"matrix_e2e"}""");

            var createdAt = DateTimeOffset.Parse("2026-04-15T00:00:00+00:00");
            var manifestPath = MatrixArtifactManifestWriter.WriteDefaultProofManifest(artifactRoot, createdAt);
            var verification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);

            Assert.Equal(Path.Combine(artifactRoot, "matrix-artifact-manifest.json"), manifestPath);
            Assert.Equal("verified", verification.VerificationPosture);
            Assert.Empty(verification.Issues);
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;

            Assert.Equal("matrix-artifact-manifest.v0", root.GetProperty("schema_version").GetString());
            Assert.Equal("carves-matrix", root.GetProperty("producer").GetProperty("tool").GetString());
            Assert.Equal("CARVES.Matrix.Core", root.GetProperty("producer").GetProperty("component").GetString());
            Assert.Equal("local_proof_bundle", root.GetProperty("producer").GetProperty("mode").GetString());
            Assert.Equal(MatrixArtifactManifestWriter.PortableArtifactRoot, root.GetProperty("artifact_root").GetString());
            Assert.Equal(MatrixArtifactManifestWriter.RedactedLocalArtifactRoot, root.GetProperty("producer_artifact_root").GetString());
            AssertSummaryOnlyPrivacy(root.GetProperty("privacy"));

            var artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "guard_decision");
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "handoff_packet");
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "audit_evidence");
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "shield_evaluation");
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "shield_badge_json");
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "shield_badge_svg");
            Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "matrix_summary");

            foreach (var artifact in artifacts)
            {
                Assert.False(Path.IsPathRooted(artifact.GetProperty("path").GetString()!));
                Assert.Equal(64, artifact.GetProperty("sha256").GetString()!.Length);
                Assert.True(artifact.GetProperty("size").GetInt64() > 0);
                Assert.False(string.IsNullOrWhiteSpace(artifact.GetProperty("schema_version").GetString()));
                Assert.False(string.IsNullOrWhiteSpace(artifact.GetProperty("producer").GetString()));
                Assert.Equal("2026-04-15T00:00:00+00:00", artifact.GetProperty("created_at").GetString());
                AssertSummaryOnlyPrivacy(artifact.GetProperty("privacy_flags"));
            }

            File.AppendAllText(Path.Combine(artifactRoot, "project", "shield-evidence.json"), "\n{\"mutated\":true}");
            var changed = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);
            Assert.Equal("changed_or_substituted", changed.VerificationPosture);
            Assert.Contains(
                changed.Issues,
                issue => issue.ArtifactKind == "audit_evidence" && issue.Code == "artifact_hash_mismatch");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixArtifactManifestVerifier_HandlesLargeGuardJsonlAndReportsSizeMismatch()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-large-manifest-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var largeGuardJsonl = string.Join(
                Environment.NewLine,
                Enumerable.Range(0, 1500).Select(index =>
                    $$"""{"schema_version":1,"run_id":"GRD-MATRIX-LARGE-{{index:D4}}","decision":"allow","summary":"{{new string('a', 220)}}"}"""));
            WriteArtifact(artifactRoot, "project/decisions.jsonl", largeGuardJsonl);
            WriteArtifact(artifactRoot, "project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1"}""");
            WriteArtifact(artifactRoot, "project/shield-evidence.json", """{"schema_version":"shield-evidence.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-evaluate.json", """{"schema_version":"shield-evaluate.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-badge.json", """{"schema_version":"shield-badge.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-badge.svg", "<svg></svg>");
            WriteArtifact(artifactRoot, "project/matrix-summary.json", """{"smoke":"matrix_large_log_stress"}""");

            var manifestPath = MatrixArtifactManifestWriter.WriteDefaultProofManifest(artifactRoot, DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"));
            var verification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);

            Assert.Equal("verified", verification.VerificationPosture);
            Assert.Empty(verification.Issues);

            var guardPath = Path.Combine(artifactRoot, "project", "decisions.jsonl");
            Assert.True(new FileInfo(guardPath).Length > 300_000);

            SetArtifactManifestLongField(
                manifestPath,
                "guard_decision",
                "size",
                new FileInfo(guardPath).Length + 1);
            var changed = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);

            Assert.Equal("changed_or_substituted", changed.VerificationPosture);
            Assert.Contains(
                changed.Issues,
                issue => issue.ArtifactKind == "guard_decision"
                         && issue.Code == "artifact_size_mismatch"
                         && issue.ExpectedSize == new FileInfo(guardPath).Length + 1
                         && issue.ActualSize == new FileInfo(guardPath).Length);
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixArtifactManifestVerifier_FailsPrivacyGateWhenForbiddenArtifactFlagIsTrue()
    {
        var artifactRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-privacy-gate-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteArtifact(artifactRoot, "project/decisions.jsonl", """{"decision":"allow"}""");
            WriteArtifact(artifactRoot, "project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1"}""");
            WriteArtifact(artifactRoot, "project/shield-evidence.json", """{"schema_version":"shield-evidence.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-evaluate.json", """{"schema_version":"shield-evaluate.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-badge.json", """{"schema_version":"shield-badge.v0"}""");
            WriteArtifact(artifactRoot, "project/shield-badge.svg", "<svg></svg>");
            WriteArtifact(artifactRoot, "project/matrix-summary.json", """{"smoke":"matrix_e2e"}""");

            var manifestPath = MatrixArtifactManifestWriter.WriteDefaultProofManifest(artifactRoot);
            SetArtifactPrivacyFlag(manifestPath, "audit_evidence", "raw_diff_included", value: true);

            var verification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);

            Assert.Equal("privacy_gate_failed", verification.VerificationPosture);
            Assert.Contains(
                verification.Issues,
                issue => issue.ArtifactKind == "audit_evidence"
                         && issue.Code == "privacy_forbidden_flag_true:raw_diff_included");
        }
        finally
        {
            if (Directory.Exists(artifactRoot))
            {
                Directory.Delete(artifactRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void MatrixArtifactManifestDocs_DefineSchemaAndExampleFixture()
    {
        var repoRoot = LocateSourceRepoRoot();
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "matrix-artifact-manifest-v0.md"));
        var schema = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "schemas", "matrix-artifact-manifest.v0.schema.json"));
        var fixturePath = Path.Combine(repoRoot, "docs", "matrix", "examples", "matrix-artifact-manifest.v0.schema-example.json");

        Assert.Contains("matrix-artifact-manifest.v0", doc, StringComparison.Ordinal);
        Assert.Contains("sha256", doc, StringComparison.Ordinal);
        Assert.Contains("size", doc, StringComparison.Ordinal);
        Assert.Contains("privacy_flags", doc, StringComparison.Ordinal);
        Assert.Contains("private_payload_included", doc, StringComparison.Ordinal);
        Assert.Contains("privacy_gate_failed", doc, StringComparison.Ordinal);
        Assert.Contains("missing_artifact", doc, StringComparison.Ordinal);
        Assert.Contains("hash_mismatch", doc, StringComparison.Ordinal);
        Assert.Contains("schema_mismatch", doc, StringComparison.Ordinal);
        Assert.Contains("privacy_violation", doc, StringComparison.Ordinal);
        Assert.Contains("unverified_score", doc, StringComparison.Ordinal);
        Assert.Contains("unsupported_version", doc, StringComparison.Ordinal);
        Assert.Contains("Linux-native public artifact recheck path", doc, StringComparison.Ordinal);
        Assert.Contains("without rerunning Guard, Handoff, Audit, Shield, Matrix proof scripts, `pwsh`, or repo-local release lanes", doc, StringComparison.Ordinal);
        Assert.Contains("guard_decision", doc, StringComparison.Ordinal);
        Assert.Contains("handoff_packet", doc, StringComparison.Ordinal);
        Assert.Contains("audit_evidence", doc, StringComparison.Ordinal);
        Assert.Contains("shield_evaluation", doc, StringComparison.Ordinal);
        Assert.Contains("shield_badge_json", doc, StringComparison.Ordinal);
        Assert.Contains("matrix_summary", doc, StringComparison.Ordinal);
        Assert.Contains("large-log-stress.md", doc, StringComparison.Ordinal);
        Assert.Contains("hosted verification", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("operating-system sandboxing", doc, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("\"schema_version\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"matrix-artifact-manifest.v0\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"sha256\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"size\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"privacy_flags\"", schema, StringComparison.Ordinal);
        Assert.Contains("\"private_payload_included\"", schema, StringComparison.Ordinal);

        using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var root = fixture.RootElement;
        Assert.Equal("matrix-artifact-manifest.v0", root.GetProperty("schema_version").GetString());
        AssertSummaryOnlyPrivacy(root.GetProperty("privacy"));
        var artifacts = root.GetProperty("artifacts").EnumerateArray().ToArray();
        Assert.True(artifacts.Length >= 7);
        Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "guard_decision");
        Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "handoff_packet");
        Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "audit_evidence");
        Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "shield_evaluation");
        Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "shield_badge_svg");
        Assert.Contains(artifacts, item => item.GetProperty("artifact_kind").GetString() == "matrix_summary");
        foreach (var artifact in artifacts)
        {
            Assert.Equal(64, artifact.GetProperty("sha256").GetString()!.Length);
            Assert.True(artifact.GetProperty("size").GetInt64() >= 0);
            AssertSummaryOnlyPrivacy(artifact.GetProperty("privacy_flags"));
        }
    }

    [Fact]
    public void AuditEvidence_RecordsGuardAndHandoffInputArtifactHashes()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), "carves-audit-input-hash-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            WriteArtifact(repoRoot, AuditInputReader.DefaultGuardDecisionPath, """
            {"schema_version":1,"run_id":"guard-run-1","recorded_at_utc":"2026-04-15T00:00:00Z","source":"carves-guard","outcome":"allow","policy_id":"policy-1","changed_files":[],"violations":[],"warnings":[],"evidence_refs":["guard-run:guard-run-1"]}
            """);
            WriteArtifact(repoRoot, AuditInputReader.DefaultHandoffPacketPath, """
            {
              "schema_version": "carves-continuity-handoff.v1",
              "handoff_id": "HND-INPUT-HASH",
              "created_at_utc": "2026-04-15T00:00:00Z",
              "resume_status": "ready",
              "current_objective": "Keep the proof input hashes linked.",
              "remaining_work": [
                {
                  "action": "Review linked artifacts."
                }
              ],
              "completed_facts": [
                {
                  "statement": "Guard evidence is present.",
                  "evidence_refs": [
                    "guard-run:guard-run-1"
                  ]
                }
              ],
              "blocked_reasons": [],
              "must_not_repeat": [
                {
                  "item": "Do not substitute local evidence after proof generation.",
                  "reason": "The proof is hash-linked."
                }
              ],
              "decision_refs": [
                "guard-run:guard-run-1"
              ],
              "context_refs": [
                {
                  "ref": ".ai/runtime/guard/decisions.jsonl"
                }
              ],
              "evidence_refs": [
                {
                  "ref": "guard-run:guard-run-1"
                }
              ],
              "confidence": "high"
            }
            """);

            var evidence = new AuditEvidenceService().BuildEvidence(new AuditInputOptions(
                repoRoot,
                AuditInputReader.DefaultGuardDecisionPath,
                GuardDecisionPathExplicit: false,
                HandoffPacketPaths: [],
                HandoffPacketPathsExplicit: false));

            var inputArtifacts = evidence.Provenance.InputArtifacts;
            var guard = Assert.Single(inputArtifacts, artifact => artifact.Kind == "guard_decisions");
            var handoff = Assert.Single(inputArtifacts, artifact => artifact.Kind == "handoff_packet");
            AssertInputArtifact(repoRoot, guard, AuditInputReader.DefaultGuardDecisionPath, "loaded");
            AssertInputArtifact(repoRoot, handoff, AuditInputReader.DefaultHandoffPacketPath, "loaded");
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    private static void WriteArtifact(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
    }

    private static void CreateVerifiableMatrixBundle(string artifactRoot, string? shieldEvaluationJson = null)
    {
        WriteArtifact(artifactRoot, "project/decisions.jsonl", """{"decision":"allow"}""");
        WriteArtifact(artifactRoot, "project/handoff.json", """{"schema_version":"carves-continuity-handoff.v1"}""");
        WriteArtifact(artifactRoot, "project/shield-evidence.json", """{"schema_version":"shield-evidence.v0"}""");
        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(Path.Combine(artifactRoot, "project", "shield-evidence.json"));
        WriteArtifact(artifactRoot, "project/shield-evaluate.json", shieldEvaluationJson ?? ValidShieldEvaluationJson(evidenceSha256));
        WriteArtifact(artifactRoot, "project/shield-badge.json", """{"schema_version":"shield-badge.v0"}""");
        WriteArtifact(artifactRoot, "project/shield-badge.svg", "<svg></svg>");
        WriteArtifact(artifactRoot, "project/matrix-summary.json", MatrixSummaryJson(artifactRoot));
        MatrixArtifactManifestWriter.WriteDefaultProofManifest(artifactRoot, DateTimeOffset.Parse("2026-04-15T00:00:00+00:00"));
        WriteMatrixProofSummary(artifactRoot);
    }

    private static string ValidShieldEvaluationJson(string consumedEvidenceSha256)
    {
        return $$"""
        {
          "schema_version": "shield-evaluate.v0",
          "status": "ok",
          "certification": false,
          "consumed_evidence_sha256": "{{consumedEvidenceSha256}}",
          "standard": {
            "label": "CARVES G1.H1.A1 /1d PASS"
          },
          "lite": {
            "score": 50,
            "band": "disciplined"
          }
        }
        """;
    }

    private static void WriteMatrixProofSummary(string artifactRoot)
    {
        var manifestPath = Path.Combine(artifactRoot, MatrixArtifactManifestWriter.DefaultManifestFileName);
        var manifestVerification = MatrixArtifactManifestWriter.VerifyManifest(manifestPath);
        using var matrixSummaryDocument = JsonDocument.Parse(File.ReadAllText(Path.Combine(artifactRoot, "project", "matrix-summary.json")));
        var matrixSummaryRoot = matrixSummaryDocument.RootElement;
        var summary = new
        {
            schema_version = "matrix-proof-summary.v0",
            smoke = "matrix_native_minimal_proof_lane",
            shell = "carves-matrix",
            proof_mode = "native_minimal",
            proof_capabilities = NativeProofCapabilities(),
            artifact_root = MatrixArtifactManifestWriter.PortableArtifactRoot,
            artifact_manifest = new
            {
                path = MatrixArtifactManifestWriter.DefaultManifestFileName,
                schema_version = MatrixArtifactManifestWriter.ManifestSchemaVersion,
                sha256 = MatrixArtifactManifestWriter.ComputeFileSha256(manifestPath),
                verification_posture = manifestVerification.VerificationPosture,
                issue_count = manifestVerification.Issues.Count,
            },
            trust_chain_hardening = ValidTrustChainHardening(),
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
                artifact_root = GetString(matrixSummaryRoot, "artifact_root"),
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
        File.WriteAllText(
            Path.Combine(artifactRoot, "matrix-proof-summary.json"),
            JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = true,
            }));
    }

    private static object ValidTrustChainHardening()
    {
        return new
        {
            gates_satisfied = true,
            computed_by = "matrix_verifier",
            gates = new object[]
            {
                ValidGate("manifest_integrity", "Manifest hashes, sizes, privacy flags, and artifact file presence verified."),
                ValidGate("required_artifacts", "All required artifact entries are present with expected path, schema, and producer metadata."),
                ValidGate("trial_artifacts", "Agent Trial artifacts are not present; non-trial Matrix compatibility mode applies."),
                ValidGate("shield_score", "Shield evaluation status, certification posture, Standard label, and Lite score fields are verified."),
                ValidGate("summary_consistency", "Matrix proof summary references the current manifest hash, posture, and issue count."),
            },
        };
    }

    private static object NativeProofCapabilities()
    {
        return new
        {
            proof_lane = "native_minimal",
            execution_backend = "dotnet_runner_chain",
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
        };
    }

    private static object ValidGate(string gateId, string reason)
    {
        return new
        {
            gate_id = gateId,
            satisfied = true,
            reason,
            issue_codes = Array.Empty<string>(),
            reason_codes = Array.Empty<string>(),
        };
    }

    private static string MatrixSummaryJson(string artifactRoot)
    {
        var evidenceSha256 = MatrixArtifactManifestWriter.ComputeFileSha256(Path.Combine(artifactRoot, "project", "shield-evidence.json"));
        var summary = new
        {
            schema_version = "matrix-summary.v0",
            proof_role = "composition_orchestrator",
            proof_mode = "native_minimal",
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
        return JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        });
    }

    private static string? GetString(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind == JsonValueKind.String
            ? current.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path)
               && current.ValueKind == JsonValueKind.Number
               && current.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool? GetBool(JsonElement element, params string[] path)
    {
        return TryGet(element, out var current, path) && current.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? current.GetBoolean()
            : null;
    }

    private static bool TryGet(JsonElement element, out JsonElement value, params string[] path)
    {
        value = element;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static void AssertContainsReasonCode(JsonElement root, string reasonCode)
    {
        Assert.Contains(
            root.GetProperty("reason_codes").EnumerateArray(),
            code => code.GetString() == reasonCode);
    }

    private static string ReadMatrixCoreSource(string repoRoot, string fileName)
    {
        return File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Matrix.Core", fileName));
    }

    private static string ReadMatrixCoreSources(string repoRoot)
    {
        var coreRoot = Path.Combine(repoRoot, "src", "CARVES.Matrix.Core");
        return string.Join(
            Environment.NewLine,
            Directory.GetFiles(coreRoot, "Matrix*.cs")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static MatrixCliRunResult RunMatrixCli(params string[] arguments)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = MatrixCliRunner.Run(arguments);
            return new MatrixCliRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record MatrixCliRunResult(int ExitCode, string StandardOutput, string StandardError);

    private static void AssertSummaryOnlyPrivacy(JsonElement privacy)
    {
        Assert.True(privacy.GetProperty("summary_only").GetBoolean());
        Assert.False(privacy.GetProperty("source_included").GetBoolean());
        Assert.False(privacy.GetProperty("raw_diff_included").GetBoolean());
        Assert.False(privacy.GetProperty("prompt_included").GetBoolean());
        Assert.False(privacy.GetProperty("model_response_included").GetBoolean());
        Assert.False(privacy.GetProperty("secrets_included").GetBoolean());
        Assert.False(privacy.GetProperty("credentials_included").GetBoolean());
        Assert.False(privacy.GetProperty("private_payload_included").GetBoolean());
        Assert.False(privacy.GetProperty("customer_payload_included").GetBoolean());
        Assert.False(privacy.GetProperty("hosted_upload_required").GetBoolean());
        Assert.False(privacy.GetProperty("certification_claim").GetBoolean());
        Assert.False(privacy.GetProperty("public_leaderboard_claim").GetBoolean());
    }

    private static void SetArtifactPrivacyFlag(
        string manifestPath,
        string artifactKind,
        string flag,
        bool value)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Manifest JSON root was not an object.");
        var artifacts = manifest["artifacts"]?.AsArray()
            ?? throw new InvalidOperationException("Manifest did not include artifacts.");
        foreach (var artifact in artifacts.OfType<JsonObject>())
        {
            if (string.Equals((string?)artifact["artifact_kind"], artifactKind, StringComparison.Ordinal))
            {
                var privacyFlags = artifact["privacy_flags"]?.AsObject()
                    ?? throw new InvalidOperationException("Artifact did not include privacy_flags.");
                privacyFlags[flag] = value;
                File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
        }

        throw new InvalidOperationException($"Artifact kind was not found: {artifactKind}");
    }

    private static void SetArtifactManifestField(
        string manifestPath,
        string artifactKind,
        string field,
        string value)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Manifest JSON root was not an object.");
        var artifacts = manifest["artifacts"]?.AsArray()
            ?? throw new InvalidOperationException("Manifest did not include artifacts.");
        foreach (var artifact in artifacts.OfType<JsonObject>())
        {
            if (string.Equals((string?)artifact["artifact_kind"], artifactKind, StringComparison.Ordinal))
            {
                artifact[field] = value;
                File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
        }

        throw new InvalidOperationException($"Artifact kind was not found: {artifactKind}");
    }

    private static void SetArtifactManifestLongField(
        string manifestPath,
        string artifactKind,
        string field,
        long value)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Manifest JSON root was not an object.");
        var artifacts = manifest["artifacts"]?.AsArray()
            ?? throw new InvalidOperationException("Manifest did not include artifacts.");
        foreach (var artifact in artifacts.OfType<JsonObject>())
        {
            if (string.Equals((string?)artifact["artifact_kind"], artifactKind, StringComparison.Ordinal))
            {
                artifact[field] = value;
                File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                return;
            }
        }

        throw new InvalidOperationException($"Artifact kind was not found: {artifactKind}");
    }

    private static void SetManifestSchemaVersion(string manifestPath, string schemaVersion)
    {
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidOperationException("Manifest JSON root was not an object.");
        manifest["schema_version"] = schemaVersion;
        File.WriteAllText(manifestPath, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void AssertInputArtifact(
        string root,
        ShieldEvidenceInputArtifact artifact,
        string relativePath,
        string inputStatus)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(relativePath, artifact.Path);
        Assert.Equal(inputStatus, artifact.InputStatus);
        Assert.Equal(MatrixArtifactManifestWriter.ComputeFileSha256(path), artifact.Sha256);
        Assert.Equal(new FileInfo(path).Length, artifact.Size);
    }

    private static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root from test output directory.");
    }
}
