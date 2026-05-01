using CliProgram = Carves.Runtime.Cli.Program;

namespace Carves.Runtime.IntegrationTests;

internal static class CliProgramHarness
{
    public static CliRunResult RunInDirectory(string workingDirectory, params string[] args)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Directory.SetCurrentDirectory(workingDirectory);
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = CliProgram.Main(args);
            return new CliRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    internal sealed record CliRunResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string CombinedOutput => string.Concat(StandardOutput, StandardError);
    }
}
