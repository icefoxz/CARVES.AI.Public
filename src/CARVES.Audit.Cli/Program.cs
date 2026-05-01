using Carves.Audit.Core;

namespace Carves.Audit.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        return AuditCliRunner.Run(args);
    }
}
