using System.Diagnostics;
using System.Text.Json;
using Carves.Shield.Core;

namespace Carves.Runtime.IntegrationTests;

public sealed class ShieldExtractionShellTests
{
    [Fact]
    public void ShieldExtractionProjects_ExistAndRuntimeWrapperDelegates()
    {
        var repoRoot = LocateSourceRepoRoot();
        var shieldCoreProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Shield.Core", "Carves.Shield.Core.csproj"));
        var shieldCliProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Shield.Cli", "Carves.Shield.Cli.csproj"));
        var runtimeProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "carves.csproj"));
        var runtimeWrapper = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Shield.cs"));
        var shieldProof = File.ReadAllText(Path.Combine(repoRoot, "scripts", "shield", "shield-github-actions-proof.ps1"));
        var publishReadiness = File.ReadAllText(Path.Combine(repoRoot, "scripts", "release", "github-publish-readiness.ps1"));

        Assert.Contains("<PackageId>CARVES.Shield.Core</PackageId>", shieldCoreProject, StringComparison.Ordinal);
        Assert.Contains("<PackageId>CARVES.Shield.Cli</PackageId>", shieldCliProject, StringComparison.Ordinal);
        Assert.Contains("<ToolCommandName>carves-shield</ToolCommandName>", shieldCliProject, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Core", runtimeProject, StringComparison.Ordinal);
        Assert.Contains("ShieldCliRunner.Run", runtimeWrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldEvaluationService", runtimeWrapper, StringComparison.Ordinal);
        Assert.Contains("src/CARVES.Shield.Cli/Carves.Shield.Cli.csproj", shieldProof, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Core", publishReadiness, StringComparison.Ordinal);
        Assert.Contains("CARVES.Shield.Cli", publishReadiness, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneShieldRunner_EvaluatesAndWritesBadgeWithoutRuntimeTaskTruth()
    {
        using var repo = ShieldCliSandbox.Create();
        repo.WriteFile("shield-evidence.json", ValidEvidence());

        var evaluate = RunShieldInDirectory(repo.RootPath, "evaluate", "shield-evidence.json", "--json");
        var badge = RunShieldInDirectory(repo.RootPath, "badge", "shield-evidence.json", "--json", "--output", "docs/shield-badge.svg");

        Assert.Equal(0, evaluate.ExitCode);
        using var evaluateDocument = JsonDocument.Parse(evaluate.StandardOutput);
        Assert.Equal("shield-evaluate.v0", evaluateDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("ok", evaluateDocument.RootElement.GetProperty("status").GetString());
        Assert.False(evaluateDocument.RootElement.GetProperty("certification").GetBoolean());

        Assert.Equal(0, badge.ExitCode);
        using var badgeDocument = JsonDocument.Parse(badge.StandardOutput);
        Assert.Equal("shield-badge.v0", badgeDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("ok", badgeDocument.RootElement.GetProperty("status").GetString());
        Assert.True(File.Exists(Path.Combine(repo.RootPath, "docs", "shield-badge.svg")));
    }

    private static CliResult RunShieldInDirectory(string workingDirectory, params string[] arguments)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = ShieldCliRunner.Run(arguments);
            return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static string ValidEvidence()
    {
        return """
        {
          "schema_version": "shield-evidence.v0",
          "evidence_id": "shield-extraction-evidence",
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
                "effective_protected_path_prefixes": [ ".git/" ],
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
                "workflow_paths": [ ".github/workflows/carves-guard.yml" ],
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
              "proofs": []
            },
            "handoff": { "enabled": false },
            "audit": { "enabled": false }
          },
          "provenance": {
            "producer": "carves-shield",
            "producer_version": "0.1.0-alpha.1",
            "generated_by": "local",
            "source": "shield-extraction-test",
            "warnings": []
          }
        }
        """;
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

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class ShieldCliSandbox : IDisposable
    {
        private ShieldCliSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static ShieldCliSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "carves-shield-extraction-tests", Guid.NewGuid().ToString("N"));
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
