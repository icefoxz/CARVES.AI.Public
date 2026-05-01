namespace Carves.Audit.Tests;

public sealed class AuditStandaloneReadinessTests
{
    [Fact]
    public void PackageMetadataDocsAndSmokeAreEvidenceLayerReady()
    {
        var repoRoot = FindRepositoryRoot();
        var cliProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Audit.Cli", "Carves.Audit.Cli.csproj"));
        var coreProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Audit.Core", "Carves.Audit.Core.csproj"));
        var distribution = File.ReadAllText(Path.Combine(repoRoot, "docs", "guides", "CARVES_AUDIT_DISTRIBUTION.md"));
        var checkpoint = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "audit-evidence-layer-checkpoint.md"));
        var smoke = File.ReadAllText(Path.Combine(repoRoot, "scripts", "audit", "audit-packaged-install-smoke.ps1"));

        foreach (var project in new[] { cliProject, coreProject })
        {
            Assert.Contains("<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>", project, StringComparison.Ordinal);
            Assert.Contains("<PackageProjectUrl>https://github.com/CARVES-AI/CARVES.Runtime</PackageProjectUrl>", project, StringComparison.Ordinal);
            Assert.Contains("<RepositoryUrl>https://github.com/CARVES-AI/CARVES.Runtime</RepositoryUrl>", project, StringComparison.Ordinal);
            Assert.Contains("CARVES_AUDIT_DISTRIBUTION.md", project, StringComparison.Ordinal);
        }

        Assert.Contains(".ai/runtime/guard/decisions.jsonl", distribution, StringComparison.Ordinal);
        Assert.Contains(".ai/handoff/handoff.json", distribution, StringComparison.Ordinal);
        Assert.Contains("carves-audit evidence --json --output .carves/shield-evidence.json", distribution, StringComparison.Ordinal);
        Assert.Contains("ready as the matrix evidence discovery layer", checkpoint, StringComparison.Ordinal);
        Assert.Contains("\"dotnet\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"tool\"", smoke, StringComparison.Ordinal);
        Assert.Contains("\"install\"", smoke, StringComparison.Ordinal);
        Assert.Contains("summary", smoke, StringComparison.Ordinal);
        Assert.Contains("timeline", smoke, StringComparison.Ordinal);
        Assert.Contains("explain", smoke, StringComparison.Ordinal);
        Assert.Contains("evidence", smoke, StringComparison.Ordinal);
        Assert.Contains("remote_registry_published = $false", smoke, StringComparison.Ordinal);
        Assert.Contains("nuget_org_push_required = $false", smoke, StringComparison.Ordinal);
    }

    [Fact]
    public void AuditDocsStayInsideAuditProductBoundary()
    {
        var repoRoot = FindRepositoryRoot();
        var docs = Directory
            .EnumerateFiles(Path.Combine(repoRoot, "docs", "audit"), "*.md", SearchOption.AllDirectories)
            .Concat([Path.Combine(repoRoot, "docs", "guides", "CARVES_AUDIT_DISTRIBUTION.md")])
            .Select(File.ReadAllText)
            .ToArray();

        Assert.NotEmpty(docs);
        foreach (var doc in docs)
        {
            Assert.Contains("Audit", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("planner wave", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Runtime internal", doc, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("TaskGraph", doc, StringComparison.Ordinal);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "CARVES.Runtime.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Repository root could not be found.");
    }
}
