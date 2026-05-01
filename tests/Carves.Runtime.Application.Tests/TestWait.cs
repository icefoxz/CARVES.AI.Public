using System.Diagnostics;

namespace Carves.Runtime.Application.Tests;

internal static class TestWait
{
    private static readonly ManualResetEventSlim DelayGate = new(initialState: false);

    public static void Delay(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        DelayGate.Wait(delay);
    }

    public static bool WaitUntil(TimeSpan timeout, TimeSpan interval, Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return true;
            }

            Delay(interval);
        }

        return condition();
    }
}
