using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Host;

namespace Carves.Runtime.IntegrationTests;

[Collection("console-program-host-contract")]
public sealed class RuntimeProjectionSmokeHostContractTests
{
    public static TheoryData<string, string> NoArgumentProjectionCommands =>
        new()
        {
            { "runtime-hotspot-cross-family-patterns", "Runtime governance archive status" },
            { "runtime-central-interaction-multi-device-projection", "Runtime central interaction multi-device projection" },
            { "runtime-semantic-correctness-death-test-evidence", "Runtime semantic-correctness death-test evidence" },
            { "runtime-session-gateway-dogfood-validation", "Runtime Session Gateway dogfood validation" },
            { "runtime-session-gateway-governance-assist", "Runtime Session Gateway governance assist" },
        };

    [Theory]
    [MemberData(nameof(NoArgumentProjectionCommands))]
    public void RuntimeProjectionSmoke_InspectAndApiKeepSelectedReadOnlySurfacesQueryable(string command, string inspectHeading)
    {
        var repoRoot = ResolveRepoRoot();

        Assert.Contains(command, RuntimeSurfaceCommandRegistry.CommandNames, StringComparer.Ordinal);

        var inspect = RunProgram("--repo-root", repoRoot, "--cold", "inspect", command);
        var api = RunProgram("--repo-root", repoRoot, "--cold", "api", command);
        var inspectOutput = inspect.CombinedOutput;

        AssertQueryableInspectExit(command, inspect);
        Assert.Equal(0, api.ExitCode);
        Assert.Contains(inspectHeading, inspectOutput, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(api.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(command, root.GetProperty("surface_id").GetString());
        Assert.True(root.EnumerateObject().Count() > 1);
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

    private static string ResolveRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static void AssertQueryableInspectExit(string command, ProgramRunResult inspect)
    {
        if (string.Equals(command, "runtime-session-gateway-governance-assist", StringComparison.Ordinal))
        {
            Assert.True(inspect.ExitCode is 0 or 1, inspect.StandardOutput + inspect.StandardError);
            if (inspect.ExitCode == 1)
            {
                Assert.Contains("Validation valid: False", inspect.CombinedOutput, StringComparison.Ordinal);
            }

            return;
        }

        Assert.Equal(0, inspect.ExitCode);
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}
