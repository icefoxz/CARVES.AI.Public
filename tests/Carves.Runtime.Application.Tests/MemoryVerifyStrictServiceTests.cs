using System.Text.Json;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Tests;

public sealed class MemoryVerifyStrictServiceTests
{
    [Fact]
    public void StrictVerify_PassesAgainstCurrentRepoArtifacts()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var service = new MemoryVerifyStrictService(repoRoot);

        var report = service.RunStrict();

        Assert.True(report.Passed);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal("memory_verify_result.v1", report.SchemaVersion);
        Assert.Equal("carves memory verify --strict --json", report.VerifyCommand);
        Assert.DoesNotContain(report.Checks, check => check.Severity == "critical" && check.Status == "fail");
    }

    [Fact]
    public void StrictVerify_FailsWhenRequiredArtifactsAreMissing()
    {
        using var workspace = new TemporaryWorkspace();
        var service = new MemoryVerifyStrictService(workspace.RootPath);

        var report = service.RunStrict();

        Assert.False(report.Passed);
        Assert.Equal(1, report.ExitCode);
        Assert.Contains(report.Checks, check => check.Severity == "critical" && check.Status == "fail");
    }

    [Fact]
    public void ReleaseGateDocument_MatchesRuntimeVerifyContract()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var service = new MemoryVerifyStrictService(repoRoot);
        var json = File.ReadAllText(Path.Combine(repoRoot, "docs", "runtime", "memory-gate", "MEMORY_M0_RELEASE_GATE_V1.json"));
        using var document = JsonDocument.Parse(json);

        var artifacts = document.RootElement.GetProperty("required_artifacts")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        var checks = document.RootElement.GetProperty("critical_checks")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();

        Assert.Equal("memory-m0-release-gate.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal("carves memory verify --strict --json", document.RootElement.GetProperty("verify_command").GetString());
        Assert.Equal(service.GetRequiredArtifacts(), artifacts);
        Assert.Equal(service.GetCriticalChecks(), checks);
    }
}
