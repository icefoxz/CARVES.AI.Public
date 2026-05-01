using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeSessionGatewayPrivateAlphaHandoffHostContractTests
{
    [Fact]
    public void RuntimeSessionGatewayPrivateAlphaHandoff_InspectAndApiProjectBoundedAlphaHandoffTruth()
    {
        var repoRoot = ResolveRepoRoot();

        var inspect = RunProgram("--repo-root", repoRoot, "--cold", "inspect", "runtime-session-gateway-private-alpha-handoff");
        var api = RunProgram("--repo-root", repoRoot, "--cold", "api", "runtime-session-gateway-private-alpha-handoff");

        Assert.Equal(0, inspect.ExitCode);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains("Runtime Session Gateway private alpha handoff", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Overall posture: private_alpha_deliverable_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Dogfood validation posture: narrow_private_alpha_ready", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Program closure verdict: program_closure_complete", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Alpha setup doc: docs/session-gateway/ALPHA_SETUP.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Operator proof contract doc: docs/session-gateway/operator-proof-contract.md", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Operator proof current state: WAITING_OPERATOR_SETUP", inspect.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Bug-report bundle commands:", inspect.StandardOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal("runtime-session-gateway-private-alpha-handoff", root.GetProperty("surface_id").GetString());
        Assert.Equal("private_alpha_deliverable_ready", root.GetProperty("overall_posture").GetString());
        Assert.Equal("narrow_private_alpha_ready", root.GetProperty("dogfood_validation_posture").GetString());
        Assert.Equal("program_closure_complete", root.GetProperty("program_closure_verdict").GetString());
        Assert.Equal("runtime_owned_private_alpha", root.GetProperty("handoff_ownership").GetString());
        Assert.Equal("docs/session-gateway/operator-proof-contract.md", root.GetProperty("operator_proof_contract_path").GetString());
        Assert.Equal("/session-gateway/v1/shell", root.GetProperty("thin_shell_route").GetString());
        Assert.Equal("repo_local_proof", root.GetProperty("operator_proof_contract").GetProperty("current_proof_source").GetString());
        Assert.Equal("WAITING_OPERATOR_SETUP", root.GetProperty("operator_proof_contract").GetProperty("current_operator_state").GetString());
        Assert.Equal(3, root.GetProperty("supported_intents").GetArrayLength());
        Assert.True(root.GetProperty("startup_commands").GetArrayLength() >= 2);
        Assert.True(root.GetProperty("bug_report_bundle_commands").GetArrayLength() >= 3);
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

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError);
}
