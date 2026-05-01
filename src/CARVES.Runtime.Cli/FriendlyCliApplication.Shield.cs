using Carves.Shield.Core;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunShield(string repoRoot, IReadOnlyList<string> arguments)
    {
        return ShieldCliRunner.Run(repoRoot, arguments, commandName: "carves shield");
    }
}
