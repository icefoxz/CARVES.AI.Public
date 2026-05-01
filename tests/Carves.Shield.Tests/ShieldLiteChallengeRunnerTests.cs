using Carves.Shield.Core;
using System.Text.Json.Nodes;

namespace Carves.Shield.Tests;

public sealed class ShieldLiteChallengeRunnerTests
{
    [Fact]
    public void RunFile_ValidPackPassesRequiredCasesAndKeepsNonClaims()
    {
        using var fixture = ShieldChallengePackFixture.CreateValid();

        var result = RunChallenge(fixture);

        Assert.True(result.IsPassed);
        Assert.Equal("passed", result.Status);
        Assert.Equal(ShieldLiteChallengeContract.ResultSchemaVersion, result.SchemaVersion);
        Assert.Equal("shield-lite-direct-runner-tests", result.ChallengeSuiteId);
        Assert.Equal(ShieldLiteChallengeContract.SummaryLabel, result.SummaryLabel);
        Assert.True(result.LocalOnly);
        Assert.False(result.Certification);
        Assert.Equal(ShieldLiteChallengeContract.RequiredChallengeKinds.Count, result.CaseCount);
        Assert.Equal(result.CaseCount, result.PassedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(1, result.PassRate);
        Assert.Equal(
            ShieldLiteChallengeContract.RequiredChallengeKinds,
            result.Results.Select(item => item.ChallengeKind).ToArray());
        Assert.Contains("not certified safe", result.NonClaims);
        Assert.Contains("not certification", result.NonClaims);
        Assert.DoesNotContain(result.NonClaims, claim => claim.Contains("certified", StringComparison.OrdinalIgnoreCase) && claim != "not certified safe");
        Assert.All(result.Results, item =>
        {
            Assert.True(item.Passed);
            Assert.Equal("passed", item.Status);
            Assert.NotEmpty(item.ReasonCodes);
            Assert.NotEmpty(item.EvidenceRefs);
            Assert.NotEmpty(item.ShareableArtifacts);
            Assert.Empty(item.Issues);
        });
    }

    [Fact]
    public void RunFile_MissingPackReturnsInvalidPackResult()
    {
        using var workspace = TempWorkspace.Create();

        var result = new ShieldLiteChallengeRunner().RunFile(workspace.RootPath, "missing-pack.json");

        Assert.False(result.IsPassed);
        Assert.Equal("failed", result.Status);
        Assert.Equal(1, result.CaseCount);
        Assert.Equal(0, result.PassedCount);
        Assert.Equal(1, result.FailedCount);
        var suite = Assert.Single(result.Results);
        Assert.Equal("suite", suite.CaseId);
        Assert.Contains("challenge_pack_missing", suite.Issues);
        Assert.Contains("not certified safe", result.NonClaims);
    }

    [Fact]
    public void RunFile_InvalidJsonReturnsInvalidPackResult()
    {
        using var workspace = TempWorkspace.Create();
        workspace.WriteFile("challenge.json", "{");

        var result = new ShieldLiteChallengeRunner().RunFile(workspace.RootPath, "challenge.json");

        Assert.False(result.IsPassed);
        var suite = Assert.Single(result.Results);
        Assert.Equal("suite", suite.CaseId);
        Assert.Contains("challenge_pack_invalid_json", suite.Issues);
    }

    [Fact]
    public void RunFile_SuiteMetadataMismatchAddsSuiteFailureWithoutChangingCasePosture()
    {
        using var fixture = ShieldChallengePackFixture.CreateValid();
        fixture.SetString("schema_version", "shield-lite-challenge.v999");
        fixture.SetString("mode", "hosted_challenge");
        fixture.SetBool("local_only", false);
        fixture.SetBool("certification", true);
        fixture.Write();

        var result = RunChallenge(fixture);

        Assert.False(result.IsPassed);
        Assert.Equal(ShieldLiteChallengeContract.RequiredChallengeKinds.Count + 1, result.CaseCount);
        Assert.Equal(ShieldLiteChallengeContract.RequiredChallengeKinds.Count, result.PassedCount);
        Assert.Equal(1, result.FailedCount);
        var suite = Assert.Single(result.Results, item => item.CaseId == "suite");
        Assert.Contains("schema_version_mismatch", suite.Issues);
        Assert.Contains("mode_mismatch", suite.Issues);
        Assert.Contains("local_only_mismatch", suite.Issues);
        Assert.Contains("certification_mismatch", suite.Issues);
        Assert.All(result.Results.Where(item => item.CaseId != "suite"), item => Assert.True(item.Passed));
    }

    [Fact]
    public void RunFile_MissingRequiredChallengeKindAddsSyntheticFailure()
    {
        using var fixture = ShieldChallengePackFixture.CreateValid();
        fixture.RemoveCase("oversized_patch");
        fixture.Write();

        var result = RunChallenge(fixture);

        Assert.False(result.IsPassed);
        Assert.Equal(ShieldLiteChallengeContract.RequiredChallengeKinds.Count, result.CaseCount);
        Assert.Equal(ShieldLiteChallengeContract.RequiredChallengeKinds.Count - 1, result.PassedCount);
        Assert.Equal(1, result.FailedCount);
        var missing = Assert.Single(result.Results, item => item.CaseId == "missing:oversized_patch");
        Assert.Equal("oversized_patch", missing.ChallengeKind);
        Assert.False(missing.Passed);
        Assert.Equal("review", missing.ExpectedDecision);
        Assert.Equal("basic", missing.ShieldLiteBandCeiling);
        Assert.Contains("missing_required_challenge_kind", missing.Issues);
        Assert.Contains("oversized_patch", missing.ReasonCodes);
    }

    [Fact]
    public void RunFile_CasePostureMismatchReportsDirectCaseIssues()
    {
        using var fixture = ShieldChallengePackFixture.CreateValid();
        var posture = fixture.FindCase("protected_path_violation")["expected_local_decision_posture"]!.AsObject();
        posture["decision"] = "allow";
        posture["shield_lite_band_ceiling"] = "basic";
        posture["reason_codes"] = new JsonArray();
        posture["evidence_refs"] = new JsonArray();
        fixture.Write();

        var result = RunChallenge(fixture);

        Assert.False(result.IsPassed);
        var failed = Assert.Single(result.Results, item => item.ChallengeKind == "protected_path_violation");
        Assert.False(failed.Passed);
        Assert.Equal("block", failed.ExpectedDecision);
        Assert.Equal("allow", failed.ObservedDecision);
        Assert.Equal("basic", failed.ShieldLiteBandCeiling);
        Assert.Contains("decision_posture_mismatch", failed.Issues);
        Assert.Contains("band_ceiling_mismatch", failed.Issues);
        Assert.Contains("reason_code_missing", failed.Issues);
        Assert.Contains("evidence_refs_missing", failed.Issues);
    }

    [Fact]
    public void RunFile_PrivacyAndAllowedOutputFailuresReportDirectCaseIssues()
    {
        using var fixture = ShieldChallengePackFixture.CreateValid();
        var challengeCase = fixture.FindCase("missing_ci_evidence");
        var outputs = challengeCase["allowed_outputs"]!.AsObject();
        outputs["local_result"] = false;
        outputs["certification"] = true;
        outputs["shareable_artifacts"] = new JsonArray();
        outputs["forbidden_outputs"] = new JsonArray("source_code");
        challengeCase["privacy"]!.AsObject()["source_included"] = true;
        fixture.Write();

        var result = RunChallenge(fixture);

        Assert.False(result.IsPassed);
        var failed = Assert.Single(result.Results, item => item.ChallengeKind == "missing_ci_evidence");
        Assert.Contains("local_result_not_true", failed.Issues);
        Assert.Contains("allowed_outputs_certification_not_false", failed.Issues);
        Assert.Contains("shareable_artifacts_missing", failed.Issues);
        Assert.Contains("certification_forbidden_output_missing", failed.Issues);
        Assert.Contains("privacy_flag_not_false:source_included", failed.Issues);
    }

    [Fact]
    public void RunFile_DuplicateCaseIdAndUnknownKindAreReported()
    {
        using var fixture = ShieldChallengePackFixture.CreateValid();
        var duplicate = fixture.CloneCase("protected_path_violation");
        duplicate["challenge_kind"] = "unknown_kind";
        fixture.AddCase(duplicate);
        fixture.Write();

        var result = RunChallenge(fixture);

        Assert.False(result.IsPassed);
        var failed = Assert.Single(result.Results, item => item.ChallengeKind == "unknown_kind");
        Assert.Contains("duplicate_case_id", failed.Issues);
        Assert.Contains("unknown_challenge_kind", failed.Issues);
    }

    [Fact]
    public void RunnerSource_DoesNotInvokePowerShellOrChallengeSmokeScripts()
    {
        var repoRoot = LocateSourceRepoRoot();
        var runner = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Shield.Core", "ShieldLiteChallengeRunner.cs"));

        Assert.Contains("public ShieldLiteChallengeRunResult RunFile", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessStartInfo", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("pwsh", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".ps1", runner, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shield-lite-starter-challenge-smoke", runner, StringComparison.Ordinal);
    }

    private static ShieldLiteChallengeRunResult RunChallenge(ShieldChallengePackFixture fixture)
    {
        return new ShieldLiteChallengeRunner().RunFile(fixture.RootPath, fixture.RelativePath);
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
