namespace Carves.Matrix.Tests;

public sealed class MatrixWindowsPlayableReleasePackageContractTests
{
    [Fact]
    public void WindowsPlayableReleasePackageScriptBuildsZipFromSelfContainedScorerRoot()
    {
        var repoRoot = MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot();
        var script = Read(repoRoot, "scripts/matrix/build-windows-playable-package.ps1");
        var docs = string.Join(
            Environment.NewLine,
            Read(repoRoot, "docs/matrix/README.md"),
            Read(repoRoot, "docs/matrix/portable-scorer-bundle.md"),
            Read(repoRoot, "docs/matrix/portable-agent-trial-pack.md"),
            Read(repoRoot, "docs/matrix/agent-trial-v1-local-quickstart.md"),
            Read(repoRoot, "docs/matrix/known-limitations.md"));

        foreach (var text in new[]
        {
            "scripts/matrix/build-windows-playable-package.ps1",
            "publish-windows-playable-scorer.ps1",
            "carves.exe test package --windows-playable",
            "tools/carves/carves.exe",
            "tools/carves/scorer-root-manifest.json",
            "tools/carves/scorer-manifest.json",
            "agent-workspace/tools/",
            "absolute local path leaks"
        })
        {
            Assert.Contains(text, docs, StringComparison.Ordinal);
        }

        foreach (var text in new[]
        {
            "publish-windows-playable-scorer.ps1",
            "carves.exe",
            "\"test\"",
            "\"package\"",
            "--windows-playable",
            "--scorer-root",
            "--zip-output",
            "Assert-ZipEntryNamesArePortable",
            "Assert-ZipContains -Entries $entries -EntryName \"tools/carves/carves.exe\"",
            "Assert-ZipContains -Entries $entries -EntryName \"tools/carves/scorer-root-manifest.json\"",
            "Assert-ZipContains -Entries $entries -EntryName \"tools/carves/scorer-manifest.json\"",
            "Assert-ZipDoesNotContainPrefix -Entries $entries -Prefix \"agent-workspace/tools/\"",
            "Assert-NoLocalRootLeak"
        })
        {
            Assert.Contains(text, script, StringComparison.Ordinal);
        }
    }

    private static string Read(string repoRoot, string relativePath)
    {
        return File.ReadAllText(Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
