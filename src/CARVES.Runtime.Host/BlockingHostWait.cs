using System.Diagnostics;

namespace Carves.Runtime.Host;

internal static class BlockingHostWait
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

    public static T Poll<T>(TimeSpan timeout, TimeSpan interval, Func<T> probe, Func<T, bool> completed)
    {
        var stopwatch = Stopwatch.StartNew();
        var current = probe();
        while (!completed(current) && stopwatch.Elapsed < timeout)
        {
            Delay(interval);
            current = probe();
        }

        return current;
    }

    public static bool WaitUntil(TimeSpan timeout, TimeSpan interval, Func<bool> condition)
    {
        return Poll(timeout, interval, condition, static value => value);
    }
}
