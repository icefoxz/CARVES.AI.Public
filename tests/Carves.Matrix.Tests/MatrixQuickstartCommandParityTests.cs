namespace Carves.Matrix.Tests;

public sealed class MatrixQuickstartCommandParityTests
{
    private const string NativeQuickstartArtifactRoot = "artifacts/matrix/native-quickstart";
    private const string DotnetBuildCommand = "dotnet build ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release";
    private const string DotnetProofCommand = "dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json";
    private const string DotnetVerifyCommand = "dotnet run --project ./src/CARVES.Matrix.Cli/Carves.Matrix.Cli.csproj --configuration Release --no-build -- verify artifacts/matrix/native-quickstart --json";
    private const string InstalledProofCommand = "carves-matrix proof --lane native-minimal --artifact-root artifacts/matrix/native-quickstart --configuration Release --json";
    private const string InstalledVerifyCommand = "carves-matrix verify artifacts/matrix/native-quickstart --json";
    private const string FullReleasePowerShellCommand = "pwsh ./scripts/matrix/matrix-proof-lane.ps1";

    [Fact]
    public void Readme_UsesSameNativeQuickstartArtifactRootAsSmokeTests()
    {
        var readme = ReadMatrixDoc("README.md");

        Assert.Contains(InstalledProofCommand, readme, StringComparison.Ordinal);
        Assert.Contains(InstalledVerifyCommand, readme, StringComparison.Ordinal);
        Assert.Contains(NativeQuickstartArtifactRoot, readme, StringComparison.Ordinal);
        Assert.DoesNotContain("carves-matrix proof --artifact-root artifacts/matrix/native --configuration Release --json", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("carves-matrix proof --artifact-root artifacts/matrix/native-quickstart --configuration Release --json", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("carves-matrix verify artifacts/matrix --json", readme, StringComparison.Ordinal);
        AssertBefore(readme, InstalledProofCommand, FullReleasePowerShellCommand);
        AssertBefore(readme, InstalledVerifyCommand, FullReleasePowerShellCommand);
    }

    [Fact]
    public void Quickstarts_CoverBuildProofVerifyAndInstalledToolForms()
    {
        foreach (var doc in new[] { ReadMatrixDoc("quickstart.en.md"), ReadMatrixDoc("quickstart.zh-CN.md") })
        {
            Assert.Contains(DotnetBuildCommand, doc, StringComparison.Ordinal);
            Assert.Contains(DotnetProofCommand, doc, StringComparison.Ordinal);
            Assert.Contains(DotnetVerifyCommand, doc, StringComparison.Ordinal);
            Assert.Contains(InstalledProofCommand, doc, StringComparison.Ordinal);
            Assert.Contains(InstalledVerifyCommand, doc, StringComparison.Ordinal);
            Assert.Contains(NativeQuickstartArtifactRoot, doc, StringComparison.Ordinal);
            AssertBefore(doc, DotnetProofCommand, FullReleasePowerShellCommand);
            AssertBefore(doc, DotnetVerifyCommand, FullReleasePowerShellCommand);
            AssertBefore(doc, InstalledProofCommand, FullReleasePowerShellCommand);
            AssertBefore(doc, InstalledVerifyCommand, FullReleasePowerShellCommand);
        }
    }

    [Fact]
    public void Quickstarts_MarkPowerShellAsFullReleaseLaneNotLinuxNativeRequirement()
    {
        var readme = ReadMatrixDoc("README.md");
        var quickstartEn = ReadMatrixDoc("quickstart.en.md");
        var quickstartZh = ReadMatrixDoc("quickstart.zh-CN.md");

        Assert.Contains("PowerShell proof", readme, StringComparison.Ordinal);
        Assert.Contains("without making PowerShell a requirement for the Linux-native first-run proof", readme, StringComparison.Ordinal);
        Assert.Contains("PowerShell 7 or newer only if you want the full release proof", quickstartEn, StringComparison.Ordinal);
        Assert.Contains("PowerShell scripts are release proof and packaged smoke lanes, not the Linux first-run requirement.", quickstartEn, StringComparison.Ordinal);
        Assert.Contains("只有在需要 full release proof 或 packaged-install smoke lane 时才需要 PowerShell 7", quickstartZh, StringComparison.Ordinal);
        Assert.Contains("PowerShell scripts 是 release proof 与 packaged smoke lane，不是 Linux first-run requirement。", quickstartZh, StringComparison.Ordinal);
    }

    private static string ReadMatrixDoc(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            MatrixProofSummarySchemaTestSupport.LocateSourceRepoRoot(),
            "docs",
            "matrix",
            fileName));
    }

    private static void AssertBefore(string text, string earlier, string later)
    {
        var earlierIndex = text.IndexOf(earlier, StringComparison.Ordinal);
        var laterIndex = text.IndexOf(later, StringComparison.Ordinal);
        Assert.True(earlierIndex >= 0, $"Missing expected earlier text: {earlier}");
        Assert.True(laterIndex >= 0, $"Missing expected later text: {later}");
        Assert.True(earlierIndex < laterIndex, $"Expected '{earlier}' to appear before '{later}'.");
    }
}
