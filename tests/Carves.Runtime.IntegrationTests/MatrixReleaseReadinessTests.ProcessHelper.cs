using System.Diagnostics;
using System.Text.Json;

namespace Carves.Runtime.IntegrationTests;

public sealed partial class MatrixReleaseReadinessTests
{
    [Fact]
    public void MatrixPowerShellScripts_UseSharedProcessHelper()
    {
        var repoRoot = LocateSourceRepoRoot();
        var helper = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", "matrix-checked-process.ps1"));
        var targetScripts = new[]
        {
            "matrix-e2e-smoke.ps1",
            "matrix-packaged-install-smoke.ps1",
            "matrix-cross-platform-verify-pilot.ps1",
        };

        Assert.Contains("function Invoke-MatrixCheckedProcess", helper, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => Pump(process.StandardOutput", helper, StringComparison.Ordinal);
        Assert.Contains("Task.Run(() => Pump(process.StandardError", helper, StringComparison.Ordinal);
        Assert.Contains("WaitForExit(timeoutMilliseconds)", helper, StringComparison.Ordinal);
        Assert.Contains("Kill(entireProcessTree: true)", helper, StringComparison.Ordinal);
        Assert.Contains("stdout_truncated", helper, StringComparison.Ordinal);
        Assert.Contains("stderr_truncated", helper, StringComparison.Ordinal);

        foreach (var scriptName in targetScripts)
        {
            var script = File.ReadAllText(Path.Combine(repoRoot, "scripts", "matrix", scriptName));
            Assert.Contains(". (Join-Path $PSScriptRoot \"matrix-checked-process.ps1\")", script, StringComparison.Ordinal);
            Assert.Contains("Invoke-MatrixCheckedProcess", script, StringComparison.Ordinal);
            Assert.DoesNotContain("StandardOutput.ReadToEnd", script, StringComparison.Ordinal);
            Assert.DoesNotContain("StandardError.ReadToEnd", script, StringComparison.Ordinal);
            Assert.DoesNotContain("WaitForExit()", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MatrixCheckedProcessHelper_DrainsStdoutAndStderrFlood()
    {
        var repoRoot = LocateSourceRepoRoot();
        var result = RunPowerShellHelperScenario(
            repoRoot,
            """
            $child = 'for ($i = 0; $i -lt 1200; $i++) { [Console]::Out.WriteLine("stdout-flood-" + $i); [Console]::Error.WriteLine("stderr-flood-" + $i) }'
            $result = Invoke-MatrixCheckedProcess -FileName "pwsh" -Arguments @("-NoProfile", "-Command", $child) -WorkingDirectory $RepoRoot -TimeoutSeconds 10 -MaxOutputChars 200000
            $result | ConvertTo-Json -Depth 10
            """);

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.Equal(0, root.GetProperty("exit_code").GetInt32());
        Assert.True(root.GetProperty("passed").GetBoolean());
        Assert.False(root.GetProperty("timed_out").GetBoolean());
        Assert.False(root.GetProperty("stdout_truncated").GetBoolean());
        Assert.False(root.GetProperty("stderr_truncated").GetBoolean());
        Assert.Contains("stdout-flood-1199", root.GetProperty("stdout").GetString(), StringComparison.Ordinal);
        Assert.Contains("stderr-flood-1199", root.GetProperty("stderr").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void MatrixCheckedProcessHelper_TimeoutReturnsQuickly()
    {
        var repoRoot = LocateSourceRepoRoot();
        var stopwatch = Stopwatch.StartNew();
        var result = RunPowerShellHelperScenario(
            repoRoot,
            """
            $result = Invoke-MatrixCheckedProcess -FileName "pwsh" -Arguments @("-NoProfile", "-Command", "Start-Sleep -Seconds 10") -WorkingDirectory $RepoRoot -TimeoutSeconds 1
            $result | ConvertTo-Json -Depth 10
            """);
        stopwatch.Stop();

        Assert.Equal(0, result.ExitCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(6), $"Timeout helper took too long: {stopwatch.Elapsed}");
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.True(root.GetProperty("timed_out").GetBoolean());
        Assert.False(root.GetProperty("passed").GetBoolean());
        Assert.True(root.GetProperty("process_tree_kill_attempted").GetBoolean());
    }

    [Fact]
    public void MatrixCheckedProcessHelper_CapsOutputAndReportsTruncation()
    {
        var repoRoot = LocateSourceRepoRoot();
        var result = RunPowerShellHelperScenario(
            repoRoot,
            """
            $child = 'for ($i = 0; $i -lt 100; $i++) { [Console]::Out.WriteLine("0123456789abcdefghij"); [Console]::Error.WriteLine("abcdefghij0123456789") }'
            $result = Invoke-MatrixCheckedProcess -FileName "pwsh" -Arguments @("-NoProfile", "-Command", $child) -WorkingDirectory $RepoRoot -TimeoutSeconds 10 -MaxOutputChars 128
            $result | ConvertTo-Json -Depth 10
            """);

        Assert.Equal(0, result.ExitCode);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var root = document.RootElement;
        Assert.True(root.GetProperty("stdout_truncated").GetBoolean());
        Assert.True(root.GetProperty("stderr_truncated").GetBoolean());
        Assert.True(root.GetProperty("stdout").GetString()!.Length <= 128);
        Assert.True(root.GetProperty("stderr").GetString()!.Length <= 128);
    }

    private static CliProcessResult RunPowerShellHelperScenario(string repoRoot, string scenarioBody)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "carves-matrix-helper-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var scriptPath = Path.Combine(tempRoot, "scenario.ps1");
        try
        {
            var helperPath = Path.Combine(repoRoot, "scripts", "matrix", "matrix-checked-process.ps1");
            File.WriteAllText(
                scriptPath,
                $$"""
                Set-StrictMode -Version Latest
                $ErrorActionPreference = "Stop"
                $RepoRoot = "{{EscapePowerShellSingleQuotedString(repoRoot)}}"
                . '{{EscapePowerShellSingleQuotedString(helperPath)}}'
                {{scenarioBody}}
                """);

            return RunPowerShellScript(repoRoot, scriptPath);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static CliProcessResult RunPowerShellScript(string workingDirectory, string scriptPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start pwsh.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(30)))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"PowerShell scenario timed out. STDERR: {stderrTask.Result}");
        }

        return new CliProcessResult(process.ExitCode, stdoutTask.Result, stderrTask.Result);
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
