using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeGovernanceSurfaceCoverageAuditHostContractTests
{
    [Fact]
    public void InspectAndApiRuntimeGovernanceSurfaceCoverageAudit_ProjectBoundedCoverageEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-governance-surface-coverage-audit");
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-governance-surface-coverage-audit");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime governance surface coverage audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Coverage complete: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Lifecycle budget complete: True", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Default-path surfaces: 2/2", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-agent-short-context", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-worker-execution-audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("runtime-brokered-execution", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("does not prove behavior correctness", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-governance-surface-coverage-audit", root.GetProperty("surface_id").GetString());
        Assert.True(root.GetProperty("coverage_complete").GetBoolean());
        Assert.True(root.GetProperty("lifecycle_budget_complete").GetBoolean());
        Assert.Equal(0, root.GetProperty("blocking_gap_count").GetInt32());
        Assert.Equal(2, root.GetProperty("default_path_surface_count").GetInt32());
        Assert.Contains(root.GetProperty("coverage_dimensions").EnumerateArray(), item =>
            item.GetString() == "host_contract_test_evidence");
        Assert.Contains(root.GetProperty("coverage_dimensions").EnumerateArray(), item =>
            item.GetString() == "read_path_budget_class");
        Assert.Contains(root.GetProperty("required_surfaces").EnumerateArray(), item =>
            item.GetString() == "runtime-workspace-mutation-audit");
        Assert.Contains(root.GetProperty("entries").EnumerateArray(), entry =>
            entry.GetProperty("surface_id").GetString() == "runtime-worker-execution-audit"
            && entry.GetProperty("coverage_status").GetString() == "covered"
            && entry.GetProperty("read_path_class").GetString() == "audit_handoff"
            && entry.GetProperty("resource_pack_covered").GetBoolean());
        Assert.Contains(root.GetProperty("entries").EnumerateArray(), entry =>
            entry.GetProperty("surface_id").GetString() == "packet-enforcement"
            && entry.GetProperty("coverage_status").GetString() == "covered"
            && !entry.GetProperty("resource_pack_required").GetBoolean());
        Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item =>
            item.GetString()!.Contains("does not prove behavior correctness", StringComparison.Ordinal));
    }
}
