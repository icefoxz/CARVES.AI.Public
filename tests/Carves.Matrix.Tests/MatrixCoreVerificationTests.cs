using System.Text.Json;
using static Carves.Matrix.Tests.MatrixCliTestRunner;
using static Carves.Matrix.Tests.MatrixVerifyJsonAssertions;

namespace Carves.Matrix.Tests;

public sealed class MatrixCoreVerificationTests
{
    [Fact]
    public void VerifyCommand_ValidBundlePassesInProcess()
    {
        using var bundle = MatrixBundleFixture.Create();

        var result = RunMatrixCli("verify", bundle.ArtifactRoot, "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("matrix-verify.v0", root.GetProperty("schema_version").GetString());
        Assert.Equal("verified", root.GetProperty("status").GetString());
        Assert.Equal("verified", root.GetProperty("verification_posture").GetString());
        Assert.Empty(root.GetProperty("reason_codes").EnumerateArray());
        Assert.Equal(0, root.GetProperty("issue_count").GetInt32());
        Assert.True(root.GetProperty("trust_chain_hardening").GetProperty("gates_satisfied").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_ReportsMissingRequiredArtifactEntry()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.RemoveManifestArtifact("audit_evidence");
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "missing_artifact");
        AssertContainsIssue(root, "audit_evidence", "required_artifact_entry_missing", "missing_artifact");
    }

    [Fact]
    public void VerifyCommand_ReportsMissingRequiredArtifactFile()
    {
        using var bundle = MatrixBundleFixture.Create();
        File.Delete(Path.Combine(bundle.ArtifactRoot, "project", "shield-evaluate.json"));

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "missing_artifact");
        AssertContainsIssue(root, "shield_evaluation", "artifact_missing", "missing_artifact");
    }

    [Fact]
    public void VerifyCommand_ReportsHashMismatchReasonCode()
    {
        using var bundle = MatrixBundleFixture.Create();
        File.AppendAllText(Path.Combine(bundle.ArtifactRoot, "project", "shield-evidence.json"), "\n{\"mutated\":true}");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "hash_mismatch");
        AssertContainsReasonCode(root, "unverified_score");
        AssertContainsIssue(root, "audit_evidence", "artifact_hash_mismatch", "hash_mismatch");
        AssertContainsIssue(root, "shield_evaluation", "shield_evidence_hash_mismatch", "unverified_score");
    }

    [Fact]
    public void VerifyCommand_ReportsSizeMismatchReasonCode()
    {
        using var bundle = MatrixBundleFixture.Create();
        var artifactPath = Path.Combine(bundle.ArtifactRoot, "project", "decisions.jsonl");
        bundle.SetArtifactManifestLongField("guard_decision", "size", new FileInfo(artifactPath).Length + 1);
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "hash_mismatch");
        AssertContainsIssue(root, "guard_decision", "artifact_size_mismatch", "hash_mismatch");
    }

    [Fact]
    public void VerifyCommand_ReportsSchemaMismatchReasonCode()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.SetArtifactManifestStringField("shield_evaluation", "schema_version", "shield-evaluate.v999");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "schema_mismatch");
        AssertContainsIssue(root, "shield_evaluation", "required_artifact_schema_mismatch", "schema_mismatch");
        AssertContainsIssue(root, "shield_evaluation", "shield_evaluation_source_manifest_shield_evaluation_schema_version_mismatch", "schema_mismatch");
    }

    [Fact]
    public void VerifyCommand_ReportsPrivacyViolationReasonCode()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.SetArtifactPrivacyFlag("audit_evidence", "raw_diff_included", value: true);
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "privacy_violation");
        AssertContainsIssue(root, "audit_evidence", "privacy_forbidden_flag_true:raw_diff_included", "privacy_violation");
    }

    [Fact]
    public void VerifyCommand_ReportsUnverifiedShieldScoreReasonCode()
    {
        using var bundle = MatrixBundleFixture.Create(
            """
            {
              "schema_version": "shield-evaluate.v0",
              "status": "invalid_input",
              "certification": false
            }
            """);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "unverified_score");
        AssertContainsIssue(root, "shield_evaluation", "shield_score_unverified", "unverified_score");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationMissingConsumedEvidenceHash()
    {
        using var bundle = MatrixBundleFixture.Create(
            """
            {
              "schema_version": "shield-evaluate.v0",
              "status": "ok",
              "certification": false,
              "standard": {
                "label": "CARVES G1.H1.A1 /1d PASS"
              },
              "lite": {
                "score": 50,
                "band": "disciplined"
              }
            }
            """);

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "unverified_score");
        AssertContainsIssue(root, "shield_evaluation", "shield_evidence_hash_missing", "unverified_score");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationBoundToDifferentEvidenceHash()
    {
        using var bundle = MatrixBundleFixture.Create(MatrixBundleFixture.ValidShieldEvaluationJson(new string('0', 64)));

        var root = RunVerifyJson(bundle.ArtifactRoot);

        Assert.Equal("failed", root.GetProperty("status").GetString());
        AssertContainsReasonCode(root, "unverified_score");
        AssertContainsIssue(root, "shield_evaluation", "shield_evidence_hash_mismatch", "unverified_score");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationMissingManifestEntryBeforeScoreTrust()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.RemoveManifestArtifact("shield_evaluation");
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "shield_evaluation", "shield_evaluation_source_manifest_entry_missing:shield_evaluation", "missing_artifact");
        AssertDoesNotContainIssue(root, "shield_evaluation", "shield_score_unverified");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationSemanticHashDriftBeforeScoreTrust()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.ReplaceShieldEvaluationTextAndRefreshProofSummary("\"status\": \"ok\"", "\"status\": \"no\"");

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "shield_evaluation", "shield_evaluation_source_manifest_hash_mismatch:shield_evaluation", "hash_mismatch");
        AssertDoesNotContainIssue(root, "shield_evaluation", "shield_score_unverified");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationSemanticSizeDriftBeforeScoreTrust()
    {
        using var bundle = MatrixBundleFixture.Create();
        var artifactPath = Path.Combine(bundle.ArtifactRoot, "project", "shield-evaluate.json");
        bundle.SetArtifactManifestLongField("shield_evaluation", "size", new FileInfo(artifactPath).Length + 1);
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "shield_evaluation", "shield_evaluation_source_manifest_size_mismatch:shield_evaluation", "hash_mismatch");
        AssertDoesNotContainIssue(root, "shield_evaluation", "shield_score_unverified");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationDuplicateManifestEntryBeforeScoreTrust()
    {
        using var bundle = MatrixBundleFixture.Create();
        bundle.DuplicateManifestArtifact("shield_evaluation");
        bundle.WriteProofSummary();

        var root = RunVerifyJson(bundle.ArtifactRoot);

        AssertContainsIssue(root, "shield_evaluation", "shield_evaluation_source_manifest_entry_duplicate:shield_evaluation", "schema_mismatch");
        AssertDoesNotContainIssue(root, "shield_evaluation", "shield_score_unverified");
        Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
    }

    [Fact]
    public void VerifyCommand_RejectsShieldEvaluationReparsePointBeforeScoreTrust()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var bundle = MatrixBundleFixture.Create();
        var outsideRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-shield-evaluation-symlink-target-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(outsideRoot);
            var sourcePath = Path.Combine(bundle.ArtifactRoot, "project", "shield-evaluate.json");
            var outsidePath = Path.Combine(outsideRoot, "shield-evaluate.json");
            File.WriteAllText(outsidePath, File.ReadAllText(sourcePath));
            File.Delete(sourcePath);
            File.CreateSymbolicLink(sourcePath, outsidePath);
            bundle.WriteProofSummary();

            var root = RunVerifyJson(bundle.ArtifactRoot);

            AssertContainsIssue(root, "shield_evaluation", "shield_evaluation_source_reparse_point_rejected:shield_evaluation", "schema_mismatch");
            AssertDoesNotContainIssue(root, "shield_evaluation", "shield_score_unverified");
            Assert.False(root.GetProperty("shield_evaluation").GetProperty("score_verified").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(outsideRoot))
            {
                Directory.Delete(outsideRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void VerifyCommand_PathIsNativeCoreAndDoesNotInvokePowerShellScripts()
    {
        var repoRoot = LocateSourceRepoRoot();
        var verifyPath = ReadMatrixCoreSource(repoRoot, "MatrixVerifyCommand.cs");

        Assert.Contains("BuildVerifyResult(options.ArtifactRoot!, options.RequireTrial)", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeScript", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeProcess", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveMatrixScript", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("pwsh", verifyPath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrix-e2e-smoke.ps1", verifyPath, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-packaged-install-smoke.ps1", verifyPath, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixCliRunner_IsDecomposedIntoCommandAndSupportFiles()
    {
        var repoRoot = LocateSourceRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "CARVES.Matrix.Core");
        var runner = File.ReadAllText(Path.Combine(coreRoot, "MatrixCliRunner.cs"));
        var expectedFiles = new[]
        {
            "MatrixProofCommand.cs",
            "MatrixFullReleaseProofAssembly.cs",
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
            "MatrixNativeProjectProofArtifacts.cs",
            "MatrixNativeProjectProofCommand.cs",
            "MatrixNativeProjectProofHandoff.cs",
            "MatrixNativeProjectProofModels.cs",
            "MatrixNativeProjectProofProductChain.cs",
            "MatrixNativeProjectProofRepository.cs",
            "MatrixNativeProjectProofSupport.cs",
            "MatrixNativeFullReleaseProofCommand.cs",
            "MatrixNativeFullReleaseProofAtomicity.cs",
            "MatrixNativeFullReleaseProofModels.cs",
            "MatrixNativeFullReleaseProofProducer.cs",
            "MatrixNativePackagedProofArtifacts.cs",
            "MatrixNativePackagedProofCommand.cs",
            "MatrixNativePackagedProofModels.cs",
            "MatrixNativePackagedProofProcess.cs",
            "MatrixNativePackagedProofProductChain.cs",
            "MatrixNativePackagedProofRepository.cs",
            "MatrixNativePackagingCommands.cs",
            "MatrixNativePackagingHarness.cs",
            "MatrixNativePackagingModels.cs",
            "MatrixNativePackagingSpecs.cs",
            "MatrixArtifactManifestWriter.cs",
            "MatrixArtifactManifestRequirements.cs",
            "MatrixArtifactManifestMaterialization.cs",
            "MatrixArtifactPathPolicy.cs",
            "MatrixArtifactManifestVerifier.cs",
            "MatrixArtifactManifestPrivacy.cs",
            "MatrixArtifactManifestIO.cs",
            "MatrixArtifactManifestModels.cs",
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
            "MatrixVerifyTrialLocalScore.cs",
            "MatrixProofSummaryBuilder.cs",
            "MatrixProofSummaryPublicContract.cs",
            "MatrixProofSummaryPublicContractVerifier.cs",
            "MatrixProofSummaryTrustChainProjection.cs",
            "MatrixScriptCommand.cs",
            "MatrixProcessInvoker.cs",
            "MatrixArtifactLayout.cs",
            "MatrixJsonOutput.cs",
            "MatrixCliUsage.cs",
            "MatrixProofOptions.cs",
            "MatrixVerifyOptions.cs",
            "MatrixTrialOptions.cs",
            "MatrixTrialCommand.cs",
            "MatrixTrialCommandModels.cs",
            "MatrixTrialCommandOutput.cs",
            "MatrixTrialDiagnostics.cs",
            "MatrixTrialHistoryCommand.cs",
            "MatrixTrialHistoryModels.cs",
            "MatrixTrialHistoryStore.cs",
            "MatrixTrialBundleWriter.cs",
            "MatrixTrialScoreReadback.cs",
            "MatrixTrialResultCard.cs",
            "AgentTrialLocalScoreMapper.cs",
            "MatrixCliModels.cs",
        };

        foreach (var file in expectedFiles)
        {
            Assert.True(File.Exists(Path.Combine(coreRoot, file)), $"Missing Matrix CLI decomposition file: {file}");
        }

        Assert.Contains("public static partial class MatrixCliRunner", runner, StringComparison.Ordinal);
        Assert.Contains("\"verify\" => RunVerify", runner, StringComparison.Ordinal);
        Assert.Contains("\"trial\" => RunTrial", runner, StringComparison.Ordinal);
        Assert.Contains("RunScriptCommand(\"matrix-e2e-smoke.ps1\"", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildVerifyResult", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("RunNativeCliStep", runner, StringComparison.Ordinal);
        Assert.Contains("TryPrepareNativeProofRepository", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofRepositorySetup.cs"), StringComparison.Ordinal);
        Assert.Contains("TryRunNativeProofProductChain", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofProductChain.cs"), StringComparison.Ordinal);
        Assert.Contains("CompleteNativeMinimalProof", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofFinalization.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyRequiredArtifacts", ReadMatrixCoreSource(repoRoot, "MatrixVerifyArtifacts.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyShieldEvaluation", ReadMatrixCoreSource(repoRoot, "MatrixVerifyShieldEvaluation.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyFullReleaseProofSummary", ReadMatrixCoreSource(repoRoot, "MatrixVerifyFullReleaseSummary.cs"), StringComparison.Ordinal);
        Assert.Contains("TryReadVerifiedSummarySourceDocument", ReadMatrixCoreSource(repoRoot, "MatrixVerifiedArtifactReader.cs"), StringComparison.Ordinal);
        Assert.Contains("TryReadVerifiedShieldEvaluationDocument", ReadMatrixCoreSource(repoRoot, "MatrixVerifiedArtifactReader.cs"), StringComparison.Ordinal);
        Assert.Contains("TryGetVerifiedArtifactManifestEntry", ReadMatrixCoreSource(repoRoot, "MatrixVerifiedArtifactManifestEntryReader.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifySummaryTrustChainHardening", ReadMatrixCoreSource(repoRoot, "MatrixVerifySummaryTrustChain.cs"), StringComparison.Ordinal);
        Assert.Contains("MatrixProofSummaryPublicContractModel", ReadMatrixCoreSource(repoRoot, "MatrixProofSummaryPublicContract.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyProofSummaryPublicContract", ReadMatrixCoreSource(repoRoot, "MatrixProofSummaryPublicContractVerifier.cs"), StringComparison.Ordinal);
        Assert.Contains("BuildNativeProofSummary", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofSummary.cs"), StringComparison.Ordinal);
        Assert.Contains("ProduceNativeFullReleaseProjectArtifacts", ReadMatrixCoreSource(repoRoot, "MatrixNativeProjectProofCommand.cs"), StringComparison.Ordinal);
        Assert.Contains("TryRunNativeFullReleaseProjectChain", ReadMatrixCoreSource(repoRoot, "MatrixNativeProjectProofProductChain.cs"), StringComparison.Ordinal);
        Assert.Contains("ProduceNativeFullReleasePackagedArtifacts", ReadMatrixCoreSource(repoRoot, "MatrixNativePackagedProofCommand.cs"), StringComparison.Ordinal);
        Assert.Contains("RunNativeFullReleaseProof", ReadMatrixCoreSource(repoRoot, "MatrixNativeFullReleaseProofCommand.cs"), StringComparison.Ordinal);
        Assert.Contains("ProduceNativeFullReleaseProofArtifacts", ReadMatrixCoreSource(repoRoot, "MatrixNativeFullReleaseProofProducer.cs"), StringComparison.Ordinal);
        Assert.Contains("PreserveNativeFullReleaseFailure", ReadMatrixCoreSource(repoRoot, "MatrixNativeFullReleaseProofAtomicity.cs"), StringComparison.Ordinal);
        Assert.Contains("CompleteFullReleaseProof", ReadMatrixCoreSource(repoRoot, "MatrixFullReleaseProofAssembly.cs"), StringComparison.Ordinal);
        Assert.Contains("RunNativeInstalledMatrixChain", ReadMatrixCoreSource(repoRoot, "MatrixNativePackagedProofProcess.cs"), StringComparison.Ordinal);
        Assert.Contains("TryRunNativePackagedProductChain", ReadMatrixCoreSource(repoRoot, "MatrixNativePackagedProofProductChain.cs"), StringComparison.Ordinal);
        Assert.Contains("RunNativePackagingHarness", ReadMatrixCoreSource(repoRoot, "MatrixNativePackagingHarness.cs"), StringComparison.Ordinal);
        Assert.Contains("PackNativeTool", ReadMatrixCoreSource(repoRoot, "MatrixNativePackagingCommands.cs"), StringComparison.Ordinal);
        Assert.Contains("RunNativeCliStep", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofStepRunner.cs"), StringComparison.Ordinal);
        Assert.Contains("WriteNativeProofFailure", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofFailure.cs"), StringComparison.Ordinal);
        Assert.Contains("ResolveNativeRelativePath", ReadMatrixCoreSource(repoRoot, "MatrixNativeProofIO.cs"), StringComparison.Ordinal);
        Assert.Contains("WriteManifest", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestWriter.cs"), StringComparison.Ordinal);
        Assert.Contains("DefaultRequiredArtifactRequirements", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestRequirements.cs"), StringComparison.Ordinal);
        Assert.Contains("VerifyManifestArtifact", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestVerifier.cs"), StringComparison.Ordinal);
        Assert.Contains("ValidatePrivacyFlags", ReadMatrixCoreSource(repoRoot, "MatrixArtifactManifestPrivacy.cs"), StringComparison.Ordinal);
        Assert.Contains("InvokeProcess", ReadMatrixCoreSource(repoRoot, "MatrixProcessInvoker.cs"), StringComparison.Ordinal);
        Assert.Contains("MatrixOptions", ReadMatrixCoreSource(repoRoot, "MatrixProofOptions.cs"), StringComparison.Ordinal);
        Assert.Contains("MatrixVerifyOptions", ReadMatrixCoreSource(repoRoot, "MatrixVerifyOptions.cs"), StringComparison.Ordinal);
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

    private static string ReadMatrixCoreSource(string repoRoot, string fileName)
    {
        return File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Matrix.Core", fileName));
    }
}
