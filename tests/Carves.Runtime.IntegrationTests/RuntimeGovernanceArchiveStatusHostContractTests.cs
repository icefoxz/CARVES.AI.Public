using System.Text.Json;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeGovernanceArchiveStatusHostContractTests
{
    [Fact]
    public void RuntimeGovernanceArchiveStatus_InspectAndApiProjectBoundedArchiveAliasStatus()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();

        var inspect = RunProgram("--repo-root", sandbox.RootPath, "--cold", "inspect", "runtime-governance-archive-status");
        var api = RunProgram("--repo-root", sandbox.RootPath, "--cold", "api", "runtime-governance-archive-status");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime governance archive status", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Role: primary", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("compatibility_aliases=6", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Compatibility classes: name_compat_only=6", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("historical_document_bodies_embedded=False", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.True(inspect.StandardOutput.Split(Environment.NewLine).Length <= 30);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-governance-archive-status", root.GetProperty("surface_id").GetString());
        Assert.Equal("primary", root.GetProperty("surface_role").GetString());
        Assert.Equal("active_primary", root.GetProperty("retirement_posture").GetString());
        Assert.Equal(6, root.GetProperty("legacy_aliases").GetArrayLength());
        Assert.Equal(6, root.GetProperty("compatibility_class_counts").GetProperty("name_compat_only").GetInt32());
        Assert.Equal(6, root.GetProperty("output_budget").GetProperty("alias_entry_count").GetInt32());
        Assert.False(root.GetProperty("output_budget").GetProperty("historical_document_bodies_embedded").GetBoolean());
        Assert.True(root.GetProperty("consumer_inventory").GetProperty("scanned_file_count").GetInt32() > 0);

        var allowedCompatibilityClasses = new[]
        {
            "name_compat_only",
            "shape_compat_required",
            "human_inspect_only",
            "blocked_unknown_consumer",
        };
        foreach (var alias in root.GetProperty("legacy_aliases").EnumerateArray())
        {
            var compatibilityClass = alias.GetProperty("compatibility_class").GetString() ?? "";
            Assert.Equal("runtime-governance-archive-status", alias.GetProperty("successor_surface_id").GetString());
            Assert.Equal("alias_retained", alias.GetProperty("retirement_posture").GetString());
            Assert.Contains(compatibilityClass, allowedCompatibilityClasses);
            Assert.Equal("name_compat_only", compatibilityClass);
            Assert.True(alias.GetProperty("legacy_api_fields").GetArrayLength() > 0);
            Assert.True(alias.GetProperty("command_reference_count").GetInt32() > 0);
            Assert.Equal(0, alias.GetProperty("json_field_consumer_reference_count").GetInt32());
            Assert.True(alias.GetProperty("compatibility_evidence_refs").GetArrayLength() > 0);
            Assert.True(alias.GetProperty("command_reference_evidence_refs").GetArrayLength() > 0);
            Assert.Equal(0, alias.GetProperty("json_field_consumer_evidence_refs").GetArrayLength());
        }
    }

    private static ProgramRunResult RunProgram(params string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
