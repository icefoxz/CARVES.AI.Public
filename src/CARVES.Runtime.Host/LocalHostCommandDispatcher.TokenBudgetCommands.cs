using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static readonly string[] ContextOptionsWithValues = ["--model", "--max-context-tokens"];
    private static readonly string[] EvidenceSearchOptionsWithValues = ["--task-id", "--kind", "--budget", "--take"];
    private static readonly string[] MemorySearchOptionsWithValues = ["--category", "--scope", "--budget"];
    private static readonly string[] MemoryPromoteOptionsWithValues =
    [
        "--from-evidence",
        "--category",
        "--title",
        "--summary",
        "--statement",
        "--scope",
        "--task-scope",
        "--commit-scope",
        "--target-memory-path",
        "--confidence",
        "--actor",
        "--supersede",
    ];

    private static OperatorCommandResult RunContextCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: context <show|estimate> <task-id> [--model <model>] [--max-context-tokens <n>]");
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "show" when arguments.Count >= 2 => services.OperatorSurfaceService.ContextShow(arguments[1]),
            "estimate" when arguments.Count >= 2 => services.OperatorSurfaceService.ContextEstimate(
                arguments[1],
                ResolveOption(arguments, "--model"),
                ResolveOptionalInt(arguments, "--max-context-tokens")),
            _ => OperatorCommandResult.Failure("Usage: context <show|estimate> <task-id> [--model <model>] [--max-context-tokens <n>]"),
        };
    }

    private static OperatorCommandResult RunEvidenceCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0 || !string.Equals(arguments[0], "search", StringComparison.OrdinalIgnoreCase))
        {
            return OperatorCommandResult.Failure("Usage: evidence search [<query...>] [--task-id <task-id>] [--kind <context|execution-run|review|planning>] [--budget <tokens>] [--take <n>]");
        }

        var query = string.Join(
            ' ',
            ResolvePrimaryArguments(arguments.Skip(1).ToArray(), EvidenceSearchOptionsWithValues, Array.Empty<string>())).Trim();
        var taskId = ResolveOption(arguments, "--task-id");
        var kind = ResolveOption(arguments, "--kind");
        var budgetTokens = ResolveOptionalPositiveInt(arguments, "--budget", 450);
        var take = ResolveOptionalPositiveInt(arguments, "--take", 10);
        return services.OperatorSurfaceService.EvidenceSearch(
            string.IsNullOrWhiteSpace(query) ? null : query,
            taskId,
            kind,
            budgetTokens,
            take);
    }

    private static OperatorCommandResult RunMemoryCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return OperatorCommandResult.Failure("Usage: memory <search|promote|verify> [...]");
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "search" => RunMemorySearchCommand(services, arguments),
            "promote" => RunMemoryPromoteCommand(services, arguments),
            "verify" => RunMemoryVerifyCommand(services, arguments),
            _ => OperatorCommandResult.Failure("Usage: memory <search|promote|verify> [...]"),
        };
    }

    private static OperatorCommandResult RunMemorySearchCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var query = string.Join(
            ' ',
            ResolvePrimaryArguments(arguments.Skip(1).ToArray(), MemorySearchOptionsWithValues, ["--include-inactive-facts"])).Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return OperatorCommandResult.Failure("Usage: memory search <query...> [--category <architecture|project|modules|patterns>] [--scope <scope>] [--budget <tokens>] [--include-inactive-facts]");
        }

        return services.OperatorSurfaceService.MemorySearch(
            query,
            ResolveOption(arguments, "--category"),
            ResolveOption(arguments, "--scope"),
            ResolveOptionalPositiveInt(arguments, "--budget", 350),
            arguments.Any(argument => string.Equals(argument, "--include-inactive-facts", StringComparison.OrdinalIgnoreCase)));
    }

    private static OperatorCommandResult RunMemoryPromoteCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var evidenceId = ResolveOption(arguments, "--from-evidence");
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            return OperatorCommandResult.Failure("Usage: memory promote --from-evidence <evidence-id> [--category <category>] [--title <title>] [--summary <summary>] [--statement <statement>] [--scope <scope>] [--task-scope <task-id>] [--commit-scope <commit>] [--target-memory-path <path>] [--confidence <0.0-1.0>] [--actor <actor>] [--canonical] [--supersede <fact-id>]");
        }

        return services.OperatorSurfaceService.MemoryPromoteFromEvidence(
            evidenceId,
            ResolveOption(arguments, "--category"),
            ResolveOption(arguments, "--title"),
            ResolveOption(arguments, "--summary"),
            ResolveOption(arguments, "--statement"),
            ResolveOption(arguments, "--scope"),
            ResolveOption(arguments, "--target-memory-path"),
            ResolveOption(arguments, "--task-scope"),
            ResolveOption(arguments, "--commit-scope"),
            ResolveOptionalDouble(arguments, "--confidence", 0.8),
            arguments.Any(argument => string.Equals(argument, "--canonical", StringComparison.OrdinalIgnoreCase)),
            ResolveMultiOption(arguments, "--supersede"),
            ResolveOption(arguments, "--actor") ?? "operator");
    }

    private static OperatorCommandResult RunMemoryVerifyCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var strict = arguments.Any(argument => string.Equals(argument, "--strict", StringComparison.OrdinalIgnoreCase));
        var json = arguments.Any(argument => string.Equals(argument, "--json", StringComparison.OrdinalIgnoreCase));
        if (!strict)
        {
            return OperatorCommandResult.Failure("Usage: memory verify --strict [--json]");
        }

        return services.OperatorSurfaceService.MemoryVerify(strict, json);
    }
}
