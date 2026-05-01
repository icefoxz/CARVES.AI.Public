using Carves.Handoff.Core;

namespace Carves.Handoff.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        return HandoffCliRunner.Run(args);
    }
}
