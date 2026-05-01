using System.Diagnostics;
using System.Text;

namespace Carves.Runtime.Host;

internal sealed class LocalHostProcessLauncher
{
    private const string LogsDirectoryName = "logs";
    private const string StandardOutputLogFileName = "host.stdout.log";
    private const string StandardErrorLogFileName = "host.stderr.log";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    public Process Start(string repoRoot, int port, int intervalMilliseconds)
    {
        var assemblyPath = typeof(Program).Assembly.Location;
        var sourceDirectory = Path.GetDirectoryName(assemblyPath)
            ?? throw new InvalidOperationException("Host assembly directory is unavailable.");
        var deploymentDirectory = PrepareDeployment(repoRoot, sourceDirectory);
        var deployedAssemblyPath = Path.Combine(deploymentDirectory, Path.GetFileName(assemblyPath));
        var standardOutputLogPath = GetStandardOutputLogPath(deploymentDirectory);
        var standardErrorLogPath = GetStandardErrorLogPath(deploymentDirectory);
        PrepareLogFile(standardOutputLogPath);
        PrepareLogFile(standardErrorLogPath);

        var startInfo = OperatingSystem.IsWindows()
            ? CreateWindowsStartInfo(deploymentDirectory, deployedAssemblyPath, repoRoot, port, intervalMilliseconds, standardOutputLogPath, standardErrorLogPath)
            : CreateUnixStartInfo(deploymentDirectory, deployedAssemblyPath, repoRoot, port, intervalMilliseconds, standardOutputLogPath, standardErrorLogPath);
        startInfo.Environment["CARVES_REPO_ROOT"] = repoRoot;
        startInfo.Environment["CARVES_HOST_SERVE_SUPPRESS_EXIT_OUTPUT"] = "1";

        var process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException("Failed to launch resident CARVES host process.");
    }

    public static string GetStandardOutputLogPath(string deploymentDirectory)
    {
        return Path.Combine(deploymentDirectory, LogsDirectoryName, StandardOutputLogFileName);
    }

    public static string GetStandardErrorLogPath(string deploymentDirectory)
    {
        return Path.Combine(deploymentDirectory, LogsDirectoryName, StandardErrorLogFileName);
    }

    private static ProcessStartInfo CreateUnixStartInfo(
        string deploymentDirectory,
        string deployedAssemblyPath,
        string repoRoot,
        int port,
        int intervalMilliseconds,
        string standardOutputLogPath,
        string standardErrorLogPath)
    {
        var launchScriptPath = Path.Combine(deploymentDirectory, "carves-host-launch.sh");
        var command = BuildUnixHostCommand(deployedAssemblyPath, repoRoot, port, intervalMilliseconds, standardOutputLogPath, standardErrorLogPath);
        var script = new StringBuilder();
        script.AppendLine("#!/usr/bin/env sh");
        script.Append("export CARVES_REPO_ROOT=").AppendLine(ShellQuote(repoRoot));
        script.AppendLine("export CARVES_HOST_SERVE_SUPPRESS_EXIT_OUTPUT=1");
        script.AppendLine("if command -v setsid >/dev/null 2>&1; then");
        script.Append("  exec setsid ").AppendLine(command);
        script.AppendLine("fi");
        script.Append("exec ").AppendLine(command);
        File.WriteAllText(launchScriptPath, script.ToString(), Utf8WithoutBom);

        var startInfo = CreateBaseStartInfo(deploymentDirectory);
        startInfo.FileName = "/bin/sh";
        startInfo.ArgumentList.Add(launchScriptPath);
        return startInfo;
    }

    private static string BuildUnixHostCommand(
        string deployedAssemblyPath,
        string repoRoot,
        int port,
        int intervalMilliseconds,
        string standardOutputLogPath,
        string standardErrorLogPath)
    {
        return new StringBuilder()
            .Append("dotnet ")
            .Append(ShellQuote(deployedAssemblyPath))
            .Append(" --repo-root ")
            .Append(ShellQuote(repoRoot))
            .Append(" host serve --port ")
            .Append(port.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" --interval-ms ")
            .Append(intervalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" >> ")
            .Append(ShellQuote(standardOutputLogPath))
            .Append(" 2>> ")
            .Append(ShellQuote(standardErrorLogPath))
            .ToString();
    }

    private static ProcessStartInfo CreateWindowsStartInfo(
        string deploymentDirectory,
        string deployedAssemblyPath,
        string repoRoot,
        int port,
        int intervalMilliseconds,
        string standardOutputLogPath,
        string standardErrorLogPath)
    {
        var launchScriptPath = Path.Combine(deploymentDirectory, "carves-host-launch.cmd");
        var script = new StringBuilder();
        script.AppendLine("@echo off");
        script.Append("set \"CARVES_REPO_ROOT=").Append(repoRoot).AppendLine("\"");
        script.AppendLine("set \"CARVES_HOST_SERVE_SUPPRESS_EXIT_OUTPUT=1\"");
        script.Append("dotnet ")
            .Append(CmdQuote(deployedAssemblyPath))
            .Append(" --repo-root ")
            .Append(CmdQuote(repoRoot))
            .Append(" host serve --port ")
            .Append(port.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" --interval-ms ")
            .Append(intervalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Append(" >> ")
            .Append(CmdQuote(standardOutputLogPath))
            .Append(" 2>> ")
            .AppendLine(CmdQuote(standardErrorLogPath));
        script.AppendLine("exit /b %ERRORLEVEL%");
        File.WriteAllText(launchScriptPath, script.ToString(), Utf8WithoutBom);

        var startInfo = CreateBaseStartInfo(deploymentDirectory);
        startInfo.FileName = "cmd.exe";
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(launchScriptPath);
        return startInfo;
    }

    private static ProcessStartInfo CreateBaseStartInfo(string deploymentDirectory)
    {
        return new ProcessStartInfo
        {
            WorkingDirectory = deploymentDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
        };
    }

    private static string PrepareDeployment(string repoRoot, string sourceDirectory)
    {
        var deploymentGenerationId = LocalHostPaths.CreateDeploymentGenerationId();
        var deploymentsDirectory = LocalHostPaths.GetDeploymentsDirectory(repoRoot);
        var deploymentDirectory = LocalHostPaths.GetDeploymentDirectory(repoRoot, deploymentGenerationId);
        var stagingDirectory = $"{deploymentDirectory}.staging";

        Directory.CreateDirectory(deploymentsDirectory);

        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, true);
        }

        CopyDirectory(sourceDirectory, stagingDirectory);

        Directory.Move(stagingDirectory, deploymentDirectory);
        return deploymentDirectory;
    }

    private static void PrepareLogFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty, Utf8WithoutBom);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var childDestination = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, childDestination);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(file));
            File.Copy(file, destinationPath, overwrite: true);
        }
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    private static string CmdQuote(string value)
    {
        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
