using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Host;

internal static partial class LocalHostCommandDispatcher
{
    private static string ResolvePath(string repoRoot, string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string? ResolveOptionalPath(string repoRoot, string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : ResolvePath(repoRoot, path);
    }

    private static string? ResolveOption(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new InvalidOperationException($"Usage error: option '{option}' requires a value.");
            }

            return arguments[index + 1];
        }

        return null;
    }

    private static int ResolveOptionalPositiveInt(IReadOnlyList<string> arguments, string option, int defaultValue)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || !int.TryParse(arguments[index + 1], out var parsed) || parsed <= 0)
            {
                throw new InvalidOperationException($"Usage: {option} <positive-integer>");
            }

            return parsed;
        }

        return defaultValue;
    }

    private static int? ResolveOptionalInt(IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count || !int.TryParse(arguments[index + 1], out var parsed) || parsed <= 0)
            {
                throw new InvalidOperationException($"Usage: {option} <positive-integer>");
            }

            return parsed;
        }

        return null;
    }

    private static double ResolveOptionalDouble(IReadOnlyList<string> arguments, string option, double defaultValue)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count
                || !double.TryParse(arguments[index + 1], out var parsed)
                || double.IsNaN(parsed)
                || double.IsInfinity(parsed)
                || parsed < 0
                || parsed > 1)
            {
                throw new InvalidOperationException($"Usage: {option} <0.0-1.0>");
            }

            return parsed;
        }

        return defaultValue;
    }

    private static IReadOnlyList<string> ResolveMultiOption(IReadOnlyList<string> arguments, string option)
    {
        var values = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new InvalidOperationException($"Usage error: option '{option}' requires a value.");
            }

            values.Add(arguments[index + 1]);
            index += 1;
        }

        return values;
    }

    private static WorkerSelectionOptions? BuildWorkerSelectionOptions(string? routingIntentOverride, string? routingModuleOverride, bool forceFallbackOnly)
    {
        if (string.IsNullOrWhiteSpace(routingIntentOverride) && string.IsNullOrWhiteSpace(routingModuleOverride) && !forceFallbackOnly)
        {
            return null;
        }

        return new WorkerSelectionOptions
        {
            ForceFallbackOnly = forceFallbackOnly,
            RoutingIntentOverride = string.IsNullOrWhiteSpace(routingIntentOverride) ? null : routingIntentOverride,
            RoutingModuleIdOverride = string.IsNullOrWhiteSpace(routingModuleOverride) ? null : routingModuleOverride,
        };
    }

    private static PlannerWakeReason ParsePlannerWakeReason(string? value, PlannerWakeReason fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<PlannerWakeReason>(normalized, true, out var parsed) ? parsed : fallback;
    }

    private static PlannerSleepReason ParsePlannerSleepReason(string? value, PlannerSleepReason fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<PlannerSleepReason>(normalized, true, out var parsed) ? parsed : fallback;
    }

    private static string? ResolvePrimaryArgument(IReadOnlyList<string> arguments, IReadOnlyCollection<string> optionsWithValues, IReadOnlyCollection<string> flagOptions)
    {
        return ResolvePrimaryArguments(arguments, optionsWithValues, flagOptions).FirstOrDefault();
    }

    private static IReadOnlyList<string> ResolvePrimaryArguments(IReadOnlyList<string> arguments, IReadOnlyCollection<string> optionsWithValues, IReadOnlyCollection<string> flagOptions)
    {
        var consumedIndexes = new HashSet<int>();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (optionsWithValues.Contains(arguments[index], StringComparer.OrdinalIgnoreCase))
            {
                consumedIndexes.Add(index);
                if (index + 1 < arguments.Count)
                {
                    consumedIndexes.Add(index + 1);
                }

                continue;
            }

            if (flagOptions.Contains(arguments[index], StringComparer.OrdinalIgnoreCase))
            {
                consumedIndexes.Add(index);
            }
        }

        var resolved = new List<string>();
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!consumedIndexes.Contains(index))
            {
                resolved.Add(arguments[index]);
            }
        }

        return resolved;
    }

    private static RuntimeServices ResolveRepoScopedServices(RuntimeServices services, IReadOnlyList<string> arguments, out IReadOnlyList<string> filteredArguments)
    {
        filteredArguments = StripOption(arguments, "--repo-id", out var repoId);
        if (string.IsNullOrWhiteSpace(repoId))
        {
            return services;
        }

        var descriptor = services.RepoRegistryService.Inspect(repoId);
        return RuntimeComposition.Create(descriptor.RepoPath);
    }

    private static IReadOnlyList<string> StripOption(IReadOnlyList<string> arguments, string option, out string? value)
    {
        var filtered = new List<string>(arguments.Count);
        value = null;
        for (var index = 0; index < arguments.Count; index++)
        {
            if (!string.Equals(arguments[index], option, StringComparison.OrdinalIgnoreCase))
            {
                filtered.Add(arguments[index]);
                continue;
            }

            if (index + 1 >= arguments.Count)
            {
                throw new InvalidOperationException($"Usage error: option '{option}' requires a value.");
            }

            value = arguments[index + 1];
            index += 1;
        }

        return filtered;
    }

    private static OperatorCommandResult RunInspectCardWithServices(RuntimeServices services, string cardId)
    {
        var surface = new LocalHostSurfaceService(services);
        return OperatorCommandResult.Success(surface.ToPrettyJson(surface.BuildCardInspect(cardId)));
    }

    private static Carves.Runtime.Domain.Planning.CardLifecycleState ParseCardLifecycleState(string value)
    {
        if (Enum.TryParse<Carves.Runtime.Domain.Planning.CardLifecycleState>(value, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Usage: card status <card-id> <draft|reviewed|approved|rejected|archived> [reason...]");
    }

    private static ActorSessionKind? ParseActorSessionKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<ActorSessionKind>(value, true, out var parsed) ? parsed : null;
    }

    private static OwnershipScope? ParseOwnershipScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<OwnershipScope>(value, true, out var parsed) ? parsed : null;
    }

    private static OperatorOsEventKind? ParseOperatorOsEventKind(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<OperatorOsEventKind>(value, true, out var parsed) ? parsed : null;
    }

    private static GuidedPlanningPosture ParseGuidedPlanningPosture(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<GuidedPlanningPosture>(normalized, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Usage: intent candidate <candidate-card-id> <emerging|needs_confirmation|wobbling|grounded|paused|forbidden|ready_to_plan>");
    }

    private static GuidedPlanningDecisionStatus ParseGuidedPlanningDecisionStatus(string value)
    {
        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<GuidedPlanningDecisionStatus>(normalized, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException("Usage: intent decision <decision-id> <open|resolved|paused|forbidden>");
    }
}
