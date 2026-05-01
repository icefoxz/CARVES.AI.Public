using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Host;

internal static class ReviewActorCommandArguments
{
    public static bool TryParse(
        IReadOnlyList<string> arguments,
        out IReadOnlyList<string> filteredArguments,
        out ActorSessionKind? actorKind,
        out string? actorIdentity,
        out string? error)
    {
        var filtered = new List<string>();
        actorKind = null;
        actorIdentity = null;
        error = null;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];
            if (string.Equals(argument, "--actor-kind", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count)
                {
                    filteredArguments = [];
                    error = "Usage: --actor-kind <operator|agent|planner|worker>";
                    return false;
                }

                if (!Enum.TryParse<ActorSessionKind>(arguments[++index], ignoreCase: true, out var parsedKind))
                {
                    filteredArguments = [];
                    error = $"Unknown actor kind '{arguments[index]}'.";
                    return false;
                }

                actorKind = parsedKind;
                continue;
            }

            if (string.Equals(argument, "--actor-identity", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= arguments.Count)
                {
                    filteredArguments = [];
                    error = "Usage: --actor-identity <identity>";
                    return false;
                }

                actorIdentity = arguments[++index];
                continue;
            }

            filtered.Add(argument);
        }

        if (actorKind is null && !string.IsNullOrWhiteSpace(actorIdentity))
        {
            filteredArguments = [];
            error = "--actor-identity requires --actor-kind.";
            return false;
        }

        if (actorKind is not null && string.IsNullOrWhiteSpace(actorIdentity))
        {
            actorIdentity = actorKind.Value.ToString().ToLowerInvariant();
        }

        filteredArguments = filtered;
        return true;
    }
}
