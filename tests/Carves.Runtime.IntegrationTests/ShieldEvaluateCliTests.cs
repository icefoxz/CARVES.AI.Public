using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class ShieldEvaluateCliTests
{
    [Fact]
    public void ShieldEvaluateJson_EmitsCombinedLocalSelfCheck()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", ValidEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "evaluate", "shield-evidence.json", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("shield-evaluate.v0", root.GetProperty("schema_version").GetString());
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.Equal(
            ComputeFileSha256(Path.Combine(repo.RootPath, "shield-evidence.json")),
            root.GetProperty("consumed_evidence_sha256").GetString());
        Assert.Equal("CARVES G8.H0.A0 /30d PASS", root.GetProperty("standard").GetProperty("label").GetString());
        Assert.Equal(36, root.GetProperty("lite").GetProperty("score").GetInt32());
        Assert.Equal("basic", root.GetProperty("lite").GetProperty("band").GetString());
    }

    [Fact]
    public void ShieldEvaluateText_EmitsLiteOnlyValidResult()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", ValidEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "evaluate", "shield-evidence.json", "--output", "lite");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("CARVES Shield evaluate", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Status: ok", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Evidence SHA-256:", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Lite: 36/100 (basic)", result.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Standard:", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldEvaluateJson_ReturnsInvalidPrivacyPosture()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", InvalidPrivacyEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "evaluate", "shield-evidence.json", "--json");

        Assert.Equal(1, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("invalid_privacy_posture", root.GetProperty("status").GetString());
        Assert.Contains(root.GetProperty("errors").EnumerateArray(), error =>
            error.GetProperty("evidence_refs").EnumerateArray().Any(item => item.GetString() == "privacy.raw_diff_included"));
    }

    [Fact]
    public void ShieldEvaluateText_ReturnsInvalidPrivacyPosture()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", InvalidPrivacyEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "evaluate", "shield-evidence.json");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Status: invalid_privacy_posture", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("privacy.raw_diff_included", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldBadgeStdout_EmitsStaticSvg()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", ValidEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "badge", "shield-evidence.json");

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("<svg", result.StandardOutput.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("CARVES Shield", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("36/100 Basic", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("self-check", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldBadgeOutputFile_WritesStaticSvg()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", ValidEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "badge", "shield-evidence.json", "--output", "docs/shield-badge.svg");

        Assert.Equal(0, result.ExitCode);
        var badgePath = Path.Combine(repo.RootPath, "docs", "shield-badge.svg");
        Assert.True(File.Exists(badgePath));
        Assert.Contains("<svg", File.ReadAllText(badgePath), StringComparison.Ordinal);
        Assert.Contains("Wrote: docs/shield-badge.svg", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldBadgeJson_EmitsMetadata()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", ValidEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "badge", "shield-evidence.json", "--json");

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("shield-badge.v0", root.GetProperty("schema_version").GetString());
        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.True(root.GetProperty("self_check").GetBoolean());
        Assert.False(root.GetProperty("certification").GetBoolean());
        Assert.Equal("36/100 Basic", root.GetProperty("badge").GetProperty("message").GetString());
        Assert.Equal("white", root.GetProperty("badge").GetProperty("color_name").GetString());
        Assert.Equal("G8.H0.A0", root.GetProperty("badge").GetProperty("standard_compact").GetString());
        Assert.Equal(
            ComputeFileSha256(Path.Combine(repo.RootPath, "shield-evidence.json")),
            root.GetProperty("evaluation").GetProperty("consumed_evidence_sha256").GetString());
        Assert.Contains("<svg", root.GetProperty("badge").GetProperty("svg").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ShieldBadgeInvalidEvidence_ReturnsNonZeroAndDoesNotWriteBadge()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", InvalidPrivacyEvidence());

        var result = CliProgramHarness.RunInDirectory(repo.RootPath, "shield", "badge", "shield-evidence.json", "--output", "shield-badge.svg");

        Assert.Equal(1, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(repo.RootPath, "shield-badge.svg")));
        Assert.Contains("Status: invalid_privacy_posture", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("privacy.raw_diff_included", result.StandardOutput, StringComparison.Ordinal);
    }

    private static string InvalidPrivacyEvidence()
    {
        return ValidEvidence().Replace(
            "\"raw_diff_included\": false",
            "\"raw_diff_included\": true",
            StringComparison.Ordinal);
    }

    private static string ValidEvidence()
    {
        return """
        {
          "schema_version": "shield-evidence.v0",
          "evidence_id": "cli-shield-evidence",
          "generated_at_utc": "2026-04-14T10:45:00Z",
          "mode_hint": "both",
          "sample_window_days": 30,
          "repository": {
            "host": "github",
            "visibility": "public"
          },
          "privacy": {
            "source_included": false,
            "raw_diff_included": false,
            "prompt_included": false,
            "secrets_included": false,
            "redaction_applied": true,
            "upload_intent": "local_only"
          },
          "dimensions": {
            "guard": {
              "enabled": true,
              "policy": {
                "present": true,
                "schema_valid": true,
                "effective_protected_path_prefixes": [
                  ".git/",
                  ".env"
                ],
                "protected_path_action": "block",
                "outside_allowed_action": "review",
                "fail_closed": true,
                "change_budget_present": true,
                "dependency_policy_present": true,
                "source_test_rule_present": true,
                "mixed_feature_refactor_rule_present": true
              },
              "ci": {
                "detected": true,
                "workflow_paths": [
                  ".github/workflows/carves-guard.yml"
                ],
                "guard_check_command_detected": true,
                "fails_on_review_or_block": true
              },
              "decisions": {
                "present": true,
                "window_days": 30,
                "allow_count": 1,
                "review_count": 0,
                "block_count": 0,
                "unresolved_review_count": 0,
                "unresolved_block_count": 0
              },
              "proofs": [
                {
                  "kind": "ci_workflow",
                  "ref": ".github/workflows/carves-guard.yml"
                }
              ]
            },
            "handoff": {
              "enabled": false
            },
            "audit": {
              "enabled": false
            }
          },
          "provenance": {
            "producer": "carves-cli",
            "producer_version": "0.2.0-beta.1",
            "generated_by": "local",
            "source": "cli-test",
            "warnings": []
          }
        }
        """;
    }

    private static string ComputeFileSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class ShieldCliSandbox : IDisposable
    {
        private ShieldCliSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static ShieldCliSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "carves-shield-cli-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var sandbox = new ShieldCliSandbox(root);
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
