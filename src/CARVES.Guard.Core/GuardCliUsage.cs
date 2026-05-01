namespace Carves.Guard.Core;

public static partial class GuardCliRunner
{
    private static int WriteGuardUsage(string commandName, int exitCode = 2)
    {
        var output = exitCode == 0 ? Console.Out : Console.Error;
        output.WriteLine($"Usage: {commandName} <init|check|run|audit|report|explain> ...");
        output.WriteLine($"       {commandName} init [--json] [--policy <path>] [--force]");
        output.WriteLine($"       {commandName} check [--json] [--policy <path>] [--base <ref>] [--head <ref>]");
        output.WriteLine(string.Equals(commandName, "carves-guard", StringComparison.Ordinal)
            ? $"       {commandName} run <task-id> [--json] [--policy <path>] [task-run flags...]  # requires CARVES Runtime host"
            : $"       {commandName} run <task-id> [--json] [--policy <path>] [task-run flags...]  # experimental");
        output.WriteLine($"       {commandName} audit [--json] [--limit <n>]");
        output.WriteLine($"       {commandName} report [--json] [--policy <path>] [--limit <n>]");
        output.WriteLine($"       {commandName} explain <run-id> [--json]");
        if (string.Equals(commandName, "carves-guard", StringComparison.Ordinal))
        {
            output.WriteLine("       Optional: --repo-root <path>");
        }

        return exitCode;
    }
}
