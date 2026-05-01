using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Tests;

public sealed class OperatorActionabilityServiceTests
{
    private static readonly ProviderHealthActionabilityProjectionService ProjectionService = new();

    [Fact]
    public void Assess_ReturnsHumanRequiredForPendingApprovals()
    {
        var service = new OperatorActionabilityService();

        var assessment = service.Assess(
            session: new RuntimeSessionState { Status = RuntimeSessionStatus.Idle },
            pendingApprovalCount: 1,
            reviewTaskCount: 0,
            blockedTaskCount: 0,
            activeIncidentCount: 0,
            nonBlockingNoiseCount: 0,
            providerHealth: BuildProjection([], new WorkerSelectionDecision { SelectedBackendId = "codex_cli" }));

        Assert.Equal(OperatorActionabilityState.HumanRequired, assessment.State);
        Assert.Equal(OperatorActionabilityReason.PendingApproval, assessment.Reason);
    }

    [Fact]
    public void Assess_ReturnsBlockedForActiveBlockers()
    {
        var service = new OperatorActionabilityService();

        var assessment = service.Assess(
            session: new RuntimeSessionState { Status = RuntimeSessionStatus.Idle },
            pendingApprovalCount: 0,
            reviewTaskCount: 0,
            blockedTaskCount: 1,
            activeIncidentCount: 0,
            nonBlockingNoiseCount: 0,
            providerHealth: BuildProjection([], new WorkerSelectionDecision { SelectedBackendId = "codex_cli" }));

        Assert.Equal(OperatorActionabilityState.Blocked, assessment.State);
        Assert.Equal(OperatorActionabilityReason.ActiveBlocker, assessment.Reason);
    }

    [Fact]
    public void Assess_ReturnsAutonomousDegradedForSelectedLaneDegradation()
    {
        var service = new OperatorActionabilityService();

        var assessment = service.Assess(
            session: new RuntimeSessionState { Status = RuntimeSessionStatus.Idle, LoopActionability = RuntimeActionability.HumanActionable },
            pendingApprovalCount: 0,
            reviewTaskCount: 0,
            blockedTaskCount: 0,
            activeIncidentCount: 0,
            nonBlockingNoiseCount: 0,
            providerHealth: BuildProjection(
            [
                new ProviderHealthRecord { BackendId = "codex_cli", State = WorkerBackendHealthState.Degraded },
                new ProviderHealthRecord { BackendId = "null_worker", State = WorkerBackendHealthState.Healthy },
            ],
            new WorkerSelectionDecision { SelectedBackendId = "codex_cli" },
            preferredBackendId: "codex_cli"));

        Assert.Equal(OperatorActionabilityState.AutonomousDegraded, assessment.State);
        Assert.Equal(OperatorActionabilityReason.ProviderDegraded, assessment.Reason);
        Assert.Equal(RuntimeActionability.HumanActionable, assessment.SessionActionability);
        Assert.Equal(0, assessment.HealthyProviderCount);
        Assert.Equal(1, assessment.DegradedProviderCount);
    }

    [Fact]
    public void Assess_ReturnsAdvisoryOnlyForOptionalProviderResidue()
    {
        var service = new OperatorActionabilityService();

        var assessment = service.Assess(
            session: new RuntimeSessionState { Status = RuntimeSessionStatus.Idle, LoopActionability = RuntimeActionability.PlannerActionable },
            pendingApprovalCount: 0,
            reviewTaskCount: 0,
            blockedTaskCount: 0,
            activeIncidentCount: 0,
            nonBlockingNoiseCount: 0,
            providerHealth: BuildProjection(
            [
                new ProviderHealthRecord { BackendId = "codex_cli", State = WorkerBackendHealthState.Healthy },
                new ProviderHealthRecord { BackendId = "openai_api", State = WorkerBackendHealthState.Unavailable },
                new ProviderHealthRecord { BackendId = "claude_api", State = WorkerBackendHealthState.Disabled },
            ],
            new WorkerSelectionDecision { SelectedBackendId = "codex_cli" },
            preferredBackendId: "codex_cli"));

        Assert.Equal(OperatorActionabilityState.AdvisoryOnly, assessment.State);
        Assert.Equal(OperatorActionabilityReason.ProviderOptionalResidue, assessment.Reason);
        Assert.False(assessment.RequiresHuman);
        Assert.Equal(1, assessment.OptionalProviderIssueCount);
        Assert.Equal(1, assessment.DisabledProviderCount);
    }

    [Fact]
    public void Assess_ReturnsAutonomousDegradedWhenFallbackIsInUse()
    {
        var service = new OperatorActionabilityService();

        var assessment = service.Assess(
            session: new RuntimeSessionState { Status = RuntimeSessionStatus.Idle, LoopActionability = RuntimeActionability.PlannerActionable },
            pendingApprovalCount: 0,
            reviewTaskCount: 0,
            blockedTaskCount: 0,
            activeIncidentCount: 0,
            nonBlockingNoiseCount: 0,
            providerHealth: BuildProjection(
            [
                new ProviderHealthRecord { BackendId = "codex_cli", State = WorkerBackendHealthState.Unavailable },
                new ProviderHealthRecord { BackendId = "codex_sdk", State = WorkerBackendHealthState.Healthy },
            ],
            new WorkerSelectionDecision { SelectedBackendId = "codex_sdk", UsedFallback = true },
            preferredBackendId: "codex_cli"));

        Assert.Equal(OperatorActionabilityState.AutonomousDegraded, assessment.State);
        Assert.Equal(OperatorActionabilityReason.ProviderUnavailable, assessment.Reason);
        Assert.True(assessment.FallbackInUse);
        Assert.Equal("codex_sdk", assessment.SelectedBackendId);
        Assert.Equal("codex_cli", assessment.PreferredBackendId);
    }

    [Fact]
    public void Assess_ReturnsAdvisoryOnlyForAutonomousSessionWithNoise()
    {
        var service = new OperatorActionabilityService();

        var assessment = service.Assess(
            session: new RuntimeSessionState { Status = RuntimeSessionStatus.Idle, LoopActionability = RuntimeActionability.PlannerActionable },
            pendingApprovalCount: 0,
            reviewTaskCount: 0,
            blockedTaskCount: 0,
            activeIncidentCount: 0,
            nonBlockingNoiseCount: 3,
            providerHealth: BuildProjection(
            [
                new ProviderHealthRecord { BackendId = "codex_cli", State = WorkerBackendHealthState.Healthy },
            ],
            new WorkerSelectionDecision { SelectedBackendId = "codex_cli" },
            preferredBackendId: "codex_cli"));

        Assert.Equal(OperatorActionabilityState.AdvisoryOnly, assessment.State);
        Assert.Equal(OperatorActionabilityReason.NonBlockingNoise, assessment.Reason);
        Assert.False(assessment.RequiresHuman);
    }

    private static ProviderHealthActionabilityProjection BuildProjection(
        IReadOnlyList<ProviderHealthRecord> records,
        WorkerSelectionDecision selection,
        string? preferredBackendId = null)
    {
        return ProjectionService.Build(records, selection, preferredBackendId);
    }
}
