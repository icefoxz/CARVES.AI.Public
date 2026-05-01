using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

[Collection("console-program-host-contract")]
public sealed class RuntimeSessionGatewayRepeatabilityHostContractTests
{
    [Fact]
    public void RuntimeSessionGatewayRepeatability_InspectAndApiProjectBoundedRepeatabilityTruth()
    {
        var repoRoot = ResolveRepoRoot();

        var inspect = RunProgram("--repo-root", repoRoot, "--cold", "inspect", "runtime-session-gateway-repeatability");
        var api = RunProgram("--repo-root", repoRoot, "--cold", "api", "runtime-session-gateway-repeatability");
        var inspectOutput = inspect.CombinedOutput;

        AssertInspectablePostureExit(inspect);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Session Gateway repeatability readiness", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Private alpha handoff posture:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Repeatability doc: docs/session-gateway/repeatability-readiness.md", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Recent gateway tasks:", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("contract_binding=", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("review_evidence=", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("worker_completion_claim=", inspectOutput, StringComparison.Ordinal);
        Assert.Contains("Artifact bundle commands:", inspectOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-session-gateway-repeatability", root.GetProperty("surface_id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("overall_posture").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("private_alpha_handoff_posture").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("program_closure_verdict").GetString()));
        Assert.Equal("docs/session-gateway/repeatability-readiness.md", root.GetProperty("repeatability_readiness_path").GetString());
        Assert.Equal("/session-gateway/v1/shell", root.GetProperty("thin_shell_route").GetString());
        Assert.Equal("repo_local_proof", root.GetProperty("operator_proof_contract").GetProperty("current_proof_source").GetString());
        Assert.True(root.GetProperty("recent_gateway_tasks").GetArrayLength() >= 1);
        Assert.NotNull(root.GetProperty("recent_gateway_tasks")[0].GetProperty("acceptance_contract_binding_state").GetString());
        Assert.NotNull(root.GetProperty("recent_gateway_tasks")[0].GetProperty("review_evidence_status").GetString());
        Assert.NotNull(root.GetProperty("recent_gateway_tasks")[0].GetProperty("worker_completion_claim_status").GetString());
        Assert.True(root.GetProperty("recent_gateway_tasks")[0].TryGetProperty("missing_worker_completion_claim_fields", out _));
        Assert.True(root.GetProperty("artifact_bundle_commands").GetArrayLength() >= 1);
    }

    private static void AssertInspectablePostureExit(ProgramRunResult result)
    {
        Assert.True(result.ExitCode is 0 or 1, result.StandardOutput + result.StandardError);
        if (result.ExitCode == 1)
        {
            Assert.Contains("Validation valid: False", result.CombinedOutput, StringComparison.Ordinal);
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
            var exitCode = Host.Program.Main(args);
            return new ProgramRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}
