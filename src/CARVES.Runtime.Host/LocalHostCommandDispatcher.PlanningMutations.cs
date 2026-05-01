using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static OperatorCommandResult RunCreateCardDraftCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: create-card-draft <json-path> [--repo-id <id>]")
            : scoped.OperatorSurfaceService.CreateCardDraft(ResolvePath(services.Paths.RepoRoot, filteredArguments[0]));
    }

    private static OperatorCommandResult RunUpdateCardCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: update-card <card-id> <json-path> [--repo-id <id>]")
            : scoped.OperatorSurfaceService.UpdateCardDraft(filteredArguments[0], ResolvePath(services.Paths.RepoRoot, filteredArguments[1]));
    }

    private static OperatorCommandResult RunListCardsCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        var surface = new LocalHostSurfaceService(scoped);
        return OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildCardList(ResolveOption(filteredArguments, "--state"))));
    }

    private static OperatorCommandResult RunInspectCardCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: inspect-card <card-id> [--repo-id <id>]")
            : RunInspectCardWithServices(scoped, filteredArguments[0]);
    }

    private static OperatorCommandResult RunSetCardStatusAliasCommand(RuntimeServices services, IReadOnlyList<string> arguments, CardLifecycleState state, string commandName)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count == 0
            ? OperatorCommandResult.Failure($"Usage: {commandName} <card-id> [reason...] [--repo-id <id>]")
            : scoped.OperatorSurfaceService.SetCardStatus(filteredArguments[0], state, filteredArguments.Count > 1 ? string.Join(' ', filteredArguments.Skip(1)) : null);
    }

    private static OperatorCommandResult RunCreateTaskGraphDraftCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: create-taskgraph-draft <json-path> [--repo-id <id>]")
            : scoped.OperatorSurfaceService.CreateTaskGraphDraft(ResolvePath(services.Paths.RepoRoot, filteredArguments[0]));
    }

    private static OperatorCommandResult RunApproveTaskGraphDraftCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: approve-taskgraph-draft <draft-id> [reason...] [--repo-id <id>]")
            : scoped.OperatorSurfaceService.ApproveTaskGraphDraft(filteredArguments[0], filteredArguments.Count == 1 ? "Approved through host." : string.Join(' ', filteredArguments.Skip(1)));
    }

    private static OperatorCommandResult RunApproveSuggestedTaskCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return arguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: approve-suggested-task <suggestion-id> <reason...>")
            : services.OperatorSurfaceService.ApproveSuggestedTask(arguments[0], string.Join(' ', arguments.Skip(1)));
    }

    private static OperatorCommandResult RunSupersedeCardTasksCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        var scoped = ResolveRepoScopedServices(services, arguments, out var filteredArguments);
        return filteredArguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: supersede-card-tasks <card-id> <reason...> [--repo-id <id>]")
            : scoped.OperatorSurfaceService.SupersedeCardTasks(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)));
    }

    private static OperatorCommandResult RunApproveTaskCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        return arguments.Count == 0
            ? OperatorCommandResult.Failure("Usage: approve-task <task-id>")
            : services.OperatorSurfaceService.ApproveTask(arguments[0]);
    }

    private static OperatorCommandResult RunReviewTaskCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        return filteredArguments.Count < 3
            ? OperatorCommandResult.Failure("Usage: review-task <task-id> <verdict> <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.ReviewTask(filteredArguments[0], filteredArguments[1], string.Join(' ', filteredArguments.Skip(2)))
                : services.OperatorSurfaceService.ReviewTaskAsActor(filteredArguments[0], filteredArguments[1], string.Join(' ', filteredArguments.Skip(2)), actorKind.Value, actorIdentity!);
    }

    private static OperatorCommandResult RunApproveReviewCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        if (filteredArguments.Count < 2)
        {
            return OperatorCommandResult.Failure("Usage: approve-review <task-id> [--provisional] <reason...> [--actor-kind <kind>] [--actor-identity <id>]");
        }

        var provisional = filteredArguments.Any(argument => string.Equals(argument, "--provisional", StringComparison.OrdinalIgnoreCase));
        var filtered = filteredArguments.Where(argument => !string.Equals(argument, "--provisional", StringComparison.OrdinalIgnoreCase)).ToArray();
        return filtered.Length < 2
            ? OperatorCommandResult.Failure("Usage: approve-review <task-id> [--provisional] <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.ApproveReview(
                    filtered[0],
                    string.Join(' ', filtered.Skip(1)),
                    provisional: provisional)
                : services.OperatorSurfaceService.ApproveReviewAsActor(
                    filtered[0],
                    string.Join(' ', filtered.Skip(1)),
                    actorKind.Value,
                    actorIdentity!,
                    provisional: provisional);
    }

    private static OperatorCommandResult RunRejectReviewCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        return filteredArguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: reject-review <task-id> <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.RejectReview(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)))
                : services.OperatorSurfaceService.RejectReviewAsActor(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)), actorKind.Value, actorIdentity!);
    }

    private static OperatorCommandResult RunReopenReviewCommand(RuntimeServices services, IReadOnlyList<string> arguments)
    {
        if (!ReviewActorCommandArguments.TryParse(arguments, out var filteredArguments, out var actorKind, out var actorIdentity, out var actorError))
        {
            return OperatorCommandResult.Failure(actorError ?? "Invalid actor options.");
        }

        return filteredArguments.Count < 2
            ? OperatorCommandResult.Failure("Usage: reopen-review <task-id> <reason...> [--actor-kind <kind>] [--actor-identity <id>]")
            : actorKind is null
                ? services.OperatorSurfaceService.ReopenReview(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)))
                : services.OperatorSurfaceService.ReopenReviewAsActor(filteredArguments[0], string.Join(' ', filteredArguments.Skip(1)), actorKind.Value, actorIdentity!);
    }
}
