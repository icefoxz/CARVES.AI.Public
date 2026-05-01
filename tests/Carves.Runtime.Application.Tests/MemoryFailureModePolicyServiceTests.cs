using System.Text.Json;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Application.Tests;

public sealed class MemoryFailureModePolicyServiceTests
{
    [Fact]
    public void FailurePolicies_HaveFixedModeBehaviorsForCriticalCases()
    {
        var service = new MemoryFailureModePolicyService();

        Assert.Equal("block", service.ResolveFailureBehavior("canonical_fact_without_evidence", MemoryFailureMode.Strict));
        Assert.Equal("exclude", service.ResolveFailureBehavior("canonical_fact_without_evidence", MemoryFailureMode.Search));
        Assert.Equal("blocker", service.ResolveFailureBehavior("canonical_fact_without_evidence", MemoryFailureMode.Release));
        Assert.Equal("degraded", service.ResolveFailureBehavior("active_view_build_failed", MemoryFailureMode.Normal));
        Assert.Equal("fallback_lexical_only", service.ResolveFailureBehavior("active_view_build_failed", MemoryFailureMode.Search));
        Assert.Equal("non_blocking", service.ResolveFailureBehavior("stale_codegraph_low_risk_task", MemoryFailureMode.Release));
    }

    [Fact]
    public void CommandMatrix_ResolvesStableDefaultModesAndFailureSemantics()
    {
        var service = new MemoryFailureModePolicyService();

        var verify = service.GetCommandMode("carves memory verify --strict");
        var search = service.GetCommandMode("carves memory search");
        var show = service.GetCommandMode("carves context show");
        var preflight = service.GetCommandMode("writeback preflight");

        Assert.Equal(MemoryFailureMode.Strict, verify.DefaultMode);
        Assert.Equal("exit_nonzero", verify.CriticalFailureBehavior);
        Assert.Equal(1, verify.ExitCode);
        Assert.Contains("--json", verify.Flags);
        Assert.Equal(MemoryFailureMode.Search, search.DefaultMode);
        Assert.Equal("exclude_unsafe_results_with_reason", search.CriticalFailureBehavior);
        Assert.Equal(MemoryFailureMode.Normal, show.DefaultMode);
        Assert.Equal("degraded_or_block_if_task_writeback_sensitive", show.CriticalFailureBehavior);
        Assert.Equal("memory.write.blocked", preflight.TelemetryEvent);
    }

    [Fact]
    public void FailurePolicyDocument_MatchesRuntimePolicyTable()
    {
        var service = new MemoryFailureModePolicyService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "MEMORY_FAILURE_MODE_POLICY_V1.json")));
        using var document = JsonDocument.Parse(json);

        var policies = document.RootElement.GetProperty("failure_policies")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("failure_code").GetString()!,
                item => item,
                StringComparer.Ordinal);

        Assert.Equal("memory-failure-mode-policy.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal(service.GetFailurePolicies().Count, policies.Count);

        foreach (var entry in service.GetFailurePolicies())
        {
            var policy = policies[entry.FailureCode];
            Assert.Equal(entry.Strict, policy.GetProperty("strict").GetString());
            Assert.Equal(entry.Normal, policy.GetProperty("normal").GetString());
            Assert.Equal(entry.Search, policy.GetProperty("search").GetString());
            Assert.Equal(entry.Release, policy.GetProperty("release").GetString());
        }
    }

    [Fact]
    public void CommandMatrixDocument_MatchesRuntimeCommandModeTable()
    {
        var service = new MemoryFailureModePolicyService();
        var json = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "docs", "runtime", "memory-gate", "COMMAND_FAILURE_MODE_MATRIX_V1.json")));
        using var document = JsonDocument.Parse(json);

        var commands = document.RootElement.GetProperty("commands")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("command").GetString()!,
                item => item,
                StringComparer.Ordinal);

        Assert.Equal("command-failure-mode-matrix.v1", document.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("memory-gate.v1", document.RootElement.GetProperty("policy_version").GetString());
        Assert.Equal(service.GetCommandModeMatrix().Count, commands.Count);

        foreach (var entry in service.GetCommandModeMatrix())
        {
            var command = commands[entry.Command];
            Assert.Equal(entry.DefaultMode.ToString().ToLowerInvariant(), command.GetProperty("default_mode").GetString());
            Assert.Equal(entry.CriticalFailureBehavior, command.GetProperty("critical_failure_behavior").GetString());
            Assert.Equal(entry.ExitCode, command.GetProperty("exit_code").GetInt32());
            Assert.Equal(entry.JsonErrorShape, command.GetProperty("json_error_shape").GetString());
            Assert.Equal(entry.TelemetryEvent, command.GetProperty("telemetry_event").GetString());

            var flags = command.GetProperty("flags")
                .EnumerateArray()
                .Select(flag => flag.GetString()!)
                .ToArray();
            Assert.Equal(entry.Flags, flags);
        }
    }
}
