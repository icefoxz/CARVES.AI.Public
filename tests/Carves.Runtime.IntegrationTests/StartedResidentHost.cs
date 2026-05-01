using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

internal sealed class StartedResidentHost : IDisposable
{
    private readonly string repoRoot;

    private StartedResidentHost(string repoRoot, string baseUrl)
    {
        this.repoRoot = repoRoot;
        BaseUrl = baseUrl;
    }

    public string BaseUrl { get; }

    public static StartedResidentHost Start(string repoRoot, int intervalMilliseconds)
    {
        var start = ProgramHarness.Run(
            "--repo-root",
            repoRoot,
            "host",
            "start",
            "--interval-ms",
            intervalMilliseconds.ToString());
        if (start.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to start resident host.{Environment.NewLine}{start.CombinedOutput}");
        }

        return new StartedResidentHost(repoRoot, ReadRepoHostBaseUrl(repoRoot));
    }

    public void Dispose()
    {
        ProgramHarness.Run("--repo-root", repoRoot, "host", "stop", "integration cleanup");
    }

    private static string ReadRepoHostBaseUrl(string repoRoot)
    {
        var descriptorPath = Path.Combine(repoRoot, ".carves-platform", "host", "descriptor.json");
        using var document = JsonDocument.Parse(File.ReadAllText(descriptorPath));
        return document.RootElement.GetProperty("base_url").GetString()
            ?? throw new InvalidOperationException("Expected host base_url.");
    }
}
