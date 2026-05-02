namespace Carves.Runtime.IntegrationTests;

public sealed class MatrixNativeCiLaneTests
{
    [Fact]
    public void MatrixWorkflow_DefinesLinuxNativeProofAndVerifyLaneWithoutPowerShell()
    {
        var repoRoot = LocateSourceRepoRoot();
        var workflow = File.ReadAllText(Path.Combine(repoRoot, ".github", "workflows", "matrix-proof.yml"));
        var nativeJob = SliceSource(workflow, "  matrix-native-proof-linux:", "  matrix-native-full-release-shadow-linux:");
        var shadowJob = SliceSource(workflow, "  matrix-native-full-release-shadow-linux:", "  matrix-proof:");
        var fullJobStart = workflow.IndexOf("  matrix-proof:", StringComparison.Ordinal);
        Assert.True(fullJobStart >= 0, "Missing full Runtime integration proof job.");
        var fullJob = workflow[fullJobStart..];

        Assert.Contains("runs-on: ubuntu-latest", nativeJob, StringComparison.Ordinal);
        Assert.Contains("shell: bash", nativeJob, StringComparison.Ordinal);
        Assert.Contains("dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release", nativeJob, StringComparison.Ordinal);
        Assert.Contains("--no-build", nativeJob, StringComparison.Ordinal);
        Assert.Contains("proof \\", nativeJob, StringComparison.Ordinal);
        Assert.Contains("--lane native-minimal", nativeJob, StringComparison.Ordinal);
        Assert.Contains("--artifact-root \"$artifact_root\"", nativeJob, StringComparison.Ordinal);
        Assert.Contains("--configuration Release", nativeJob, StringComparison.Ordinal);
        Assert.Contains("--json", nativeJob, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native/ubuntu-latest", nativeJob, StringComparison.Ordinal);
        Assert.Contains("matrix-native-proof.json", nativeJob, StringComparison.Ordinal);
        Assert.Contains("verify \\", nativeJob, StringComparison.Ordinal);
        Assert.Contains("\"$proof_root\"", nativeJob, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native-verify/ubuntu-latest", nativeJob, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.json", nativeJob, StringComparison.Ordinal);
        Assert.Contains("cp \"$proof_root/matrix-artifact-manifest.json\"", nativeJob, StringComparison.Ordinal);
        Assert.Contains("cp \"$proof_root/matrix-proof-summary.json\"", nativeJob, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-proof-ubuntu-latest", nativeJob, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-verify-ubuntu-latest", nativeJob, StringComparison.Ordinal);

        Assert.DoesNotContain("pwsh", nativeJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".ps1", nativeJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrix-proof-lane.ps1", nativeJob, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-cross-platform-verify-pilot.ps1", nativeJob, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", nativeJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh release", nativeJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nuget push", nativeJob, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("runs-on: ubuntu-latest", shadowJob, StringComparison.Ordinal);
        Assert.Contains("continue-on-error: true", shadowJob, StringComparison.Ordinal);
        Assert.Contains("shell: bash", shadowJob, StringComparison.Ordinal);
        Assert.Contains("dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release", shadowJob, StringComparison.Ordinal);
        Assert.Contains("--no-build", shadowJob, StringComparison.Ordinal);
        Assert.Contains("proof \\", shadowJob, StringComparison.Ordinal);
        Assert.Contains("--lane native-full-release", shadowJob, StringComparison.Ordinal);
        Assert.Contains("--runtime-root .", shadowJob, StringComparison.Ordinal);
        Assert.Contains("--artifact-root \"$artifact_root\"", shadowJob, StringComparison.Ordinal);
        Assert.Contains("--configuration Release", shadowJob, StringComparison.Ordinal);
        Assert.Contains("--json", shadowJob, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native-full-release-shadow/ubuntu-latest", shadowJob, StringComparison.Ordinal);
        Assert.Contains("matrix-native-full-release-proof.json", shadowJob, StringComparison.Ordinal);
        Assert.Contains("verify \\", shadowJob, StringComparison.Ordinal);
        Assert.Contains("\"$proof_root\"", shadowJob, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native-full-release-shadow-verify/ubuntu-latest", shadowJob, StringComparison.Ordinal);
        Assert.Contains("matrix-verify.json", shadowJob, StringComparison.Ordinal);
        Assert.Contains("cp \"$proof_root/matrix-artifact-manifest.json\"", shadowJob, StringComparison.Ordinal);
        Assert.Contains("cp \"$proof_root/matrix-proof-summary.json\"", shadowJob, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-full-release-shadow-ubuntu-latest", shadowJob, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-full-release-shadow-verify-ubuntu-latest", shadowJob, StringComparison.Ordinal);
        Assert.DoesNotContain("pwsh", shadowJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".ps1", shadowJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("matrix-proof-lane.ps1", shadowJob, StringComparison.Ordinal);
        Assert.DoesNotContain("matrix-cross-platform-verify-pilot.ps1", shadowJob, StringComparison.Ordinal);
        Assert.DoesNotContain("secrets.", shadowJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gh release", shadowJob, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nuget push", shadowJob, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("shell: pwsh", fullJob, StringComparison.Ordinal);
        Assert.Contains("./scripts/matrix/matrix-proof-lane.ps1", fullJob, StringComparison.Ordinal);
        Assert.Contains("./scripts/matrix/matrix-cross-platform-verify-pilot.ps1", fullJob, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix/${{ matrix.os }}", fullJob, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-proof-${{ matrix.os }}", fullJob, StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixDocs_SeparateLinuxNativeMinimumShadowAndFullPowerShellProof()
    {
        var repoRoot = LocateSourceRepoRoot();
        var proofDoc = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "github-actions-proof.md"));
        var releaseNotes = File.ReadAllText(Path.Combine(repoRoot, "docs", "release", "matrix-release-notes.md"));

        foreach (var doc in new[] { proofDoc, releaseNotes })
        {
            Assert.Contains("Linux-native", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("matrix-native-proof-linux", doc, StringComparison.Ordinal);
            Assert.Contains("matrix-native-full-release-shadow-linux", doc, StringComparison.Ordinal);
            Assert.Contains("carves-matrix proof --lane native-minimal --json", doc, StringComparison.Ordinal);
            Assert.Contains("carves-matrix proof --lane native-full-release --json", doc, StringComparison.Ordinal);
            Assert.Contains("carves-matrix verify", doc, StringComparison.Ordinal);
            Assert.Contains("PowerShell", doc, StringComparison.Ordinal);
            Assert.Contains("non-blocking shadow", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not the default", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("summary-only", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("hosted verification", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("leaderboard", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("certification", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("does not install or invoke `pwsh`", proofDoc, StringComparison.Ordinal);
        Assert.Contains("does not invoke `scripts/matrix/*.ps1`", proofDoc, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native/ubuntu-latest/matrix-native-proof.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native-verify/ubuntu-latest/matrix-verify.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-proof-ubuntu-latest", proofDoc, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-verify-ubuntu-latest", proofDoc, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native-full-release-shadow/ubuntu-latest/matrix-native-full-release-proof.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("artifacts/matrix-native-full-release-shadow-verify/ubuntu-latest/matrix-verify.json", proofDoc, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-full-release-shadow-ubuntu-latest", proofDoc, StringComparison.Ordinal);
        Assert.Contains("carves-runtime-integration-native-full-release-shadow-verify-ubuntu-latest", proofDoc, StringComparison.Ordinal);
        Assert.Contains("Windows native full-release shadow is explicitly deferred", proofDoc, StringComparison.Ordinal);

        Assert.Contains("full PowerShell release proof", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("Windows native full-release shadow remains deferred", releaseNotes, StringComparison.Ordinal);
        Assert.Contains("do not publish packages, tags, releases, hosted verification, leaderboard entries, or certification claims", releaseNotes, StringComparison.Ordinal);
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

    private static string SliceSource(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing source marker: {start}");
        var endIndex = source.IndexOf(end, startIndex, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Missing source marker after {start}: {end}");
        return source[startIndex..endIndex];
    }
}
