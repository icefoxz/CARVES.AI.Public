using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    // Peer CLI runners write to global Console; serialize redirection to keep captures isolated.
    private static readonly object NativeCliCaptureLock = new();

    private static MatrixNativeProofStepCapture RunNativeProcessStep(
        string stepId,
        string command,
        string workingDirectory,
        string fileName,
        IReadOnlyList<string> arguments)
    {
        try
        {
            var result = InvokeProcess(fileName, arguments, workingDirectory);
            return new MatrixNativeProofStepCapture(
                new MatrixNativeProofStep(
                    stepId,
                    command,
                    result.ExitCode,
                    result.ExitCode == 0,
                    StdoutPath: null,
                    result.ExitCode == 0 ? null : TruncateForJson(result.Stdout),
                    result.ExitCode == 0 || string.IsNullOrWhiteSpace(result.Stderr) ? null : TruncateForJson(result.Stderr)),
                result.Stdout,
                result.Stderr);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or InvalidOperationException
                                   or System.ComponentModel.Win32Exception)
        {
            return new MatrixNativeProofStepCapture(
                new MatrixNativeProofStep(
                    stepId,
                    command,
                    ExitCode: 1,
                    Passed: false,
                    StdoutPath: null,
                    StdoutPreview: null,
                    StderrPreview: $"{ex.GetType().Name}: {ex.Message}"),
                string.Empty,
                ex.Message);
        }
    }

    private static MatrixNativeProofStepCapture RunNativeCliStep(
        string stepId,
        string command,
        Func<int> action,
        IReadOnlyCollection<int>? acceptedExitCodes = null)
    {
        lock (NativeCliCaptureLock)
        {
            var originalOut = Console.Out;
            var originalError = Console.Error;
            using var standardOutput = new StringWriter();
            using var standardError = new StringWriter();
            Console.SetOut(standardOutput);
            Console.SetError(standardError);

            try
            {
                var exitCode = action();
                var stdout = standardOutput.ToString();
                var stderr = standardError.ToString();
                var passed = acceptedExitCodes?.Contains(exitCode) ?? exitCode == 0;
                return new MatrixNativeProofStepCapture(
                    new MatrixNativeProofStep(
                        stepId,
                        command,
                        exitCode,
                        passed,
                        StdoutPath: null,
                        passed ? null : TruncateForJson(stdout),
                        passed || string.IsNullOrWhiteSpace(stderr) ? null : TruncateForJson(stderr)),
                    stdout,
                    stderr);
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or ArgumentException
                                       or NotSupportedException
                                       or InvalidOperationException
                                       or JsonException)
            {
                return new MatrixNativeProofStepCapture(
                    new MatrixNativeProofStep(
                        stepId,
                        command,
                        ExitCode: 1,
                        Passed: false,
                        StdoutPath: null,
                        StdoutPreview: TruncateForJson(standardOutput.ToString()),
                        StderrPreview: $"{ex.GetType().Name}: {ex.Message}"),
                    standardOutput.ToString(),
                    ex.Message);
            }
            finally
            {
                Console.SetOut(originalOut);
                Console.SetError(originalError);
            }
        }
    }

    private static bool AppendNativeStep(
        List<MatrixNativeProofStep> steps,
        MatrixNativeProofStepCapture capture,
        out MatrixNativeProofStep step)
    {
        step = capture.Step;
        steps.Add(step);
        return step.Passed;
    }
}
