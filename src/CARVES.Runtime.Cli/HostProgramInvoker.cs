using System.Text;

namespace Carves.Runtime.Cli;

internal static class HostProgramInvoker
{
    public static HostInvocationResult Invoke(string repoRoot, params string[] args)
    {
        var invocation = new List<string> { "--repo-root", repoRoot };
        invocation.AddRange(args);

        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);

        try
        {
            var exitCode = Carves.Runtime.Host.Program.Main(invocation.ToArray());
            return new HostInvocationResult(exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    public sealed record HostInvocationResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);

        public IReadOnlyList<string> Lines =>
            CombinedOutput
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        public void WriteToConsole()
        {
            if (!string.IsNullOrWhiteSpace(StandardOutput))
            {
                Console.Out.Write(StandardOutput);
            }

            if (!string.IsNullOrWhiteSpace(StandardError))
            {
                Console.Error.Write(StandardError);
            }
        }
    }
}
