using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeFileGranularityAuditHostContractTests
{
    [Fact]
    public void InspectAndApiRuntimeFileGranularityAudit_ProjectCurrentRepoGranularityEvidence()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-file-granularity-audit");
        var api = ProgramHarness.Run("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-file-granularity-audit");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime file granularity audit", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Default-path participation: not_default", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Tiny clusters:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Partial-family clusters:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Cleanup candidates:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Cleanup selection:", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("read-only", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-file-granularity-audit", root.GetProperty("surface_id").GetString());
        Assert.True(root.GetProperty("is_valid").GetBoolean());
        Assert.Equal("active_maintenance_audit", root.GetProperty("lifecycle_class").GetString());
        Assert.Equal("conditional_maintenance_audit", root.GetProperty("read_path_class").GetString());
        Assert.Equal("not_default", root.GetProperty("default_path_participation").GetString());
        Assert.True(root.GetProperty("counts").GetProperty("total_file_count").GetInt32() > 0);
        Assert.True(root.GetProperty("counts").GetProperty("source_file_count").GetInt32() > 0);
        Assert.Contains(root.GetProperty("cleanup_candidates").EnumerateArray(), item =>
            item.GetProperty("recommended_action").GetString()!.Contains("bounded cleanup card", StringComparison.Ordinal)
            || item.GetProperty("candidate_type").GetString() == "huge_file_decomposition_review");
        var selection = root.GetProperty("cleanup_selection");
        Assert.Equal("bounded_low_risk_first", selection.GetProperty("strategy").GetString());
        Assert.True(selection.GetProperty("selected_candidate_count").GetInt32() <= selection.GetProperty("max_batch_size").GetInt32());
        Assert.Contains(selection.GetProperty("selected_candidates").EnumerateArray(), item =>
            item.GetProperty("risk_class").GetString() == "lower_risk_test_scope");
        Assert.Contains(root.GetProperty("non_claims").EnumerateArray(), item =>
            item.GetString()!.Contains("does not merge", StringComparison.Ordinal));
    }
}
