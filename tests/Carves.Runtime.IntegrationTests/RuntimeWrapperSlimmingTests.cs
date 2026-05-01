using System.Diagnostics;

namespace Carves.Runtime.IntegrationTests;

public sealed class RuntimeWrapperSlimmingTests
{
    [Fact]
    public void RuntimeCli_ReferencesExtractedProductCoresAndDelegatesWrappers()
    {
        var repoRoot = LocateSourceRepoRoot();
        var runtimeProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "carves.csproj"));
        var mainDispatch = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.cs"));
        var commands = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Commands.cs"));
        var guard = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Guard.cs"));
        var shield = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Shield.cs"));
        var handoff = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Handoff.cs"));
        var matrix = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Matrix.cs"));
        var runtimeCliSources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli"), "*.cs").Select(File.ReadAllText));

        foreach (var project in new[]
        {
            "CARVES.Guard.Core",
            "CARVES.Handoff.Core",
            "CARVES.Audit.Core",
            "CARVES.Shield.Core",
            "CARVES.Matrix.Core",
        })
        {
            Assert.Contains(project, runtimeProject, StringComparison.Ordinal);
        }

        Assert.Contains("\"matrix\" => RunMatrix", mainDispatch, StringComparison.Ordinal);
        Assert.Contains("GuardCliRunner.Run", guard, StringComparison.Ordinal);
        Assert.Contains("HandoffCliRunner.Run", handoff, StringComparison.Ordinal);
        Assert.Contains("AuditCliRunner.Run", commands, StringComparison.Ordinal);
        Assert.Contains("IsPublicAuditProductCommand", commands, StringComparison.Ordinal);
        Assert.Contains("Delegate(repoRoot, \"audit\"", commands, StringComparison.Ordinal);
        Assert.Contains("ShieldCliRunner.Run", shield, StringComparison.Ordinal);
        Assert.Contains("MatrixCliRunner.Run", matrix, StringComparison.Ordinal);

        Assert.DoesNotContain("GuardCheckService", runtimeCliSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldEvaluationService", runtimeCliSources, StringComparison.Ordinal);
        Assert.DoesNotContain("ShieldBadgeService", runtimeCliSources, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditAlphaService", runtimeCliSources, StringComparison.Ordinal);
        Assert.DoesNotContain("HandoffInspectionService", runtimeCliSources, StringComparison.Ordinal);
        Assert.DoesNotContain("HandoffDraftService", runtimeCliSources, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCompatibilityWrappers_RunPublicProductHelpAndAuditSummary()
    {
        using var repo = RuntimeWrapperGitSandbox.Create();

        var guard = CliProgramHarness.RunInDirectory(repo.RootPath, "--repo-root", repo.RootPath, "guard", "help");
        var handoff = CliProgramHarness.RunInDirectory(repo.RootPath, "--repo-root", repo.RootPath, "handoff", "help");
        var shield = CliProgramHarness.RunInDirectory(repo.RootPath, "--repo-root", repo.RootPath, "shield", "help");
        var matrix = CliProgramHarness.RunInDirectory(repo.RootPath, "--repo-root", repo.RootPath, "matrix", "help");
        var audit = CliProgramHarness.RunInDirectory(repo.RootPath, "--repo-root", repo.RootPath, "audit", "summary", "--json");

        Assert.Equal(0, guard.ExitCode);
        Assert.Equal(0, handoff.ExitCode);
        Assert.Equal(0, shield.ExitCode);
        Assert.Equal(0, matrix.ExitCode);
        Assert.Equal(0, audit.ExitCode);

        Assert.Contains("Usage: carves guard", guard.StandardOutput);
        Assert.Contains("Usage: carves handoff", handoff.StandardOutput);
        Assert.Contains("Usage: carves shield", shield.StandardOutput);
        Assert.Contains("Usage: carves matrix", matrix.StandardOutput);
        Assert.Contains("\"schema_version\"", audit.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("audit-summary", audit.StandardOutput, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RuntimeDistWrappers_PreferPublishedCliBeforeSourceFallback()
    {
        var repoRoot = LocateSourceRepoRoot();
        var unixWrapper = File.ReadAllText(Path.Combine(repoRoot, "carves"));
        var powerShellWrapper = File.ReadAllText(Path.Combine(repoRoot, "carves.ps1"));
        var packScript = File.ReadAllText(Path.Combine(repoRoot, "scripts", "pack-runtime-dist.ps1"));

        Assert.Contains("runtime-cli/carves.dll", unixWrapper, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_FORCE_SOURCE", unixWrapper, StringComparison.Ordinal);
        Assert.Contains("dotnet \"$published_cli\"", unixWrapper, StringComparison.Ordinal);
        Assert.Contains("runtime-cli", powerShellWrapper, StringComparison.Ordinal);
        Assert.Contains("CARVES_RUNTIME_FORCE_SOURCE", powerShellWrapper, StringComparison.Ordinal);
        Assert.Contains("dotnet", powerShellWrapper, StringComparison.Ordinal);
        Assert.Contains("publish", packScript, StringComparison.Ordinal);
        Assert.Contains("src/CARVES.Runtime.Cli/carves.csproj", packScript, StringComparison.Ordinal);
        Assert.Contains("published_cli_entry", packScript, StringComparison.Ordinal);
        Assert.Contains("runtime-cli", packScript, StringComparison.Ordinal);
        Assert.Contains("UseAppHost=false", packScript, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDocs_KeepCompatibilityHostSeparateFromPublicMatrixStory()
    {
        var repoRoot = LocateSourceRepoRoot();
        var matrixReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "README.md"));
        var publicBoundary = File.ReadAllText(Path.Combine(repoRoot, "docs", "matrix", "public-boundary.md"));

        foreach (var doc in new[] { matrixReadme, publicBoundary })
        {
            Assert.Contains("compatibility", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reference host", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Runtime internal governance", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("do not need Runtime task/card governance", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PublicCliDocs_PinSecondLayerErgonomicsCleanup()
    {
        var repoRoot = LocateSourceRepoRoot();
        var guardReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "guard", "README.md"));
        var handoffReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "handoff", "README.md"));
        var auditReadme = File.ReadAllText(Path.Combine(repoRoot, "docs", "audit", "README.md"));

        Assert.Contains("Standalone `run` boundary", guardReadme, StringComparison.Ordinal);
        Assert.Contains("requires the CARVES Runtime host", guardReadme, StringComparison.Ordinal);
        Assert.Contains("use `carves-guard check`", guardReadme, StringComparison.Ordinal);

        Assert.Contains("nearest git repository", handoffReadme, StringComparison.Ordinal);
        Assert.Contains("--repo-root <path>", handoffReadme, StringComparison.Ordinal);

        Assert.Contains("AuditService", auditReadme, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditAlphaService", auditReadme, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicSurfaceSizeAdvisory_IsReviewSignalNotGate()
    {
        var repoRoot = LocateSourceRepoRoot();
        var advisory = File.ReadAllText(Path.Combine(repoRoot, "docs", "guides", "public-surface-size-advisory.md"));
        var index = File.ReadAllText(Path.Combine(repoRoot, "docs", "INDEX.md"));

        Assert.Contains("review signal", advisory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a CI gate", advisory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fixed line-count failure threshold", advisory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("responsibility boundaries", advisory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not split solely to satisfy a number", advisory, StringComparison.Ordinal);
        Assert.Contains("guides/public-surface-size-advisory.md", index, StringComparison.Ordinal);
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

    private sealed class RuntimeWrapperGitSandbox : IDisposable
    {
        private RuntimeWrapperGitSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static RuntimeWrapperGitSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "carves-runtime-wrapper-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var sandbox = new RuntimeWrapperGitSandbox(root);
            sandbox.RunGit("init");
            sandbox.RunGit("config", "user.email", "runtime-wrapper-test@example.invalid");
            sandbox.RunGit("config", "user.name", "Runtime Wrapper Test");
            return sandbox;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(RootPath, recursive: true);
            }
            catch
            {
            }
        }

        private void RunGit(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = RootPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");
            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {stdout}{stderr}");
            }
        }
    }
}
