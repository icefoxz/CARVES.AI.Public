namespace Carves.Runtime.IntegrationTests;

public sealed class ProductExtractionReadinessCheckpointTests
{
    [Fact]
    public void Checkpoint_ListsEveryProductRootPackagePostureAndExtractionRisk()
    {
        var repoRoot = LocateSourceRepoRoot();
        var checkpoint = Read(repoRoot, "docs/release/product-extraction-readiness-checkpoint.md");

        Assert.Contains("Guard, Handoff, Audit, Shield, and Matrix are extraction-ready product units", checkpoint, StringComparison.Ordinal);
        Assert.Contains("They are not yet separate repositories", checkpoint, StringComparison.Ordinal);
        Assert.Contains("bounded copy/split readiness", checkpoint, StringComparison.OrdinalIgnoreCase);

        var expectations = new[]
        {
            new ProductExpectation("Guard", "src/CARVES.Guard.Core/", "src/CARVES.Guard.Cli/", "docs/guard/", "scripts/guard/", "CARVES.Guard.Core", "CARVES.Guard.Cli", "0.2.0-beta.1", "carves-guard"),
            new ProductExpectation("Handoff", "src/CARVES.Handoff.Core/", "src/CARVES.Handoff.Cli/", "docs/handoff/", "scripts/handoff/", "CARVES.Handoff.Core", "CARVES.Handoff.Cli", "0.1.0-alpha.1", "carves-handoff"),
            new ProductExpectation("Audit", "src/CARVES.Audit.Core/", "src/CARVES.Audit.Cli/", "docs/audit/", "scripts/audit/", "CARVES.Audit.Core", "CARVES.Audit.Cli", "0.1.0-alpha.1", "carves-audit"),
            new ProductExpectation("Shield", "src/CARVES.Shield.Core/", "src/CARVES.Shield.Cli/", "docs/shield/", "scripts/shield/", "CARVES.Shield.Core", "CARVES.Shield.Cli", "0.1.0-alpha.1", "carves-shield"),
            new ProductExpectation("Matrix", "src/CARVES.Matrix.Core/", "src/CARVES.Matrix.Cli/", "docs/matrix/", "scripts/matrix/", "CARVES.Matrix.Core", "CARVES.Matrix.Cli", "0.2.0-alpha.1", "carves-matrix"),
        };

        foreach (var expectation in expectations)
        {
            Assert.Contains(expectation.Product, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.CodeRoot, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.CliRoot, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.DocsRoot, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.ScriptRoot, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.CorePackage, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.CliPackage, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.Version, checkpoint, StringComparison.Ordinal);
            Assert.Contains(expectation.ToolCommand, checkpoint, StringComparison.Ordinal);
        }

        Assert.Contains("Remaining extraction risks", checkpoint, StringComparison.Ordinal);
        Assert.Contains("bounded dependencies", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not published to NuGet.org", checkpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void Checkpoint_PreservesRuntimeCompatibilityBoundaryAndPublicNonClaims()
    {
        var repoRoot = LocateSourceRepoRoot();
        var checkpoint = Read(repoRoot, "docs/release/product-extraction-readiness-checkpoint.md");

        Assert.Contains("Runtime remains a compatibility and behavior-reference host", checkpoint, StringComparison.Ordinal);
        Assert.Contains("External users do not need Runtime task/card governance", checkpoint, StringComparison.Ordinal);
        Assert.Contains("carves guard", checkpoint, StringComparison.Ordinal);
        Assert.Contains("carves handoff", checkpoint, StringComparison.Ordinal);
        Assert.Contains("carves audit summary/timeline/explain/evidence", checkpoint, StringComparison.Ordinal);
        Assert.Contains("carves shield", checkpoint, StringComparison.Ordinal);
        Assert.Contains("carves matrix", checkpoint, StringComparison.Ordinal);

        Assert.Contains("does not claim", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already separate repositories", checkpoint, StringComparison.Ordinal);
        Assert.Contains("GitHub release has been created", checkpoint, StringComparison.Ordinal);
        Assert.Contains("Git tag has been created", checkpoint, StringComparison.Ordinal);
        Assert.Contains("NuGet.org packages have been published", checkpoint, StringComparison.Ordinal);
        Assert.Contains("packages have been signed", checkpoint, StringComparison.Ordinal);
        Assert.Contains("hosted verification exists", checkpoint, StringComparison.Ordinal);
        Assert.Contains("public leaderboard exists", checkpoint, StringComparison.Ordinal);
        Assert.Contains("certification exists", checkpoint, StringComparison.Ordinal);
        Assert.Contains("operating-system sandboxing exists", checkpoint, StringComparison.Ordinal);
        Assert.Contains("automatic rollback of arbitrary writes exists", checkpoint, StringComparison.Ordinal);
        Assert.Contains("external users must adopt Runtime governance", checkpoint, StringComparison.Ordinal);

        Assert.DoesNotContain("certified secure", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hosted verification is available", checkpoint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public leaderboard is available", checkpoint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Checkpoint_RecordsExactPostExtractionValidationCommands()
    {
        var repoRoot = LocateSourceRepoRoot();
        var checkpoint = Read(repoRoot, "docs/release/product-extraction-readiness-checkpoint.md");

        Assert.Contains("dotnet build CARVES.Runtime.sln --no-restore", checkpoint, StringComparison.Ordinal);
        Assert.Contains(@"dotnet test tests\Carves.Runtime.IntegrationTests\Carves.Runtime.IntegrationTests.csproj --filter ""RuntimeWrapperSlimmingTests|GuardExtractionShellTests|ShieldExtractionShellTests|MatrixExtractionShellTests|MatrixReleaseReadinessTests|GitHubPublishReadinessTests""", checkpoint, StringComparison.Ordinal);
        Assert.Contains(@"dotnet test tests\Carves.Audit.Tests\Carves.Audit.Tests.csproj --filter AuditCliRunnerTests", checkpoint, StringComparison.Ordinal);
        Assert.Contains("pwsh ./scripts/matrix/matrix-proof-lane.ps1 -ArtifactRoot <temp-artifact-root> -Configuration Debug", checkpoint, StringComparison.Ordinal);
        Assert.Contains("pwsh ./scripts/release/github-publish-readiness.ps1 -ArtifactRoot <temp-artifact-root> -AllowDirty", checkpoint, StringComparison.Ordinal);

        Assert.Contains("passed, 21 tests", checkpoint, StringComparison.Ordinal);
        Assert.Contains("passed, 19 tests", checkpoint, StringComparison.Ordinal);
        Assert.Contains("manifest includes 11 local package candidates", checkpoint, StringComparison.Ordinal);
    }

    [Fact]
    public void PublishReadinessAndDocsIndex_IncludeExtractionCheckpoint()
    {
        var repoRoot = LocateSourceRepoRoot();
        var script = Read(repoRoot, "scripts/release/github-publish-readiness.ps1");
        var index = Read(repoRoot, "docs/INDEX.md");
        var publishCheckpoint = Read(repoRoot, "docs/release/github-publish-readiness-checkpoint.md");

        Assert.Contains("docs/release/product-extraction-readiness-checkpoint.md", script, StringComparison.Ordinal);
        Assert.Contains("release/product-extraction-readiness-checkpoint.md", index, StringComparison.Ordinal);
        Assert.Contains("Guard / Handoff / Audit / Shield / Matrix", index, StringComparison.Ordinal);
        Assert.Contains("Post-product-extraction-readiness", publishCheckpoint, StringComparison.Ordinal);
        Assert.Contains("product-extraction-readiness-checkpoint.md", publishCheckpoint, StringComparison.Ordinal);
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string LocateSourceRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CARVES.Runtime.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate CARVES.Runtime source root from test output directory.");
    }

    private sealed record ProductExpectation(
        string Product,
        string CodeRoot,
        string CliRoot,
        string DocsRoot,
        string ScriptRoot,
        string CorePackage,
        string CliPackage,
        string Version,
        string ToolCommand);
}
