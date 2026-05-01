using Carves.Shield.Core;

namespace Carves.Shield.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        return ShieldCliRunner.Run(args);
    }
}
