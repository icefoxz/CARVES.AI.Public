using System.Diagnostics;

namespace Carves.Runtime.IntegrationTests;

internal static class IntegrationTestWait
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

    public static void WaitForUtcAdvance(DateTimeOffset baseline, TimeSpan timeout)
    {
        WaitUntil(timeout, TimeSpan.FromMilliseconds(1), () => DateTimeOffset.UtcNow > baseline);
    }
}
