using Carves.Matrix.Core;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunMatrix(IReadOnlyList<string> arguments)
    {
        return MatrixCliRunner.Run(arguments, commandName: "carves matrix");
    }
}
