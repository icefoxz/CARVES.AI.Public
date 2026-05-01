using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Tests;

public sealed class ProviderHealthActionabilityProjectionServiceTests
{
    [Fact]
    public void Build_ClassifiesOptionalAndDisabledBackendsAsNonActionable()
    {
        var service = new ProviderHealthActionabilityProjectionService();

        var projection = service.Build(
        [
            new ProviderHealthRecord { BackendId = "codex_cli", State = WorkerBackendHealthState.Healthy },
            new ProviderHealthRecord { BackendId = "openai_api", State = WorkerBackendHealthState.Unavailable },
            new ProviderHealthRecord { BackendId = "claude_api", State = WorkerBackendHealthState.Disabled },
        ],
        new WorkerSelectionDecision { SelectedBackendId = "codex_cli" },
        preferredBackendId: "codex_cli");

        Assert.Equal(0, projection.UnavailableActionableProviderCount);
        Assert.Equal(1, projection.OptionalIssueCount);
        Assert.Equal(1, projection.DisabledIssueCount);
        Assert.Contains(projection.Providers, item => item.BackendId == "openai_api" && !item.ActionabilityRelevant && item.SelectionRole == "optional");
        Assert.Contains(projection.Providers, item => item.BackendId == "claude_api" && !item.ActionabilityRelevant && item.SelectionRole == "disabled");
    }

    [Fact]
    public void Build_ClassifiesPreferredBackendAsActionableWhenFallbackIsSelected()
    {
        var service = new ProviderHealthActionabilityProjectionService();

        var projection = service.Build(
        [
            new ProviderHealthRecord { BackendId = "codex_cli", State = WorkerBackendHealthState.Unavailable },
            new ProviderHealthRecord { BackendId = "codex_sdk", State = WorkerBackendHealthState.Healthy },
        ],
        new WorkerSelectionDecision { SelectedBackendId = "codex_sdk", UsedFallback = true },
        preferredBackendId: "codex_cli");

        Assert.True(projection.FallbackInUse);
        Assert.Equal(1, projection.HealthyActionableProviderCount);
        Assert.Equal(1, projection.UnavailableActionableProviderCount);
        Assert.Contains(projection.Providers, item => item.BackendId == "codex_cli" && item.ActionabilityRelevant && item.SelectionRole == "preferred");
    }
}
