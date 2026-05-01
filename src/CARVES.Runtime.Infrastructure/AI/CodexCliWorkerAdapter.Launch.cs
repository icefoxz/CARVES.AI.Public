using System.Diagnostics;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static ProcessStartInfo BuildStartInfo(CodexCommand command, WorkerExecutionRequest request, DelegatedWorkerLaunchContract launchContract)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            WorkingDirectory = request.WorktreeRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in command.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--json");
        startInfo.ArgumentList.Add("--skip-git-repo-check");
        startInfo.ArgumentList.Add("--color");
        startInfo.ArgumentList.Add("never");
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(request.WorktreeRoot);
        startInfo.ArgumentList.Add("--sandbox");
        startInfo.ArgumentList.Add(MapSandboxMode(request.Profile.SandboxMode));
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add($"approval_policy={MapApprovalMode(request.Profile.ApprovalMode)}");
        var reasoningEffort = NormalizeReasoningEffort(request.ReasoningEffort);
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
        {
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add($"model_reasoning_effort=\"{reasoningEffort}\"");
        }

        if (ShouldPassModelOverride(request.ModelOverride))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(request.ModelOverride!);
        }

        if (!string.Equals(Path.GetFullPath(request.RepoRoot), Path.GetFullPath(request.WorktreeRoot), StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("--add-dir");
            startInfo.ArgumentList.Add(request.RepoRoot);
        }

        foreach (var additionalDirectory in ResolveAdditionalDirectories(request, launchContract))
        {
            startInfo.ArgumentList.Add("--add-dir");
            startInfo.ArgumentList.Add(additionalDirectory);
        }

        ApplyLaunchEnvironment(startInfo, launchContract);
        startInfo.ArgumentList.Add("-");
        return startInfo;
    }

    private static bool ShouldPassModelOverride(string? modelOverride)
    {
        if (string.IsNullOrWhiteSpace(modelOverride))
        {
            return false;
        }

        return !string.Equals(modelOverride, "codex-cli", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(modelOverride, "codex-default", StringComparison.OrdinalIgnoreCase);
    }

    private static DelegatedWorkerLaunchContract BuildLaunchContract(WorkerExecutionRequest request)
    {
        var explicitHome = Environment.GetEnvironmentVariable("CARVES_DELEGATED_WORKER_HOME_ROOT");
        var homeRoot = string.IsNullOrWhiteSpace(explicitHome)
            ? Path.Combine(Path.GetTempPath(), "carves-delegated-worker", SanitizePathSegment(request.TaskId))
            : Path.GetFullPath(explicitHome);
        var explicitDotNetHome = Environment.GetEnvironmentVariable("CARVES_DOTNET_CLI_HOME");
        var dotNetCliHome = string.IsNullOrWhiteSpace(explicitDotNetHome)
            ? Path.Combine(homeRoot, "dotnet")
            : Path.GetFullPath(explicitDotNetHome);
        var tempRoot = Path.Combine(homeRoot, "tmp");
        var toolRoot = Path.Combine(homeRoot, "tools");
        var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_HOME"] = dotNetCliHome,
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false",
            ["DOTNET_ADD_GLOBAL_TOOLS_TO_PATH"] = "false",
            ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1",
            ["TMP"] = tempRoot,
            ["TEMP"] = tempRoot,
            ["TMPDIR"] = tempRoot,
            ["CARVES_DELEGATED_WORKER_HOME"] = homeRoot,
        };
        if (request.ValidationCommands.Count > 0)
        {
            environmentVariables["CARVES_FORMAL_VALIDATION_OWNER"] = "runtime";
            environmentVariables["CARVES_DELEGATED_TOOL_ROOT"] = toolRoot;
            environmentVariables["CARVES_REAL_DOTNET_PATH"] = ResolveDotNetCommandPath();
            environmentVariables["CARVES_REAL_GIT_PATH"] = ResolveGitCommandPath();
            environmentVariables["PATH"] = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PATH"))
                ? toolRoot
                : string.Join(Path.PathSeparator, toolRoot, Environment.GetEnvironmentVariable("PATH"));
        }

        var nuGetPackages = ResolveNuGetPackagesPath();
        if (!string.IsNullOrWhiteSpace(nuGetPackages))
        {
            environmentVariables["NUGET_PACKAGES"] = nuGetPackages;
        }

        return new DelegatedWorkerLaunchContract
        {
            TaskId = request.TaskId,
            BackendId = "codex_cli",
            WorktreeRoot = request.WorktreeRoot,
            RuntimeHomeRoot = homeRoot,
            DotNetCliHome = dotNetCliHome,
            TempRoot = tempRoot,
            EnvironmentVariables = environmentVariables,
        };
    }

    private static CodexCommand? TryResolveCodexCommand()
    {
        var explicitPath = Environment.GetEnvironmentVariable("CARVES_CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return BuildCommand(explicitPath);
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidates = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(entry, "codex.exe"),
                    Path.Combine(entry, "codex.cmd"),
                    Path.Combine(entry, "codex.bat"),
                    Path.Combine(entry, "codex.ps1"),
                    Path.Combine(entry, "codex"),
                }
                : new[] { Path.Combine(entry, "codex") };

            var match = candidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(match))
            {
                return BuildCommand(match);
            }
        }

        return null;
    }

    private static CodexCommand? BuildCommand(string commandPath)
    {
        if (!File.Exists(commandPath))
        {
            return null;
        }

        var extension = Path.GetExtension(commandPath);
        if (OperatingSystem.IsWindows())
        {
            if (string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            {
                return new CodexCommand("powershell", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", commandPath], commandPath);
            }

            if (string.Equals(extension, ".cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".bat", StringComparison.OrdinalIgnoreCase))
            {
                return new CodexCommand("cmd.exe", ["/c", commandPath], commandPath);
            }
        }

        return new CodexCommand(commandPath, Array.Empty<string>(), commandPath);
    }

    private static ProbeResult RunProbe(CodexCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in command.PrefixArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("--version");
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return new ProbeResult(-1, string.Empty, "Failed to start Codex CLI.");
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProbeResult(process.ExitCode, stdout, stderr);
    }
}
