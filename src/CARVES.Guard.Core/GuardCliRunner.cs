using System.Text.Json;
using Carves.Runtime.Application.Guard;
using Carves.Runtime.Infrastructure.Processes;

namespace Carves.Guard.Core;

public enum GuardRuntimeTransportPreference
{
    Auto,
    Cold,
    Host,
}

public sealed record GuardRuntimeTaskInvocation(
    string RepoRoot,
    string TaskId,
    IReadOnlyList<string> Arguments,
    GuardRuntimeTransportPreference Transport);

public interface IGuardRuntimeTaskRunner
{
    GuardRuntimeExecutionResult Execute(GuardRuntimeTaskInvocation invocation);
}

public static partial class GuardCliRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private const string GuardRunModeStability = "experimental";

    public static int Run(IReadOnlyList<string> arguments)
    {
        var parsed = ParseStandaloneArguments(arguments);
        if (!string.IsNullOrWhiteSpace(parsed.Error))
        {
            Console.Error.WriteLine(parsed.Error);
            return 2;
        }

        var repoRoot = ResolveRepoRoot(parsed.RepoRootOverride);
        if (repoRoot is null)
        {
            Console.Error.WriteLine("Not a git repository.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Next:");
            Console.Error.WriteLine("  run git init or switch to a project folder");
            return 1;
        }

        return Run(repoRoot, parsed.CommandArguments, new UnavailableGuardRuntimeTaskRunner(), "carves-guard", parsed.Transport);
    }

    public static int Run(
        string repoRoot,
        IReadOnlyList<string> arguments,
        IGuardRuntimeTaskRunner? runtimeTaskRunner = null,
        string commandName = "carves guard",
        GuardRuntimeTransportPreference transport = GuardRuntimeTransportPreference.Auto)
    {
        if (arguments.Count == 0)
        {
            return WriteGuardUsage(commandName);
        }

        var taskRunner = runtimeTaskRunner ?? new UnavailableGuardRuntimeTaskRunner();
        return arguments[0].ToLowerInvariant() switch
        {
            "init" => RunGuardInit(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "check" => RunGuardCheck(repoRoot, arguments.Skip(1).ToArray()),
            "run" => RunGuardRun(repoRoot, arguments.Skip(1).ToArray(), commandName, taskRunner, transport),
            "audit" => RunGuardAudit(repoRoot, arguments.Skip(1).ToArray()),
            "report" => RunGuardReport(repoRoot, arguments.Skip(1).ToArray()),
            "explain" => RunGuardExplain(repoRoot, arguments.Skip(1).ToArray(), commandName),
            "help" or "--help" or "-h" => WriteGuardUsage(commandName, exitCode: 0),
            _ => WriteGuardUsage(commandName),
        };
    }
}
