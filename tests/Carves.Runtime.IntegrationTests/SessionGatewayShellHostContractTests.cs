using System.Net.Http;

namespace Carves.Runtime.IntegrationTests;

public sealed class SessionGatewayShellHostContractTests
{
    [Fact]
    public async Task SessionGatewayThinShell_ProjectsRuntimeOwnedGatewayWithoutSecondControlPlane()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            Assert.True(start.ExitCode == 0, start.CombinedOutput);

            var baseUrl = SessionGatewayHostContractTests_ReadRepoHostBaseUrl(sandbox.RootPath);
            using var client = new HttpClient();

            var html = await client.GetStringAsync($"{baseUrl}/session-gateway/v1/shell");

            Assert.Contains("CARVES AgentShell.Web", html, StringComparison.Ordinal);
            Assert.Contains("Strict Broker only", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No second control plane", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/api/session-gateway/v1/sessions", html, StringComparison.Ordinal);
            Assert.Contains("/api/session-gateway/v1/operations/", html, StringComparison.Ordinal);
            Assert.Contains("/api/session-gateway/v1/operations/{operation_id}/approve", html, StringComparison.Ordinal);
            Assert.Contains("/api/session-gateway/v1/operations/{operation_id}/reject", html, StringComparison.Ordinal);
            Assert.Contains("/api/session-gateway/v1/operations/{operation_id}/replan", html, StringComparison.Ordinal);
            Assert.Contains("Client Repo Root", html, StringComparison.Ordinal);
            Assert.Contains("client_repo_root", html, StringComparison.Ordinal);
            Assert.Contains("narrow private alpha ready", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WAITING_OPERATOR_SETUP", html, StringComparison.Ordinal);
            Assert.Contains("Repo-local readiness is not operator-run completion", html, StringComparison.Ordinal);
        }
        finally
        {
            SessionGatewayHostContractTests_StopHost(sandbox.RootPath);
        }
    }

    [Fact]
    public async Task WorkbenchBridge_AndSessionGatewayShell_ShareSameRuntimeOwnedHostLane()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        sandbox.MarkAllTasksCompleted();

        try
        {
            var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "start", "--interval-ms", "200");
            var status = ProgramHarness.Run("--repo-root", sandbox.RootPath, "host", "status", "--require-capability", "workbench");
            Assert.True(start.ExitCode == 0, start.CombinedOutput);
            Assert.True(status.ExitCode == 0, status.CombinedOutput);

            var baseUrl = SessionGatewayHostContractTests_ReadRepoHostBaseUrl(sandbox.RootPath);
            var workbench = ProgramHarness.Run("--repo-root", sandbox.RootPath, "workbench", "review");
            using var client = new HttpClient();

            var workbenchUrl = ExtractOutputValue(workbench.StandardOutput, "Workbench: ");
            var html = await client.GetStringAsync($"{baseUrl}/session-gateway/v1/shell");

            Assert.Equal(0, workbench.ExitCode);
            Assert.Equal($"{baseUrl}/workbench/review", workbenchUrl);
            Assert.Contains("Strict Broker only", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No second control plane", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/api/session-gateway/v1/sessions", html, StringComparison.Ordinal);
            Assert.Contains("/api/session-gateway/v1/operations/{operation_id}/approve", html, StringComparison.Ordinal);
        }
        finally
        {
            SessionGatewayHostContractTests_StopHost(sandbox.RootPath);
        }
    }

    private static string SessionGatewayHostContractTests_ReadRepoHostBaseUrl(string repoRoot)
    {
        var descriptorPath = Path.Combine(repoRoot, ".carves-platform", "host", "descriptor.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(descriptorPath));
        return document.RootElement.GetProperty("base_url").GetString()
            ?? throw new InvalidOperationException("Expected host base_url.");
    }

    private static string ExtractOutputValue(string output, string prefix)
    {
        return output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .First(line => line.StartsWith(prefix, StringComparison.Ordinal))
            [prefix.Length..]
            .Trim();
    }

    private static void SessionGatewayHostContractTests_StopHost(string repoRoot)
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "session gateway shell cleanup");
    }
}
