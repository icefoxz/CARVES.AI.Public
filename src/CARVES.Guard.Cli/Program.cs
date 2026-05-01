using Carves.Guard.Core;

namespace Carves.Guard.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        return GuardCliRunner.Run(args);
    }
}
