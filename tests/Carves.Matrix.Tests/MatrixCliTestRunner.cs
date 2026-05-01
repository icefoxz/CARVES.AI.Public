using Carves.Matrix.Core;

namespace Carves.Matrix.Tests;

internal static class MatrixCliTestRunner
{
    public static MatrixCliRunResult RunMatrixCli(params string[] arguments)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOutput = new StringWriter();
        using var standardError = new StringWriter();
        Console.SetOut(standardOutput);
        Console.SetError(standardError);

        try
        {
            var exitCode = MatrixCliRunner.Run(arguments);
            return new MatrixCliRunResult(exitCode, standardOutput.ToString(), standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}

internal sealed record MatrixCliRunResult(int ExitCode, string StandardOutput, string StandardError);
