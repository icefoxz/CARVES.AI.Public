using System.Diagnostics;
using System.Text.Json;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Infrastructure.AI;

internal sealed partial class CodexCliWorkerAdapter
{
    private static PreflightResult RunPreflight(
        CodexCommand command,
        WorkerExecutionRequest request,
        DelegatedWorkerLaunchContract launchContract,
        string requestPreview,
        string requestHash)
    {
        var runId = $"worker-run-{Guid.NewGuid():N}";
        var events = new List<WorkerEvent>();
        var commandTrace = new List<CommandExecutionRecord>();
        var contractPayload = JsonSerializer.Serialize(new
        {
            schema_version = launchContract.SchemaVersion,
            worktree_root = launchContract.WorktreeRoot,
            runtime_home_root = launchContract.RuntimeHomeRoot,
            dotnet_cli_home = launchContract.DotNetCliHome,
            temp_root = launchContract.TempRoot,
        });
        events.Add(BuildEvent(
            request.TaskId,
            runId,
            WorkerEventType.RunStarted,
            $"Delegated worker launch contract prepared for {request.TaskId}.",
            "preflight_contract",
            null,
            0,
            contractPayload));

        if (!Directory.Exists(request.WorktreeRoot))
        {
            return BlockPreflight(
                request,
                requestPreview,
                requestHash,
                runId,
                events,
                commandTrace,
                "Delegated worker preflight failed: worktree directory does not exist.");
        }

        string? homeFailure = null;
        string? dotNetFailure = null;
        string? tempFailure = null;
        string? toolFailure = null;
        if (!TryEnsureWritableDirectory(launchContract.RuntimeHomeRoot, out homeFailure)
            || !TryEnsureWritableDirectory(launchContract.DotNetCliHome, out dotNetFailure)
            || !TryEnsureWritableDirectory(launchContract.TempRoot, out tempFailure)
            || !TryEnsureWritableDirectory(GetToolRoot(launchContract), out toolFailure))
        {
            return BlockPreflight(
                request,
                requestPreview,
                requestHash,
                runId,
                events,
                commandTrace,
                $"Delegated worker preflight failed: {FirstNonEmpty(homeFailure, dotNetFailure, tempFailure, toolFailure, "runtime directories are not writable.")}");
        }

        var toolingFailure = EnsureDelegatedTooling(launchContract);
        if (!string.IsNullOrWhiteSpace(toolingFailure))
        {
            return BlockPreflight(
                request,
                requestPreview,
                requestHash,
                runId,
                events,
                commandTrace,
                $"Delegated worker preflight failed: {toolingFailure}");
        }

        var codexProbe = RunCommand(command.FileName, command.PrefixArguments.Append("--version").ToArray(), request.WorktreeRoot, launchContract);
        commandTrace.Add(codexProbe.ToRecord(request.WorktreeRoot, "preflight"));
        events.Add(BuildEvent(
            request.TaskId,
            runId,
            WorkerEventType.CommandExecuted,
            "Delegated worker preflight probed Codex CLI availability.",
            "preflight",
            string.Join(' ', codexProbe.Command),
            codexProbe.ExitCode,
            codexProbe.RawPayload));
        if (codexProbe.ExitCode != 0)
        {
            return BlockPreflight(
                request,
                requestPreview,
                requestHash,
                runId,
                events,
                commandTrace,
                $"Delegated worker preflight failed: codex --version could not run. {FirstNonEmpty(codexProbe.StandardError, codexProbe.StandardOutput, "probe failed")}");
        }

        var dotnetProbe = RunCommand("dotnet", ["--info"], request.WorktreeRoot, launchContract);
        commandTrace.Add(dotnetProbe.ToRecord(request.WorktreeRoot, "preflight"));
        events.Add(BuildEvent(
            request.TaskId,
            runId,
            WorkerEventType.CommandExecuted,
            "Delegated worker preflight verified dotnet --info under the launch contract.",
            "preflight",
            string.Join(' ', dotnetProbe.Command),
            dotnetProbe.ExitCode,
            dotnetProbe.RawPayload));
        if (dotnetProbe.ExitCode != 0)
        {
            return BlockPreflight(
                request,
                requestPreview,
                requestHash,
                runId,
                events,
                commandTrace,
                $"Delegated worker preflight failed: dotnet --info did not succeed under the delegated launch environment. {FirstNonEmpty(dotnetProbe.StandardError, dotnetProbe.StandardOutput, "dotnet preflight failed")}");
        }

        return new PreflightResult(true, null, events.ToArray(), commandTrace.ToArray());
    }

    private static PreflightResult BlockPreflight(
        WorkerExecutionRequest request,
        string requestPreview,
        string requestHash,
        string runId,
        IReadOnlyList<WorkerEvent> events,
        IReadOnlyList<CommandExecutionRecord> commandTrace,
        string failureReason)
    {
        var result = WorkerExecutionResult.Blocked(
            request.TaskId,
            "codex_cli",
            "codex",
            nameof(CodexCliWorkerAdapter),
            request.Profile,
            WorkerFailureKind.EnvironmentBlocked,
            failureReason,
            requestPreview,
            requestHash,
            failureLayer: WorkerFailureLayer.Environment,
            protocolFamily: ProtocolFamily,
            requestFamily: RequestFamily,
            events: events,
            commandTrace: commandTrace) with
        {
            RunId = runId,
            Summary = failureReason,
            FailureReason = failureReason,
        };
        return new PreflightResult(false, result, events, commandTrace);
    }

    private static bool TryEnsureWritableDirectory(string path, out string? failure)
    {
        failure = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        try
        {
            if (File.Exists(path))
            {
                failure = $"{path} is a file, not a writable directory.";
                return false;
            }

            Directory.CreateDirectory(path);
            var probePath = Path.Combine(path, ".carves-write-test");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failure = $"{path} is not writable: {exception.Message}";
            return false;
        }
    }

    private static string? EnsureDelegatedTooling(DelegatedWorkerLaunchContract launchContract)
    {
        if (!IsRuntimeOwnedValidation(launchContract))
        {
            return null;
        }

        var toolRoot = GetToolRoot(launchContract);
        if (string.IsNullOrWhiteSpace(toolRoot))
        {
            return "delegated tool root is missing.";
        }

        try
        {
            Directory.CreateDirectory(toolRoot);
            var dotNetShimPath = OperatingSystem.IsWindows()
                ? Path.Combine(toolRoot, "dotnet.cmd")
                : Path.Combine(toolRoot, "dotnet");
            var gitShimPath = OperatingSystem.IsWindows()
                ? Path.Combine(toolRoot, "git.cmd")
                : Path.Combine(toolRoot, "git");
            File.WriteAllText(dotNetShimPath, BuildDotNetShimScript());
            File.WriteAllText(gitShimPath, BuildGitShimScript());
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(dotNetShimPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                File.SetUnixFileMode(gitShimPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"delegated tooling could not be prepared: {exception.Message}";
        }
    }

    private static ProbeCommandResult RunCommand(string fileName, IReadOnlyList<string> arguments, string workingDirectory, DelegatedWorkerLaunchContract launchContract)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            ApplyLaunchEnvironment(startInfo, launchContract);
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProbeCommandResult(arguments.Prepend(fileName).ToArray(), -1, string.Empty, "Failed to start probe process.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ProbeCommandResult(arguments.Prepend(fileName).ToArray(), process.ExitCode, stdout, stderr);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ProbeCommandResult(arguments.Prepend(fileName).ToArray(), -1, string.Empty, exception.Message);
        }
    }

    private static void ApplyLaunchEnvironment(ProcessStartInfo startInfo, DelegatedWorkerLaunchContract launchContract)
    {
        foreach (var pair in launchContract.EnvironmentVariables)
        {
            startInfo.Environment[pair.Key] = pair.Value;
        }
    }

    private static bool IsRuntimeOwnedValidation(DelegatedWorkerLaunchContract launchContract)
    {
        return launchContract.EnvironmentVariables.TryGetValue("CARVES_FORMAL_VALIDATION_OWNER", out var owner)
               && string.Equals(owner, "runtime", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetToolRoot(DelegatedWorkerLaunchContract launchContract)
    {
        return launchContract.EnvironmentVariables.TryGetValue("CARVES_DELEGATED_TOOL_ROOT", out var toolRoot)
            ? toolRoot
            : string.Empty;
    }

    private static IEnumerable<string> ResolveAdditionalDirectories(WorkerExecutionRequest request, DelegatedWorkerLaunchContract launchContract)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(request.WorktreeRoot) };

        if (!string.IsNullOrWhiteSpace(request.RepoRoot))
        {
            seen.Add(Path.GetFullPath(request.RepoRoot));
        }

        foreach (var candidate in new[]
                 {
                     launchContract.RuntimeHomeRoot,
                     launchContract.DotNetCliHome,
                     launchContract.TempRoot,
                     ResolveNuGetPackagesPath(),
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (!seen.Add(fullPath))
            {
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static string? ResolveNuGetPackagesPath()
    {
        var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return null;
        }

        return Path.Combine(userProfile, ".nuget", "packages");
    }

    private static string ResolveDotNetCommandPath()
    {
        foreach (var candidate in new[]
                 {
                     Environment.GetEnvironmentVariable("DOTNET_HOST_PATH"),
                     Environment.ProcessPath,
                     ResolveDotNetFromRoot(Environment.GetEnvironmentVariable("DOTNET_ROOT_X64")),
                     ResolveDotNetFromRoot(Environment.GetEnvironmentVariable("DOTNET_ROOT")),
                     ResolveDotNetFromPath(),
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return "dotnet";
    }

    private static string ResolveGitCommandPath()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var candidate in OperatingSystem.IsWindows()
                         ? new[] { Path.Combine(entry, "git.exe"), Path.Combine(entry, "git.cmd"), Path.Combine(entry, "git.bat") }
                         : new[] { Path.Combine(entry, "git") })
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return "git";
    }

    private static string? ResolveDotNetFromRoot(string? dotNetRoot)
    {
        if (string.IsNullOrWhiteSpace(dotNetRoot))
        {
            return null;
        }

        return OperatingSystem.IsWindows()
            ? Path.Combine(dotNetRoot, "dotnet.exe")
            : Path.Combine(dotNetRoot, "dotnet");
    }

    private static string? ResolveDotNetFromPath()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var entry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = OperatingSystem.IsWindows()
                ? Path.Combine(entry, "dotnet.exe")
                : Path.Combine(entry, "dotnet");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string BuildDotNetShimScript()
    {
        return OperatingSystem.IsWindows()
            ? """
@echo off
setlocal
set "_real=%CARVES_REAL_DOTNET_PATH%"
if "%_real%"=="" set "_real=dotnet"
if /I "%~1"=="restore" goto runtime_owned
if /I "%~1"=="build" goto runtime_owned
if /I "%~1"=="test" goto runtime_owned
"%_real%" %*
exit /b %ERRORLEVEL%

:runtime_owned
echo CARVES delegated worker skipped dotnet %~1 because formal validation is executed by CARVES after the worker returns.
exit /b 0
"""
            : """
#!/usr/bin/env sh
real_dotnet="${CARVES_REAL_DOTNET_PATH:-dotnet}"
case "$1" in
  restore|build|test)
    echo "CARVES delegated worker skipped dotnet $1 because formal validation is executed by CARVES after the worker returns."
    exit 0
    ;;
  *)
    exec "$real_dotnet" "$@"
    ;;
esac
""";
    }

    private static string BuildGitShimScript()
    {
        return OperatingSystem.IsWindows()
            ? """
@echo off
setlocal
set "_real=%CARVES_REAL_GIT_PATH%"
if "%_real%"=="" set "_real=git"
if exist ".git" goto passthrough
if /I "%~1"=="status" goto managed_snapshot
if /I "%~1"=="diff" goto managed_snapshot
"%_real%" %*
exit /b %ERRORLEVEL%

:managed_snapshot
echo CARVES delegated worker is running in a managed worktree snapshot without .git metadata; skip git %~1 and rely on direct file inspection.
exit /b 0

:passthrough
"%_real%" %*
exit /b %ERRORLEVEL%
"""
            : """
#!/usr/bin/env sh
real_git="${CARVES_REAL_GIT_PATH:-git}"
if [ -e .git ]; then
  exec "$real_git" "$@"
fi
case "$1" in
  status|diff)
    echo "CARVES delegated worker is running in a managed worktree snapshot without .git metadata; skip git $1 and rely on direct file inspection."
    exit 0
    ;;
  *)
    exec "$real_git" "$@"
    ;;
esac
""";
    }
}
