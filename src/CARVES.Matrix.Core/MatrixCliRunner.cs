using System.Text.Json;

namespace Carves.Matrix.Core;

public static partial class MatrixCliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public static int Run(IReadOnlyList<string> arguments)
    {
        return Run(arguments, commandName: "carves-matrix");
    }

    public static int Run(IReadOnlyList<string> arguments, string commandName)
    {
        if (arguments.Count == 0)
        {
            return WriteUsage(commandName);
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "proof" => RunProof(arguments.Skip(1).ToArray()),
            "verify" => RunVerify(arguments.Skip(1).ToArray()),
            "trial" => RunTrial(arguments.Skip(1).ToArray(), commandName),
            "e2e" => RunScriptCommand("matrix-e2e-smoke.ps1", arguments.Skip(1).ToArray()),
            "packaged" => RunScriptCommand("matrix-packaged-install-smoke.ps1", arguments.Skip(1).ToArray()),
            "help" or "--help" or "-h" => WriteUsage(commandName, exitCode: 0),
            _ => WriteUsage(commandName),
        };
    }
}
