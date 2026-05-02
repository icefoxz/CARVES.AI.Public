namespace Carves.Matrix.Tests;

public sealed class MatrixPortableScorerBundleContractTests
{
    [Fact]
    public void PortableScorerBundleContractPinsWindowsPlayableLayoutAndNoGlobalFirstRun()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "tools/carves/",
            "tools/carves/carves.exe",
            "tools/carves/scorer-manifest.json",
            "scorer-manifest.json",
            "SCORE.cmd",
            "score.sh",
            "global `carves`",
            "source checkout",
            "`dotnet build`",
            "RESULT.cmd",
            "RESET.cmd",
            "Windows playable zip"
        })
        {
            Assert.Contains(text, docs, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Release Windows playable zip: includes a self-contained scorer", docs, StringComparison.Ordinal);
        Assert.Contains("Developer directory package: may be framework-dependent or scorerless", docs, StringComparison.Ordinal);
        Assert.Contains("must not be described as the Windows playable zip", docs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortableScorerManifestContractPinsDiagnosticsAndNonClaims()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var contract = Read(repoRoot, "docs/matrix/portable-scorer-bundle.md");

        foreach (var text in new[]
        {
            "\"schema_version\"",
            "\"carves-portable-scorer.v0\"",
            "\"scorer_kind\"",
            "\"runtime_identifier\"",
            "\"entrypoint\"",
            "\"carves_version\"",
            "\"build_label\"",
            "\"supported_commands\"",
            "\"self_contained\"",
            "\"requires_source_checkout_to_run\"",
            "\"requires_dotnet_to_run\"",
            "\"uses_dotnet_run\"",
            "\"scorer_root_manifest\"",
            "\"local_only\"",
            "\"file_hashes\"",
            "\"file_hashes_unavailable_reason\""
        })
        {
            Assert.Contains(text, contract, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "diagnostic metadata",
            "tamper-proof signature",
            "certification",
            "a server receipt",
            "leaderboard proof",
            "producer identity",
            "anti-cheat",
            "operating-system sandbox"
        })
        {
            Assert.Contains(text, contract, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WindowsPlayableScorerRootPublishContractPinsSelfContainedRuntimeCli()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));
        var script = Read(repoRoot, "scripts/matrix/publish-windows-playable-scorer.ps1");

        foreach (var text in new[]
        {
            "scripts/matrix/publish-windows-playable-scorer.ps1",
            "src/CARVES.Runtime.Cli/carves.csproj",
            "self-contained `win-x64`",
            "carves.exe",
            "scorer-root-manifest.json",
            "requires_source_checkout_to_run=false",
            "requires_dotnet_to_run=false",
            "uses_dotnet_run=false",
            "supported_commands=[\"test collect\",\"test reset\",\"test verify\",\"test result\"]"
        })
        {
            Assert.Contains(text, docs, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "src/CARVES.Runtime.Cli/carves.csproj",
            "\"win-x64\"",
            "\"--self-contained\"",
            "\"true\"",
            "\"-p:UseAppHost=true\"",
            "\"-p:PublishSingleFile=false\"",
            "\"carves.exe\"",
            "carves-windows-scorer-root.v0",
            "requires_dotnet_to_run = $false",
            "uses_dotnet_run = $false",
            "\"test collect\"",
            "\"test reset\"",
            "\"test verify\"",
            "\"test result\""
        })
        {
            Assert.Contains(text, script, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("dotnet run", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PortableScorerBundleContractStagesLinuxMacOsFollowUpWithoutOverclaiming()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"));

        foreach (var text in new[]
        {
            "Linux/macOS playable scorer bundles are staged follow-up, not the current Windows V1 gate",
            "Candidate linux-x64 layout",
            "Candidate macOS layouts",
            "tools/carves/carves",
            "score.sh",
            ".tar.gz",
            "executable bit",
            "LF line endings",
            "self-contained",
            "framework-dependent",
            "PATH `carves` is an advanced fallback only"
        })
        {
            Assert.Contains(text, docs, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "hosted verification",
            "certification",
            "leaderboard eligibility",
            "tamper-proof execution",
            "operating-system sandboxing"
        })
        {
            Assert.Contains(text, docs, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PortableScoreLaunchersDocumentAndImplementPackageLocalThenPathThenClearFailure()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"));
        var source = Read(repoRoot, "src/CARVES.Matrix.Core/MatrixTrialPortablePackageText.cs");

        Assert.Contains("Package-local scorer first", docs, StringComparison.Ordinal);
        Assert.Contains("PATH fallback second", docs, StringComparison.Ordinal);
        Assert.Contains("Clear failure third", docs, StringComparison.Ordinal);
        Assert.Contains("--windows-playable", docs, StringComparison.Ordinal);
        Assert.Contains("--scorer-root", docs, StringComparison.Ordinal);
        Assert.Contains("--zip-output", docs, StringComparison.Ordinal);

        var windowsLocal = source.IndexOf("tools\\carves\\carves.exe", StringComparison.Ordinal);
        var windowsPath = source.IndexOf("where carves", StringComparison.Ordinal);
        var windowsFailure = IndexAfter(source, "Missing scorer", windowsPath);
        var windowsDependencyCheck = IndexAfter(source, ":verify_runtime_dependencies", windowsPath);
        var windowsRunCarves = IndexAfter(source, "goto run_carves", windowsDependencyCheck);
        var windowsAlreadyScored = IndexAfter(source, ":already_scored", windowsDependencyCheck);
        Assert.True(windowsLocal >= 0, "SCORE.cmd must look for tools/carves/carves.exe.");
        Assert.True(windowsPath > windowsLocal, "SCORE.cmd must check PATH only after package-local scorer.");
        Assert.True(windowsFailure > windowsPath, "SCORE.cmd must print a clear failure after lookup exhaustion.");
        Assert.True(windowsRunCarves > windowsDependencyCheck, "SCORE.cmd must continue to scoring after dependency checks pass.");
        Assert.True(windowsAlreadyScored > windowsRunCarves, "SCORE.cmd must not fall through into the already-scored readback path before scoring.");

        var posixLocal = source.IndexOf("./tools/carves/carves", StringComparison.Ordinal);
        var posixPath = source.IndexOf("command -v carves", StringComparison.Ordinal);
        var posixFailure = IndexAfter(source, "Missing scorer", posixPath);
        Assert.True(posixLocal >= 0, "score.sh must look for tools/carves/carves.");
        Assert.True(posixPath > posixLocal, "score.sh must check PATH only after package-local scorer.");
        Assert.True(posixFailure > posixPath, "score.sh must print a clear failure after lookup exhaustion.");
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static int IndexAfter(string value, string search, int startIndex)
    {
        return startIndex < 0 ? -1 : value.IndexOf(search, startIndex, StringComparison.Ordinal);
    }
}
