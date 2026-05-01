using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.IntegrationTests;

public sealed class AttachSurfaceTests
{
    [Fact]
    public void Attach_InitializesFreshRepoAndRegistersIt()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-attached");
        var registryPath = Path.Combine(sandbox.RootPath, ".carves-platform", "repos", "registry.json");

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("CARVES runtime attached.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Attached repo repo-attached.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Protocol mode: carves_development", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Project understanding: fresh (refreshed)", result.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "config", "system.json")));
        Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "codegraph", "index.json")));
        Assert.True(File.Exists(Path.Combine(targetRepo.RootPath, ".ai", "opportunities", "index.json")));
        Assert.Contains("repo-attached", File.ReadAllText(registryPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Attach_ReusesAndRefreshesProjectUnderstandingDependingOnCodegraphFreshness()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");

        var first = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-understanding");
        Assert.Equal(0, first.ExitCode);
        Assert.Contains("Project understanding: fresh (refreshed)", first.StandardOutput, StringComparison.Ordinal);
        targetRepo.CommitAll("Commit CARVES attach artifacts");

        var second = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-understanding");
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleRegistry { }");
        File.SetLastWriteTimeUtc(Path.Combine(targetRepo.RootPath, "src", "Sample.cs"), DateTime.UtcNow.AddSeconds(1));
        targetRepo.CommitAll("Update sample");
        var third = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-understanding");

        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Project understanding: fresh (reused)", second.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(0, third.ExitCode);
        Assert.Contains("Project understanding: fresh (refreshed)", third.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void Attach_InitializesFreshRepoWithoutTopLevelSrcAndDiscoversCodeDirectories()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("App/MainView.cs", "namespace Attached.App; public sealed class MainView { }");
        targetRepo.WriteFile("Lib/SharedService.cs", "namespace Attached.Lib; public sealed class SharedService { }");
        targetRepo.WriteFile("tests/AppTests.cs", "namespace Attached.Tests; public sealed class AppTests { }");
        targetRepo.CommitAll("Initial commit");

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-no-src");
        var systemConfigPath = Path.Combine(targetRepo.RootPath, ".ai", "config", "system.json");
        var manifestPath = Path.Combine(targetRepo.RootPath, ".ai", "codegraph", "manifest.json");

        Assert.True(result.ExitCode == 0, result.CombinedOutput);
        Assert.Contains("CARVES runtime attached.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Project understanding: fresh (refreshed)", result.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(systemConfigPath));
        Assert.True(File.Exists(manifestPath));

        using var systemConfig = JsonDocument.Parse(File.ReadAllText(systemConfigPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var codeDirectories = systemConfig.RootElement.GetProperty("code_directories")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        Assert.Contains("App", codeDirectories, StringComparer.Ordinal);
        Assert.Contains("Lib", codeDirectories, StringComparer.Ordinal);
        Assert.Contains("tests", codeDirectories, StringComparer.Ordinal);
        Assert.DoesNotContain("src", codeDirectories, StringComparer.OrdinalIgnoreCase);
        Assert.True(manifest.RootElement.GetProperty("module_count").GetInt32() >= 2);
    }

    [Fact]
    public void Attach_RegistersStableRepoRuntimeIdentityInFleetRegistry()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");

        var first = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-runtime-identity");
        Assert.Equal(0, first.ExitCode);
        var firstEntry = Assert.Single(ReadRepoRuntimeRegistry(sandbox.RootPath));
        var firstRepoId = firstEntry.RepoId;
        targetRepo.CommitAll("Commit CARVES attach artifacts");
        var second = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-runtime-identity");
        var secondEntry = Assert.Single(ReadRepoRuntimeRegistry(sandbox.RootPath));

        Assert.Equal(0, second.ExitCode);
        Assert.StartsWith("repo-", firstEntry.RepoId, StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(targetRepo.RootPath), firstEntry.RepoPath);
        Assert.False(string.IsNullOrWhiteSpace(firstEntry.HostId));
        Assert.Equal("idle", firstEntry.Status);
        Assert.Equal(firstRepoId, secondEntry.RepoId);
        Assert.Equal(firstEntry.HostId, secondEntry.HostId);
        Assert.Equal("idle", secondEntry.Status);
        Assert.True(secondEntry.LastSeen >= firstEntry.LastSeen);
    }

    [Fact]
    public void RuntimeStartAndStop_UpdateRepoRuntimeStatusWithoutDuplicatingIdentity()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");

        var attach = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-runtime-status");
        var attachedEntry = Assert.Single(ReadRepoRuntimeRegistry(sandbox.RootPath));
        var start = ProgramHarness.Run("--repo-root", sandbox.RootPath, "runtime", "start", "repo-runtime-status", "--dry-run");
        var activeEntry = Assert.Single(ReadRepoRuntimeRegistry(sandbox.RootPath));
        var stop = ProgramHarness.Run("--repo-root", sandbox.RootPath, "runtime", "stop", "repo-runtime-status", "repo runtime identity validation");
        var idleEntry = Assert.Single(ReadRepoRuntimeRegistry(sandbox.RootPath));

        Assert.Equal(0, attach.ExitCode);
        Assert.Equal(0, start.ExitCode);
        Assert.Equal(0, stop.ExitCode);
        Assert.Equal(attachedEntry.RepoId, activeEntry.RepoId);
        Assert.Equal(attachedEntry.RepoId, idleEntry.RepoId);
        Assert.Equal("active", activeEntry.Status);
        Assert.Equal("idle", idleEntry.Status);
        Assert.True(activeEntry.LastSeen >= attachedEntry.LastSeen);
        Assert.True(idleEntry.LastSeen >= activeEntry.LastSeen);
    }

    [Fact]
    public void Attach_RejectsDirtyTargetRepo()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleService { }");
        targetRepo.CommitAll("Initial commit");
        targetRepo.WriteFile("src/Sample.cs", "namespace Attached.Sample; public sealed class SampleRegistry { }");

        var result = ProgramHarness.Run("--repo-root", sandbox.RootPath, "attach", targetRepo.RootPath, "--repo-id", "repo-dirty");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("has uncommitted changes", result.StandardError, StringComparison.Ordinal);
    }

    private sealed class GitSandbox : IDisposable
    {
        private GitSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static GitSandbox Create()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-runtime-attach", Guid.NewGuid().ToString("N"));
            GitTestHarness.InitializeRepository(rootPath);
            return new GitSandbox(rootPath);
        }

        public void WriteFile(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void CommitAll(string message)
        {
            RunGit(RootPath, "add", ".");
            RunGit(RootPath, "commit", "-m", message);
        }

        public void Dispose()
        {
            if (!Directory.Exists(RootPath))
            {
                return;
            }

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Directory.Delete(RootPath, true);
                    return;
                }
                catch (UnauthorizedAccessException) when (attempt < 4)
                {
                    IntegrationTestWait.Delay(TimeSpan.FromMilliseconds(100));
                }
                catch (IOException) when (attempt < 4)
                {
                    IntegrationTestWait.Delay(TimeSpan.FromMilliseconds(100));
                }
                catch
                {
                    return;
                }
            }

            try
            {
                Directory.Delete(RootPath, true);
            }
            catch
            {
            }
        }

        private static void RunGit(string workingDirectory, params string[] arguments)
        {
            GitTestHarness.Run(workingDirectory, arguments);
        }
    }

    private static IReadOnlyList<RepoRuntimeRegistryEntrySnapshot> ReadRepoRuntimeRegistry(string repoRoot)
    {
        var path = ControlPlanePaths.FromRepoRoot(repoRoot).PlatformRepoRuntimeRegistryLiveStateFile;
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => new RepoRuntimeRegistryEntrySnapshot(
                item.GetProperty("repo_id").GetString() ?? string.Empty,
                item.GetProperty("repo_path").GetString() ?? string.Empty,
                item.GetProperty("host_id").GetString() ?? string.Empty,
                item.GetProperty("status").GetString() ?? string.Empty,
                item.GetProperty("last_seen").GetDateTimeOffset()))
            .ToArray();
    }

    private sealed record RepoRuntimeRegistryEntrySnapshot(
        string RepoId,
        string RepoPath,
        string HostId,
        string Status,
        DateTimeOffset LastSeen);
}
