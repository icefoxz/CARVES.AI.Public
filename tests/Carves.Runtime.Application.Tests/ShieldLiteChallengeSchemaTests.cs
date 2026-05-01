using System.Text.Json;
using Carves.Shield.Core;

namespace Carves.Runtime.Application.Tests;

public sealed class ShieldLiteChallengeSchemaTests
{
    [Fact]
    public void ShieldCore_ExposesStableChallengeContractConstants()
    {
        Assert.Equal("shield-lite-challenge.v0", ShieldLiteChallengeContract.SchemaVersion);
        Assert.Equal("shield-lite-challenge-result.v0", ShieldLiteChallengeContract.ResultSchemaVersion);
        Assert.Equal("docs/shield/schemas/shield-lite-challenge-v0.schema.json", ShieldLiteChallengeContract.SchemaPath);
        Assert.Equal("docs/shield/examples/shield-lite-challenge-suite.example.json", ShieldLiteChallengeContract.SuiteExamplePath);
        Assert.Equal("docs/shield/examples/shield-lite-starter-challenge-pack.example.json", ShieldLiteChallengeContract.StarterPackPath);
        Assert.Equal("docs/shield/examples/shield-lite-challenge-result.example.json", ShieldLiteChallengeContract.ResultExamplePath);
        Assert.Equal("scripts/shield/shield-lite-starter-challenge-smoke.ps1", ShieldLiteChallengeContract.StarterSmokeScriptPath);
        Assert.Equal("local challenge result, not certified safe", ShieldLiteChallengeContract.SummaryLabel);
        Assert.Equal(ExpectedChallengeKinds, ShieldLiteChallengeContract.RequiredChallengeKinds);
    }

    [Fact]
    public void ChallengeSchema_RequiresAllV0ChallengeKindsAndLocalOnlyOutputs()
    {
        var schemaPath = Path.Combine(ResolveRepoRoot(), ShieldLiteChallengeContract.SchemaPath);
        using var document = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var root = document.RootElement;

        Assert.Equal("https://carves.ai/schemas/shield-lite-challenge-v0.schema.json", root.GetProperty("$id").GetString());
        Assert.Equal("shield-lite-challenge.v0", root.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetString());
        Assert.True(root.GetProperty("properties").GetProperty("local_only").GetProperty("const").GetBoolean());
        Assert.False(root.GetProperty("properties").GetProperty("certification").GetProperty("const").GetBoolean());

        var challengeKindSchema = root
            .GetProperty("$defs")
            .GetProperty("challenge_case")
            .GetProperty("properties")
            .GetProperty("challenge_kind");
        var enumValues = challengeKindSchema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()).ToArray();
        Assert.Equal(ExpectedChallengeKinds, enumValues);

        var requiredContains = root
            .GetProperty("properties")
            .GetProperty("cases")
            .GetProperty("allOf")
            .EnumerateArray()
            .Select(item => item.GetProperty("contains").GetProperty("properties").GetProperty("challenge_kind").GetProperty("const").GetString())
            .ToArray();
        Assert.Equal(ExpectedChallengeKinds, requiredContains);

        var postureRequired = root
            .GetProperty("$defs")
            .GetProperty("challenge_case")
            .GetProperty("properties")
            .GetProperty("expected_local_decision_posture")
            .GetProperty("required")
            .EnumerateArray()
            .Select(value => value.GetString())
            .ToArray();
        Assert.Contains("decision", postureRequired);
        Assert.Contains("shield_lite_band_ceiling", postureRequired);
        Assert.Contains("reason_codes", postureRequired);

        var outputs = root
            .GetProperty("$defs")
            .GetProperty("challenge_case")
            .GetProperty("properties")
            .GetProperty("allowed_outputs")
            .GetProperty("properties");
        Assert.True(outputs.GetProperty("local_result").GetProperty("const").GetBoolean());
        Assert.False(outputs.GetProperty("certification").GetProperty("const").GetBoolean());
    }

    [Fact]
    public void ChallengeSuiteExample_CoversRequiredCasesWithExpectedPostureAndAllowedOutputs()
    {
        var examplePath = Path.Combine(ResolveRepoRoot(), ShieldLiteChallengeContract.SuiteExamplePath);
        using var document = JsonDocument.Parse(File.ReadAllText(examplePath));
        var root = document.RootElement;

        Assert.Equal(ShieldLiteChallengeContract.SchemaVersion, root.GetProperty("schema_version").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());

        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(ExpectedChallengeKinds, cases.Select(item => item.GetProperty("challenge_kind").GetString()).ToArray());
        foreach (var challengeCase in cases)
        {
            var posture = challengeCase.GetProperty("expected_local_decision_posture");
            Assert.False(string.IsNullOrWhiteSpace(posture.GetProperty("decision").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(posture.GetProperty("shield_lite_band_ceiling").GetString()));
            Assert.NotEmpty(posture.GetProperty("reason_codes").EnumerateArray());
            Assert.NotEmpty(posture.GetProperty("evidence_refs").EnumerateArray());

            var outputs = challengeCase.GetProperty("allowed_outputs");
            Assert.True(outputs.GetProperty("local_result").GetBoolean());
            Assert.False(outputs.GetProperty("certification").GetBoolean());
            Assert.NotEmpty(outputs.GetProperty("shareable_artifacts").EnumerateArray());
            Assert.Contains(
                outputs.GetProperty("forbidden_outputs").EnumerateArray(),
                output => string.Equals(output.GetString(), "certification", StringComparison.Ordinal));

            var privacy = challengeCase.GetProperty("privacy");
            Assert.False(privacy.GetProperty("source_included").GetBoolean());
            Assert.False(privacy.GetProperty("raw_diff_included").GetBoolean());
            Assert.False(privacy.GetProperty("prompt_included").GetBoolean());
            Assert.False(privacy.GetProperty("model_response_included").GetBoolean());
            Assert.False(privacy.GetProperty("secrets_included").GetBoolean());
            Assert.False(privacy.GetProperty("credentials_included").GetBoolean());
        }

        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            claim => string.Equals(claim.GetString(), "not certification", StringComparison.Ordinal));
    }

    [Fact]
    public void StarterChallengePack_ContainsTenBoundedLocalFixtures()
    {
        var starterPackPath = Path.Combine(ResolveRepoRoot(), ShieldLiteChallengeContract.StarterPackPath);
        using var document = JsonDocument.Parse(File.ReadAllText(starterPackPath));
        var root = document.RootElement;

        Assert.Equal(ShieldLiteChallengeContract.SchemaVersion, root.GetProperty("schema_version").GetString());
        Assert.Equal("shield-lite-starter-challenge-pack-v0", root.GetProperty("challenge_suite_id").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            claim => string.Equals(claim.GetString(), "not certified safe", StringComparison.Ordinal));

        var cases = root.GetProperty("cases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 10);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("case_id").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var kind in ExpectedChallengeKinds)
        {
            Assert.Contains(cases, item => string.Equals(item.GetProperty("challenge_kind").GetString(), kind, StringComparison.Ordinal));
        }

        foreach (var challengeCase in cases)
        {
            var kind = challengeCase.GetProperty("challenge_kind").GetString();
            var posture = challengeCase.GetProperty("expected_local_decision_posture");
            Assert.Contains(kind, posture.GetProperty("reason_codes").EnumerateArray().Select(item => item.GetString()));
            Assert.NotEmpty(posture.GetProperty("evidence_refs").EnumerateArray());

            var fixture = challengeCase.GetProperty("fixture");
            Assert.False(string.IsNullOrWhiteSpace(fixture.GetProperty("summary").GetString()));
            Assert.NotEmpty(fixture.GetProperty("mutations").EnumerateArray());

            var outputs = challengeCase.GetProperty("allowed_outputs");
            Assert.True(outputs.GetProperty("local_result").GetBoolean());
            Assert.False(outputs.GetProperty("certification").GetBoolean());
            Assert.NotEmpty(outputs.GetProperty("shareable_artifacts").EnumerateArray());
            Assert.Contains(
                outputs.GetProperty("forbidden_outputs").EnumerateArray(),
                output => string.Equals(output.GetString(), "certification", StringComparison.Ordinal));

            var privacy = challengeCase.GetProperty("privacy");
            foreach (var flag in PrivacyFlags)
            {
                Assert.False(privacy.GetProperty(flag).GetBoolean());
            }
        }
    }

    [Fact]
    public void ChallengeResultExample_IsLocalOnlyAndNotCertification()
    {
        var resultPath = Path.Combine(ResolveRepoRoot(), ShieldLiteChallengeContract.ResultExamplePath);
        using var document = JsonDocument.Parse(File.ReadAllText(resultPath));
        var root = document.RootElement;

        Assert.Equal(ShieldLiteChallengeContract.ResultSchemaVersion, root.GetProperty("schema_version").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.Equal(ShieldLiteChallengeContract.SummaryLabel, root.GetProperty("summary_label").GetString());
        Assert.Equal(7, root.GetProperty("results").GetArrayLength());
        Assert.All(
            root.GetProperty("results").EnumerateArray(),
            result => Assert.True(result.GetProperty("matched_expected_posture").GetBoolean()));
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            claim => string.Equals(claim.GetString(), "not certified safe", StringComparison.Ordinal));
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            claim => string.Equals(claim.GetString(), "not certification", StringComparison.Ordinal));
    }

    [Fact]
    public void ChallengeDocs_LinkSchemaExamplesAndMatrixLocalBoundary()
    {
        var repoRoot = ResolveRepoRoot();
        var doc = File.ReadAllText(Path.Combine(repoRoot, "docs", "shield", "lite-challenge-schema-v0.md"));
        var shieldReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "shield", "README.md"));
        var quickstart = File.ReadAllText(Path.Combine(repoRoot, "docs", "shield", "lite-challenge-quickstart.md"));
        var liteScoring = File.ReadAllText(Path.Combine(repoRoot, "docs", "shield", "lite-scoring-model-v0.md"));
        var matrixReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md"));
        var starterSmokeScript = File.ReadAllText(Path.Combine(repoRoot, ShieldLiteChallengeContract.StarterSmokeScriptPath));

        foreach (var kind in ExpectedChallengeKinds)
        {
            Assert.Contains(kind, doc, StringComparison.Ordinal);
            Assert.Contains(kind, liteScoring, StringComparison.Ordinal);
        }

        Assert.Contains(ShieldLiteChallengeContract.SchemaPath, doc, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.SuiteExamplePath, doc, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.StarterPackPath, doc, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.ResultExamplePath, doc, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.SummaryLabel, doc, StringComparison.Ordinal);
        Assert.Contains("local challenge results", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not certification", doc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Shield Lite Challenge Schema v0", shieldReadme, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.StarterPackPath, shieldReadme, StringComparison.Ordinal);
        Assert.Contains("Shield Lite Starter Challenge Quickstart", shieldReadme, StringComparison.Ordinal);
        Assert.Contains("challenge results are local challenge results, not certification", shieldReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ShieldLiteChallengeContract.StarterPackPath, quickstart, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.StarterSmokeScriptPath, quickstart, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.SummaryLabel, quickstart, StringComparison.Ordinal);
        Assert.Contains("challenge results remain local challenge results", liteScoring, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Matrix may record challenge suite or challenge result artifacts", matrixReadme, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.StarterPackPath, matrixReadme, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.SummaryLabel, matrixReadme, StringComparison.Ordinal);
        Assert.Contains("does not turn challenge results into certification", matrixReadme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ShieldLiteChallengeContract.StarterPackPath, starterSmokeScript, StringComparison.Ordinal);
        Assert.Contains(ShieldLiteChallengeContract.SummaryLabel, starterSmokeScript, StringComparison.Ordinal);
    }

    private static readonly string[] ExpectedChallengeKinds =
    [
        "protected_path_violation",
        "deletion_without_credible_replacement",
        "fake_audit_evidence",
        "stale_handoff_packet",
        "privacy_leakage_flag",
        "missing_ci_evidence",
        "oversized_patch",
    ];

    private static readonly string[] PrivacyFlags =
    [
        "source_included",
        "raw_diff_included",
        "prompt_included",
        "model_response_included",
        "secrets_included",
        "credentials_included",
    ];

    private static string ResolveRepoRoot()
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

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root.");
    }
}
