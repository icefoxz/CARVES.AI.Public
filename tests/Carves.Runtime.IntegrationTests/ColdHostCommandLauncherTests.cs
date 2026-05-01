using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Carves.Runtime.IntegrationTests;

public sealed class ColdHostCommandLauncherTests
{
    [Fact]
    public void ColdLauncher_StatusSucceedsWhenRepoObjLockPathIsOccupied()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var lockedBinaryPath = Path.Combine(
            sandbox.RootPath,
            "src",
            "CARVES.Runtime.Domain",
            "obj",
            "Debug",
            "net10.0",
            "Carves.Runtime.Domain.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(lockedBinaryPath)!);
        File.WriteAllText(lockedBinaryPath, "locked by integration test");

        using var locker = LockedFileProcess.Start(lockedBinaryPath);

        var result = RunColdLauncher(
            sandbox.RootPath,
            "--repo-root",
            sandbox.RootPath,
            "status",
            "--summary");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("No resident host is running; executing `status` through the cold path.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Recommended next action:", result.StandardOutput, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(result.StandardOutput));
        Assert.True(locker.IsAlive, result.CombinedOutput);
    }

    [Fact]
    public void ColdLauncher_StatusConflictFallsBackWithReconcileGuidance()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        var runtimeDirectory = ResolveHostRuntimeDirectory(sandbox.RootPath);
        var staleDeploymentDirectory = Path.Combine(runtimeDirectory, "deployments", "cold-status-conflict");
        Directory.CreateDirectory(staleDeploymentDirectory);
        var staleExecutablePath = Path.Combine(staleDeploymentDirectory, "Carves.Runtime.Host.dll");
        File.WriteAllText(staleExecutablePath, "stale host");
        var stalePort = ReserveLoopbackPort();

        using var sleeper = LongRunningProcess.Start();
        WriteHostDescriptor(
            sandbox.RootPath,
            sleeper.ProcessId,
            runtimeDirectory,
            staleDeploymentDirectory,
            staleExecutablePath,
            $"http://127.0.0.1:{stalePort}",
            stalePort);

        var result = RunColdLauncher(
            sandbox.RootPath,
            "--repo-root",
            sandbox.RootPath,
            "status",
            "--summary");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Resident host session conflict is present; executing `status` through the cold path.", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("carves host reconcile --replace-stale --json", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void ColdLauncher_AttachMaterializesExistingBootstrapTruthSurfaces()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        targetRepo.WriteFile("src/PackagedTrial.cs", "namespace Packaged.Trial; public sealed class PackagedTrialService { }");
        targetRepo.CommitAll("Initial commit");

        var attach = RunColdLauncher(
            sandbox.RootPath,
            "--repo-root",
            sandbox.RootPath,
            "attach",
            targetRepo.RootPath,
            "--repo-id",
            "repo-cold-packaging");

        var systemConfigPath = Path.Combine(targetRepo.RootPath, ".ai", "config", "system.json");
        var manifestPath = Path.Combine(targetRepo.RootPath, ".ai", "runtime.json");
        var handshakePath = Path.Combine(targetRepo.RootPath, ".ai", "runtime", "attach-handshake.json");
        var agentBootstrapPath = Path.Combine(targetRepo.RootPath, ".ai", "AGENT_BOOTSTRAP.md");
        var rootAgentsPath = Path.Combine(targetRepo.RootPath, "AGENTS.md");

        Assert.Equal(0, attach.ExitCode);
        Assert.Contains("CARVES runtime attached.", attach.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Attach mode: fresh_init", attach.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Readiness: ready", attach.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Runtime start deferred. The repo is ready for operator start.", attach.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(systemConfigPath));
        Assert.True(File.Exists(manifestPath));
        Assert.True(File.Exists(handshakePath));
        Assert.True(File.Exists(agentBootstrapPath));
        Assert.True(File.Exists(rootAgentsPath));
        var agentBootstrap = File.ReadAllText(agentBootstrapPath);
        var rootAgents = File.ReadAllText(rootAgentsPath);
        Assert.Contains("agent start --json", agentBootstrap, StringComparison.Ordinal);
        Assert.Contains("pilot status --json", agentBootstrap, StringComparison.Ordinal);
        Assert.Contains("current_stage_id", agentBootstrap, StringComparison.Ordinal);
        Assert.Contains("next_governed_command", agentBootstrap, StringComparison.Ordinal);
        Assert.Contains("Do not edit `.ai/` official truth manually", agentBootstrap, StringComparison.Ordinal);
        Assert.Contains(RuntimeCliWrapperName(), agentBootstrap, StringComparison.Ordinal);
        Assert.Contains(".ai/AGENT_BOOTSTRAP.md", rootAgents, StringComparison.Ordinal);
        Assert.Contains("agent start --json", rootAgents, StringComparison.Ordinal);
    }

    [Fact]
    public void ColdLauncher_AttachDoesNotOverwriteExistingRootAgents()
    {
        using var sandbox = RepoSandbox.CreateFromCurrentRepo();
        using var targetRepo = GitSandbox.Create();
        const string existingAgents = "# Target Agents\n\nTarget-owned agent instructions must survive attach.\n";
        targetRepo.WriteFile("AGENTS.md", existingAgents);
        targetRepo.WriteFile("src/PackagedTrial.cs", "namespace Packaged.Trial; public sealed class PackagedTrialService { }");
        targetRepo.CommitAll("Initial commit");

        var attach = RunColdLauncher(
            sandbox.RootPath,
            "--repo-root",
            sandbox.RootPath,
            "attach",
            targetRepo.RootPath,
            "--repo-id",
            "repo-cold-packaging-existing-agents");

        var agentBootstrapPath = Path.Combine(targetRepo.RootPath, ".ai", "AGENT_BOOTSTRAP.md");
        var rootAgentsPath = Path.Combine(targetRepo.RootPath, "AGENTS.md");

        Assert.Equal(0, attach.ExitCode);
        Assert.Contains("CARVES runtime attached.", attach.StandardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(agentBootstrapPath));
        Assert.Equal(existingAgents, File.ReadAllText(rootAgentsPath));
        Assert.Contains("pilot status --json", File.ReadAllText(agentBootstrapPath), StringComparison.Ordinal);
    }

    private static ProgramRunResult RunColdLauncher(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolvePowerShellExecutable(),
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(Path.Combine(workingDirectory, "scripts", "carves-host.ps1"));
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start cold host launcher.");
        using var standardOutputClosed = new ManualResetEventSlim();
        using var standardErrorClosed = new ManualResetEventSlim();
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendProcessLine(standardOutput, standardOutputClosed, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => AppendProcessLine(standardError, standardErrorClosed, eventArgs.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(180000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch
            {
            }

            throw new TimeoutException("Cold host launcher did not exit within the expected timeout.");
        }

        standardOutputClosed.Wait(TimeSpan.FromSeconds(5));
        standardErrorClosed.Wait(TimeSpan.FromSeconds(5));
        return new ProgramRunResult(process.ExitCode, standardOutput.ToString(), standardError.ToString());
    }

    private static string ResolveHostRuntimeDirectory(string repoRoot)
    {
        var repoHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(repoRoot).ToUpperInvariant())))
            .ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "carves-runtime-host", repoHash);
    }

    private static void WriteHostDescriptor(
        string repoRoot,
        int processId,
        string runtimeDirectory,
        string deploymentDirectory,
        string executablePath,
        string baseUrl,
        int port)
    {
        var descriptorDirectory = Path.Combine(repoRoot, ".carves-platform", "host");
        Directory.CreateDirectory(descriptorDirectory);
        var descriptorPath = Path.Combine(descriptorDirectory, "descriptor.json");
        var now = DateTimeOffset.UtcNow;
        var payload = $$"""
        {
          "repo_root": "{{Path.GetFullPath(repoRoot)}}",
          "runtime_directory": "{{runtimeDirectory}}",
          "deployment_directory": "{{deploymentDirectory}}",
          "executable_path": "{{executablePath}}",
          "base_url": "{{baseUrl}}",
          "port": {{port}},
          "process_id": {{processId}},
          "started_at": "{{now:O}}",
          "version": "test",
          "stage": "integration"
        }
        """;
        File.WriteAllText(descriptorPath, payload);
    }

    private static int ReserveLoopbackPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string RuntimeCliWrapperName()
    {
        return OperatingSystem.IsWindows() ? "carves.ps1" : "carves";
    }

    private static string ResolvePowerShellExecutable()
    {
        return OperatingSystem.IsWindows() ? "powershell" : "pwsh";
    }

    private sealed record ProgramRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }

    private static void AppendProcessLine(StringBuilder builder, ManualResetEventSlim completed, string? line)
    {
        if (line is null)
        {
            completed.Set();
            return;
        }

        builder.AppendLine(line);
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
            var rootPath = Path.Combine(Path.GetTempPath(), "carves-runtime-cold-launcher", Guid.NewGuid().ToString("N"));
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

    private sealed class LockedFileProcess : IDisposable
    {
        private readonly Process process;

        private LockedFileProcess(Process process)
        {
            this.process = process;
        }

        public bool IsAlive
        {
            get
            {
                try
                {
                    return !process.HasExited;
                }
                catch
                {
                    return false;
                }
            }
        }

        public static LockedFileProcess Start(string filePath)
        {
            var escapedPath = filePath.Replace("'", "''", StringComparison.Ordinal);
            var command = "$path = '" + escapedPath + "'; "
                + "$stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None); "
                + "try { Start-Sleep -Seconds 120 } finally { $stream.Dispose() }";
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePowerShellExecutable(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(command);

            var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start locking PowerShell process.");
            WaitForExclusiveLock(filePath, process);
            return new LockedFileProcess(process);
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
            }

            process.Dispose();
        }

        private static void WaitForExclusiveLock(string filePath, Process process)
        {
            var acquired = IntegrationTestWait.WaitUntil(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(10),
                () =>
                {
                    if (process.HasExited)
                    {
                        throw new InvalidOperationException($"Locking PowerShell process exited early: {process.StandardError.ReadToEnd()}");
                    }

                    try
                    {
                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        return false;
                    }
                    catch (IOException)
                    {
                        return true;
                    }
                });

            if (!acquired)
            {
                throw new InvalidOperationException("Timed out waiting for the cold-command lock path to become active.");
            }
        }
    }

    private sealed class LongRunningProcess : IDisposable
    {
        private readonly Process process;

        private LongRunningProcess(Process process)
        {
            this.process = process;
        }

        public int ProcessId => process.Id;

        public static LongRunningProcess Start()
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                ArgumentList = { "-c", "sleep 300" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("Failed to start long-running process.");
            return new LongRunningProcess(process);
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
                catch
                {
                }
            }

            process.Dispose();
        }
    }
}
