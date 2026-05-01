using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Planning;

namespace Carves.Runtime.Infrastructure.AI;

public static class PlannerAdapterFactory
{
    public static PlannerAdapterRegistry Create(AiProviderConfig config)
    {
        var plannerConfig = config.ResolveForRole("planner");
        var localAdapter = new LocalPlannerAdapter("Local governed planner remains available as the deterministic fallback.");

        IPlannerAdapter activeAdapter = plannerConfig.Provider.ToLowerInvariant() switch
        {
            "claude" =>
                new ClaudePlannerAdapter(
                    new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(Math.Max(5, plannerConfig.RequestTimeoutSeconds)),
                    },
                    plannerConfig,
                    BuildSelectionReason(plannerConfig.Provider, "claude")),
            "codex" =>
                new CodexPlannerAdapter(
                    new HttpClient
                    {
                        Timeout = TimeSpan.FromSeconds(Math.Max(5, plannerConfig.RequestTimeoutSeconds)),
                    },
                    plannerConfig,
                    BuildSelectionReason(plannerConfig.Provider, "codex")),
            _ => localAdapter,
        };

        var adapters = new List<IPlannerAdapter> { localAdapter };
        if (!ReferenceEquals(activeAdapter, localAdapter))
        {
            adapters.Add(activeAdapter);
        }

        return new PlannerAdapterRegistry(adapters, activeAdapter);
    }

    private static string BuildSelectionReason(string configuredProvider, string adapterProvider)
    {
        return string.Equals(configuredProvider, adapterProvider, StringComparison.OrdinalIgnoreCase)
            ? $"Configured AI provider '{configuredProvider}' selected the {adapterProvider} planner adapter."
            : $"Planner adapter '{adapterProvider}' is available but inactive because provider '{configuredProvider}' is configured.";
    }
}
