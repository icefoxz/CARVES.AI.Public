namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static int RunScriptCommand(string scriptName, IReadOnlyList<string> arguments)
    {
        var options = MatrixOptions.Parse(arguments);
        if (!string.IsNullOrWhiteSpace(options.Error))
        {
            Console.Error.WriteLine(options.Error);
            return 2;
        }

        if (options.Json)
        {
            Console.Error.WriteLine("--json is only supported by proof and verify.");
            return 2;
        }

        var runtimeRoot = ResolveRuntimeRoot(options.RuntimeRoot);
        if (runtimeRoot is null)
        {
            Console.Error.WriteLine("Unable to locate CARVES.Runtime root. Run from the source repository or pass --runtime-root <path>.");
            return 2;
        }

        var scriptPath = ResolveMatrixScript(runtimeRoot, scriptName);
        var result = InvokeScript(scriptPath, BuildPassthroughScriptArguments(options), runtimeRoot, null);
        if (result.ExitCode != 0)
        {
            WriteFailedCommand(result);
            return result.ExitCode;
        }

        Console.Write(result.Stdout);
        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            Console.Error.Write(result.Stderr);
        }

        return 0;
    }

    private static IReadOnlyList<string> BuildPassthroughScriptArguments(MatrixOptions options)
    {
        var values = new List<string>();
        AddOption(values, "-RuntimeRoot", options.RuntimeRoot);
        AddOption(values, "-WorkRoot", options.WorkRoot);
        AddOption(values, "-ArtifactRoot", options.ArtifactRoot);
        AddOption(values, "-Configuration", options.Configuration);
        AddOption(values, "-ToolMode", options.ToolMode);
        AddOption(values, "-GuardCommand", options.GuardCommand);
        AddOption(values, "-HandoffCommand", options.HandoffCommand);
        AddOption(values, "-AuditCommand", options.AuditCommand);
        AddOption(values, "-ShieldCommand", options.ShieldCommand);
        AddOption(values, "-MatrixCommand", options.MatrixCommand);
        AddOption(values, "-GuardVersion", options.GuardVersion);
        AddOption(values, "-HandoffVersion", options.HandoffVersion);
        AddOption(values, "-AuditVersion", options.AuditVersion);
        AddOption(values, "-ShieldVersion", options.ShieldVersion);
        AddOption(values, "-MatrixVersion", options.MatrixVersion);
        if (options.Keep)
        {
            values.Add("-Keep");
        }

        return values;
    }

    private static void AddOption(List<string> values, string option, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        values.Add(option);
        values.Add(value);
    }

    private static ScriptResult InvokeScript(string scriptPath, IReadOnlyList<string> scriptArguments, string workingDirectory, string? outputPath)
    {
        var allArguments = new List<string>
        {
            "-NoProfile",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            scriptPath,
        };
        allArguments.AddRange(scriptArguments);
        var result = InvokeProcess("pwsh", allArguments, workingDirectory);
        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            File.WriteAllText(outputPath, result.Stdout);
            if (!string.IsNullOrWhiteSpace(result.Stderr))
            {
                File.WriteAllText(outputPath + ".stderr.txt", result.Stderr);
            }
        }

        return result;
    }

}
