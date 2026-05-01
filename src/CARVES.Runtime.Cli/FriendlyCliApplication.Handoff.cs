using Carves.Handoff.Core;

namespace Carves.Runtime.Cli;

internal static partial class FriendlyCliApplication
{
    private static int RunHandoff(string repoRoot, IReadOnlyList<string> arguments)
    {
        return HandoffCliRunner.Run(repoRoot, arguments);
    }
}
