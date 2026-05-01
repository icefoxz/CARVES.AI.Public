using Carves.Matrix.Core;
using System.Reflection;

namespace Carves.Matrix.Tests;

public sealed class MatrixNativeCliCaptureTests
{
    [Fact]
    public void NativeCliCapture_RestoresConsoleWritersAfterCapturedFailure()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        var capture = InvokeNativeCliStep(
            "failure_probe",
            "failure probe",
            () =>
            {
                Console.WriteLine("stdout-before-failure");
                Console.Error.WriteLine("stderr-before-failure");
                throw new InvalidOperationException("captured failure");
            });

        Assert.Same(originalOut, Console.Out);
        Assert.Same(originalError, Console.Error);
        Assert.False(ReadBool(ReadProperty(capture, "Step"), "Passed"));
        Assert.Equal(1, ReadInt(ReadProperty(capture, "Step"), "ExitCode"));
        Assert.Contains("stdout-before-failure", ReadString(capture, "Stdout"), StringComparison.Ordinal);
        Assert.Equal("captured failure", ReadString(capture, "Stderr"));
    }

    [Fact]
    public async Task NativeCliCapture_SerializesConcurrentConsoleRedirection()
    {
        using var startGate = new ManualResetEventSlim(false);
        var state = new CaptureConcurrencyState();

        var first = Task.Run(() => InvokeConcurrentCapture("first", startGate, state));
        var second = Task.Run(() => InvokeConcurrentCapture("second", startGate, state));

        startGate.Set();
        var captures = await Task.WhenAll(first, second);

        Assert.Equal(0, state.OverlapObserved);
        AssertCaptureContainsOnly(captures[0], "first", "second");
        AssertCaptureContainsOnly(captures[1], "second", "first");
    }

    private static object InvokeConcurrentCapture(
        string label,
        ManualResetEventSlim startGate,
        CaptureConcurrencyState state)
    {
        return InvokeNativeCliStep(
            label,
            $"{label} command",
            () =>
            {
                startGate.Wait();
                if (Interlocked.Increment(ref state.ActiveCaptures) > 1)
                {
                    Interlocked.Exchange(ref state.OverlapObserved, 1);
                }

                try
                {
                    for (var index = 0; index < 50; index++)
                    {
                        Console.WriteLine($"{label}-stdout-{index}");
                        Console.Error.WriteLine($"{label}-stderr-{index}");
                        Thread.Sleep(1);
                    }

                    return 0;
                }
                finally
                {
                    Interlocked.Decrement(ref state.ActiveCaptures);
                }
            });
    }

    private static void AssertCaptureContainsOnly(object capture, string expectedLabel, string unexpectedLabel)
    {
        var step = ReadProperty(capture, "Step");
        Assert.True(ReadBool(step, "Passed"));
        Assert.Equal(0, ReadInt(step, "ExitCode"));

        var stdout = ReadString(capture, "Stdout");
        var stderr = ReadString(capture, "Stderr");
        Assert.Contains($"{expectedLabel}-stdout-0", stdout, StringComparison.Ordinal);
        Assert.Contains($"{expectedLabel}-stdout-49", stdout, StringComparison.Ordinal);
        Assert.Contains($"{expectedLabel}-stderr-0", stderr, StringComparison.Ordinal);
        Assert.Contains($"{expectedLabel}-stderr-49", stderr, StringComparison.Ordinal);
        Assert.DoesNotContain(unexpectedLabel, stdout, StringComparison.Ordinal);
        Assert.DoesNotContain(unexpectedLabel, stderr, StringComparison.Ordinal);
    }

    private static object InvokeNativeCliStep(string stepId, string command, Func<int> action)
    {
        var method = typeof(MatrixCliRunner).GetMethod(
            "RunNativeCliStep",
            BindingFlags.NonPublic | BindingFlags.Static,
            binder: null,
            [typeof(string), typeof(string), typeof(Func<int>), typeof(IReadOnlyCollection<int>)],
            modifiers: null)
            ?? throw new MissingMethodException(nameof(MatrixCliRunner), "RunNativeCliStep");

        return method.Invoke(null, [stepId, command, action, null])
               ?? throw new InvalidOperationException("RunNativeCliStep returned null.");
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

    private sealed class CaptureConcurrencyState
    {
        public int ActiveCaptures;
        public int OverlapObserved;
    }
}
