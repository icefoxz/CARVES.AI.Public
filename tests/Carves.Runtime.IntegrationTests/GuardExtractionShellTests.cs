using System.Diagnostics;
using System.Text.Json;
using Carves.Guard.Core;

namespace Carves.Runtime.IntegrationTests;

public sealed class GuardExtractionShellTests
{
    [Fact]
    public void GuardExtractionProjects_ExistAndRuntimeWrapperDelegates()
    {
        var repoRoot = LocateSourceRepoRoot();
        var guardCoreProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Guard.Core", "Carves.Guard.Core.csproj"));
        var guardCliProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Guard.Cli", "Carves.Guard.Cli.csproj"));
        var runtimeProject = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "carves.csproj"));
        var runtimeWrapper = File.ReadAllText(Path.Combine(repoRoot, "src", "CARVES.Runtime.Cli", "FriendlyCliApplication.Guard.cs"));
        var guardSmoke = File.ReadAllText(Path.Combine(repoRoot, "scripts", "guard", "guard-packaged-install-smoke.ps1"));
        var publishReadiness = File.ReadAllText(Path.Combine(repoRoot, "scripts", "release", "github-publish-readiness.ps1"));

        Assert.Contains("<PackageId>CARVES.Guard.Core</PackageId>", guardCoreProject, StringComparison.Ordinal);
        Assert.Contains("<PackageId>CARVES.Guard.Cli</PackageId>", guardCliProject, StringComparison.Ordinal);
        Assert.Contains("<ToolCommandName>carves-guard</ToolCommandName>", guardCliProject, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Core", runtimeProject, StringComparison.Ordinal);
        Assert.Contains("GuardCliRunner.Run", runtimeWrapper, StringComparison.Ordinal);
        Assert.Contains("RuntimeGuardTaskRunner", runtimeWrapper, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Cli", guardSmoke, StringComparison.Ordinal);
        Assert.Contains("carves-guard", guardSmoke, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Core", publishReadiness, StringComparison.Ordinal);
        Assert.Contains("CARVES.Guard.Cli", publishReadiness, StringComparison.Ordinal);
    }

    [Fact]
    public void StandaloneGuardRunner_ChecksDiffWithoutRuntimeTaskTruth()
    {
        using var repo = GuardGitSandbox.Create();
        repo.WriteFile("src/todo.ts", "export const todos = [];\n");
        repo.WriteFile("tests/todo.test.ts", "test('baseline', () => expect(true).toBe(true));\n");

        var init = RunGuardInDirectory(repo.RootPath, "init", "--json");
        repo.CommitAll("baseline");
        repo.AppendFile("src/todo.ts", "export function countTodos() { return todos.length; }\n");
        repo.WriteFile("tests/todo-count.test.ts", "test('count', () => expect(0).toBe(0));\n");
        var check = RunGuardInDirectory(repo.RootPath, "check", "--json");

        Assert.Equal(0, init.ExitCode);
        using var initDocument = JsonDocument.Parse(init.StandardOutput);
        Assert.Equal("guard-init.v1", initDocument.RootElement.GetProperty("schema_version").GetString());
        Assert.Equal("carves-guard check", initDocument.RootElement.GetProperty("next_command").GetString());

        Assert.Equal(0, check.ExitCode);
        using var checkDocument = JsonDocument.Parse(check.StandardOutput);
        var root = checkDocument.RootElement;
        Assert.Equal("guard-check.v1", root.GetProperty("schema_version").GetString());
        Assert.Equal("allow", root.GetProperty("decision").GetString());
        Assert.False(root.GetProperty("requires_runtime_task_truth").GetBoolean());
    }

    [Fact]
    public void StandaloneGuardRunner_RunClearlyRequiresRuntimeHost()
    {
        using var repo = GuardGitSandbox.Create();

        var help = RunGuardInDirectory(repo.RootPath, "help");
        var run = RunGuardInDirectory(repo.RootPath, "run", "T-GUARD-001", "--json");

        Assert.Equal(0, help.ExitCode);
        Assert.Contains("requires CARVES Runtime host", help.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(2, run.ExitCode);
        Assert.Empty(run.StandardOutput);
        Assert.Contains("requires the CARVES Runtime host", run.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves guard run <task-id>", run.StandardError, StringComparison.Ordinal);
        Assert.Contains("carves-guard check", run.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeTaskRunnerUnavailable", run.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime_task_runner_unavailable", run.StandardError, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardCliRunner_IsDecomposedIntoCommandAndOutputComponents()
    {
        var repoRoot = LocateSourceRepoRoot();
        var coreRoot = Path.Combine(repoRoot, "src", "CARVES.Guard.Core");
        var expectedFiles = new[]
        {
            "GuardCliRunner.cs",
            "GuardCliCommands.cs",
            "GuardCliTextOutput.cs",
            "GuardCliJsonContracts.cs",
            "GuardCliOptions.cs",
            "GuardCliUsage.cs",
            "GuardCliRuntimeFallback.cs",
        };

        foreach (var file in expectedFiles)
        {
            var path = Path.Combine(coreRoot, file);
            Assert.True(File.Exists(path), $"Missing Guard CLI component: {file}");
            Assert.Contains("partial class GuardCliRunner", File.ReadAllText(path), StringComparison.Ordinal);
        }

        Assert.True(File.ReadAllLines(Path.Combine(coreRoot, "GuardCliRunner.cs")).Length < 140);
        Assert.Contains("RunGuardCheck", File.ReadAllText(Path.Combine(coreRoot, "GuardCliCommands.cs")), StringComparison.Ordinal);
        Assert.Contains("WriteGuardRunText", File.ReadAllText(Path.Combine(coreRoot, "GuardCliTextOutput.cs")), StringComparison.Ordinal);
        Assert.Contains("ToJsonContract", File.ReadAllText(Path.Combine(coreRoot, "GuardCliJsonContracts.cs")), StringComparison.Ordinal);
    }

    private static CliResult RunGuardInDirectory(string workingDirectory, params string[] arguments)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        try
        {
            Directory.SetCurrentDirectory(workingDirectory);
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = GuardCliRunner.Run(arguments);
            return new CliResult(exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Directory.SetCurrentDirectory(originalDirectory);
        }
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

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class GuardGitSandbox : IDisposable
    {
        private GuardGitSandbox(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; }

        public static GuardGitSandbox Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "carves-guard-extraction-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var sandbox = new GuardGitSandbox(root);
            sandbox.RunGit("init");
            sandbox.RunGit("config", "user.email", "guard-extraction-test@example.invalid");
            sandbox.RunGit("config", "user.name", "Guard Extraction Test");
            return sandbox;
        }

        public void WriteFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public void AppendFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.AppendAllText(fullPath, content);
        }

        public void CommitAll(string message)
        {
            RunGit("add", ".");
            RunGit("commit", "-m", message);
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
