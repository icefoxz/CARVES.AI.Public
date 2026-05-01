using Carves.Matrix.Core;
using System.Diagnostics;
using System.Reflection;

namespace Carves.Matrix.Tests;

public sealed class MatrixProcessInvokerTests
{
    [Fact]
    public void ProcessInvoker_DrainsStdoutAndStderrWithoutDeadlock()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = InvokeProcess(
            "bash",
            ["-lc", "for i in $(seq 1 2000); do printf 'out-%s\\n' \"$i\"; printf 'err-%s\\n' \"$i\" >&2; done"],
            TimeSpan.FromSeconds(10),
            maxCapturedOutputChars: 1_000_000);

        Assert.Equal(0, ReadInt(result, "ExitCode"));
        Assert.False(ReadBool(result, "TimedOut"));
        Assert.Contains("out-2000", ReadString(result, "Stdout"), StringComparison.Ordinal);
        Assert.Contains("err-2000", ReadString(result, "Stderr"), StringComparison.Ordinal);
    }

    [Fact]
    public void ProcessInvoker_TimesOutAndKillsLongRunningProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = InvokeProcess(
            "bash",
            ["-lc", "trap '' TERM; sleep 10"],
            TimeSpan.FromMilliseconds(200),
            maxCapturedOutputChars: 1000);

        Assert.Equal(124, ReadInt(result, "ExitCode"));
        Assert.True(ReadBool(result, "TimedOut"));
    }

    [Fact]
    public void ProcessInvoker_ReturnsTimeoutWhenKillDoesNotExitProcess()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var elapsed = Stopwatch.StartNew();
        var result = InvokeProcess(
            "bash",
            ["-lc", "sleep 2"],
            TimeSpan.FromMilliseconds(100),
            maxCapturedOutputChars: 1000,
            killGraceTimeout: TimeSpan.FromMilliseconds(100),
            killProcessTree: _ => { });
        elapsed.Stop();

        Assert.Equal(124, ReadInt(result, "ExitCode"));
        Assert.True(ReadBool(result, "TimedOut"));
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(1), $"Process timeout path took too long: {elapsed.Elapsed}.");
    }

    [Fact]
    public void ProcessInvoker_TruncatesLargeCapturedOutput()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var result = InvokeProcess(
            "bash",
            ["-lc", "printf '%*s' 2000 '' | tr ' ' 'x'"],
            TimeSpan.FromSeconds(10),
            maxCapturedOutputChars: 100);

        Assert.Equal(0, ReadInt(result, "ExitCode"));
        Assert.True(ReadBool(result, "StdoutTruncated"));
        Assert.Equal(100, ReadString(result, "Stdout").Length);
    }

    [Fact]
    public void CommandResolver_WindowsFallsBackToExecutableExtensionsWhenPathExtIsIncomplete()
    {
        var gitDirectory = @"C:\Program Files\Git\cmd";
        var nodeDirectory = @"C:\Program Files\nodejs";
        var path = string.Join(';', gitDirectory, nodeDirectory);

        var resolved = MatrixProcessCommandResolver.ResolveForProcessStart(
            "git",
            isWindows: true,
            pathValue: path,
            pathExtValue: ".CPL",
            fileExists: candidate => candidate.Contains(@"Git\cmd", StringComparison.OrdinalIgnoreCase)
                && candidate.EndsWith("git.exe", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(@"Git\cmd", resolved, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("git.exe", resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CommandResolver_NonWindowsKeepsOriginalCommand()
    {
        var resolved = MatrixProcessCommandResolver.ResolveForProcessStart(
            "git",
            isWindows: false,
            pathValue: "/usr/bin",
            pathExtValue: ".CPL",
            fileExists: _ => true);

        Assert.Equal("git", resolved);
    }

    [Theory]
    [InlineData(@"tools\git")]
    [InlineData("tools/git")]
    [InlineData(@"C:\Program Files\Git\cmd\git.exe")]
    public void CommandResolver_WindowsKeepsExplicitPaths(string command)
    {
        var resolved = MatrixProcessCommandResolver.ResolveForProcessStart(
            command,
            isWindows: true,
            pathValue: @"C:\Program Files\Git\cmd",
            pathExtValue: ".EXE",
            fileExists: _ => true);

        Assert.Equal(command, resolved);
    }

    private static object InvokeProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        int maxCapturedOutputChars)
    {
        var method = typeof(MatrixCliRunner).GetMethod(
            "InvokeProcess",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(string), typeof(IReadOnlyList<string>), typeof(string), typeof(TimeSpan), typeof(int)],
            modifiers: null)
            ?? throw new MissingMethodException(nameof(MatrixCliRunner), "InvokeProcess");

        return method.Invoke(null, [fileName, arguments, Directory.GetCurrentDirectory(), timeout, maxCapturedOutputChars])
               ?? throw new InvalidOperationException("InvokeProcess returned null.");
    }

    private static object InvokeProcess(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        int maxCapturedOutputChars,
        TimeSpan killGraceTimeout,
        Action<Process> killProcessTree)
    {
        var method = typeof(MatrixCliRunner).GetMethod(
            "InvokeProcess",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [
                typeof(string),
                typeof(IReadOnlyList<string>),
                typeof(string),
                typeof(TimeSpan),
                typeof(int),
                typeof(TimeSpan),
                typeof(Action<Process>),
            ],
            modifiers: null)
            ?? throw new MissingMethodException(nameof(MatrixCliRunner), "InvokeProcess");

        return method.Invoke(null, [fileName, arguments, Directory.GetCurrentDirectory(), timeout, maxCapturedOutputChars, killGraceTimeout, killProcessTree])
               ?? throw new InvalidOperationException("InvokeProcess returned null.");
    }

    private static object ReadProperty(object target, string propertyName)
    {
        return target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target)
               ?? throw new MissingMemberException(target.GetType().FullName, propertyName);
    }

    private static string ReadString(object target, string propertyName)
    {
        return (string)ReadProperty(target, propertyName);
    }

    private static bool ReadBool(object target, string propertyName)
    {
        return (bool)ReadProperty(target, propertyName);
    }

    private static int ReadInt(object target, string propertyName)
    {
        return (int)ReadProperty(target, propertyName);
    }
}
