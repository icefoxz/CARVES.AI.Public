using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Shield.Core;

namespace Carves.Runtime.IntegrationTests;

public sealed class ShieldChallengeCliTests
{
    [Fact]
    public void ShieldChallengeJson_RunsLocalPackAndEmitsCaseBreakdown()
    {
        using var repo = ShieldChallengeSandbox.Create();
        repo.WriteFile("challenge.json", File.ReadAllText(Path.Combine(LocateSourceRepoRoot(), ShieldLiteChallengeContract.SuiteExamplePath)));

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "challenge", "challenge.json", "--json");

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(repo.RootPath, "shield-evaluate.json")));
        Assert.False(File.Exists(Path.Combine(repo.RootPath, "shield-badge.svg")));
        Assert.False(File.Exists(Path.Combine(repo.RootPath, "challenge-result.json")));

        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(ShieldLiteChallengeContract.ResultSchemaVersion, root.GetProperty("schema_version").GetString());
        Assert.Equal("passed", root.GetProperty("status").GetString());
        Assert.Equal(ShieldLiteChallengeContract.SummaryLabel, root.GetProperty("summary_label").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.Equal(7, root.GetProperty("case_count").GetInt32());
        Assert.Equal(7, root.GetProperty("passed_count").GetInt32());
        Assert.Equal(0, root.GetProperty("failed_count").GetInt32());
        Assert.Equal(1, root.GetProperty("pass_rate").GetDouble());
        Assert.Contains(ShieldLiteChallengeContract.SummaryLabel, result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("\"certification\": true", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            root.GetProperty("non_claims").EnumerateArray(),
            claim => string.Equals(claim.GetString(), "not certified safe", StringComparison.Ordinal));

        var results = root.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(ShieldLiteChallengeContract.RequiredChallengeKinds, results.Select(item => item.GetProperty("challenge_kind").GetString()!).ToArray());
        Assert.All(results, item =>
        {
            Assert.Equal("passed", item.GetProperty("status").GetString());
            Assert.True(item.GetProperty("passed").GetBoolean());
            Assert.NotEmpty(item.GetProperty("reason_codes").EnumerateArray());
            Assert.NotEmpty(item.GetProperty("shareable_artifacts").EnumerateArray());
            Assert.Empty(item.GetProperty("issues").EnumerateArray());
        });
    }

    [Fact]
    public void ShieldChallengeJson_ReportsFailingCaseWithoutPositiveClaims()
    {
        using var repo = ShieldChallengeSandbox.Create();
        var pack = JsonNode.Parse(File.ReadAllText(Path.Combine(LocateSourceRepoRoot(), ShieldLiteChallengeContract.SuiteExamplePath)))!.AsObject();
        var firstCase = pack["cases"]!.AsArray()[0]!.AsObject();
        var posture = firstCase["expected_local_decision_posture"]!.AsObject();
        posture["decision"] = "allow";
        repo.WriteFile("challenge.json", pack.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "challenge", "challenge.json", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("failed", root.GetProperty("status").GetString());
        Assert.Equal(ShieldLiteChallengeContract.SummaryLabel, root.GetProperty("summary_label").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.Equal(6, root.GetProperty("passed_count").GetInt32());
        Assert.Equal(1, root.GetProperty("failed_count").GetInt32());
        Assert.True(root.GetProperty("pass_rate").GetDouble() < 1);
        Assert.Contains(ShieldLiteChallengeContract.SummaryLabel, result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("\"certification\": true", result.StandardOutput, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            root.GetProperty("results").EnumerateArray(),
            item => item.GetProperty("challenge_kind").GetString() == "protected_path_violation"
                    && !item.GetProperty("passed").GetBoolean()
                    && item.GetProperty("expected_decision").GetString() == "block"
                    && item.GetProperty("observed_decision").GetString() == "allow"
                    && item.GetProperty("issues").EnumerateArray().Any(issue => issue.GetString() == "decision_posture_mismatch"));
    }

    [Fact]
    public void ShieldChallengeText_PrintsPassRateAndNoPositiveCertificationLanguage()
    {
        using var repo = ShieldChallengeSandbox.Create();
        repo.WriteFile("challenge.json", File.ReadAllText(Path.Combine(LocateSourceRepoRoot(), ShieldLiteChallengeContract.SuiteExamplePath)));

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "challenge", "challenge.json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES Shield challenge", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Status: passed", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains($"Summary label: {ShieldLiteChallengeContract.SummaryLabel}", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Cases: 7/7 passed", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Pass rate: 1", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Public claims: none", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("not certified safe", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Certification: true", result.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShieldChallengeJson_RunsStarterPackWithAtLeastTenBoundedFixtures()
    {
        using var repo = ShieldChallengeSandbox.Create();
        var sourceRepoRoot = LocateSourceRepoRoot();
        var starterPackPath = Path.Combine(sourceRepoRoot, ShieldLiteChallengeContract.StarterPackPath);
        repo.WriteFile("starter.json", File.ReadAllText(starterPackPath));

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "challenge", "starter.json", "--json");

        Assert.Equal(0, result.ExitCode);
        using var resultDocument = JsonDocument.Parse(result.StandardOutput);
        var root = resultDocument.RootElement;
        var caseCount = root.GetProperty("case_count").GetInt32();
        Assert.Equal(ShieldLiteChallengeContract.ResultSchemaVersion, root.GetProperty("schema_version").GetString());
        Assert.Equal("passed", root.GetProperty("status").GetString());
        Assert.Equal(ShieldLiteChallengeContract.SummaryLabel, root.GetProperty("summary_label").GetString());
        Assert.True(root.GetProperty("local_only").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.True(caseCount >= 10);
        Assert.Equal(caseCount, root.GetProperty("passed_count").GetInt32());
        Assert.Equal(0, root.GetProperty("failed_count").GetInt32());

        Assert.All(
            root.GetProperty("results").EnumerateArray(),
            item =>
            {
                Assert.Equal("passed", item.GetProperty("status").GetString());
                Assert.True(item.GetProperty("passed").GetBoolean());
                Assert.NotEmpty(item.GetProperty("shareable_artifacts").EnumerateArray());
                Assert.Empty(item.GetProperty("issues").EnumerateArray());
            });

        using var packDocument = JsonDocument.Parse(File.ReadAllText(starterPackPath));
        var packRoot = packDocument.RootElement;
        Assert.Equal(ShieldLiteChallengeContract.SchemaVersion, packRoot.GetProperty("schema_version").GetString());
        Assert.True(packRoot.GetProperty("local_only").GetBoolean());
        Assert.False(packRoot.GetProperty("certification").GetBoolean());
        Assert.Contains(
            packRoot.GetProperty("non_claims").EnumerateArray(),
            claim => string.Equals(claim.GetString(), "not certified safe", StringComparison.Ordinal));

        var cases = packRoot.GetProperty("cases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 10);
        Assert.Equal(cases.Length, caseCount);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("case_id").GetString()).Distinct(StringComparer.Ordinal).Count());
        foreach (var requiredKind in ShieldLiteChallengeContract.RequiredChallengeKinds)
        {
            Assert.Contains(cases, item => string.Equals(item.GetProperty("challenge_kind").GetString(), requiredKind, StringComparison.Ordinal));
        }

        foreach (var challengeCase in cases)
        {
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

    private static readonly string[] PrivacyFlags =
    [
        "source_included",
        "raw_diff_included",
        "prompt_included",
        "model_response_included",
        "secrets_included",
        "credentials_included",
    ];

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

    private sealed class ShieldChallengeSandbox : IDisposable
    {
        private ShieldChallengeSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static ShieldChallengeSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "carves-shield-challenge-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var sandbox = new ShieldChallengeSandbox(root);
            sandbox.RunGit("init");
            return sandbox;
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
            }
        }

        private void RunGit(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = RootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stdout}{stderr}");
            }
        }
    }
}
