using Carves.Runtime.Application.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeTokenWorkerWrapperCanaryServiceTests
{
    [Fact]
    public void ResolveWorkerSystemInstructions_StaysDefaultOffWithoutExplicitEnablement()
    {
        var service = new RuntimeTokenWorkerWrapperCanaryService(_ => null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.False(decision.CandidateApplied);
        Assert.Equal("default_off", decision.DecisionReason);
        Assert.Equal("original", decision.EffectiveInstructions);
    }

    [Fact]
    public void ResolveWorkerSystemInstructions_AppliesCandidateOnlyForPinnedAllowlistedWorkerScope()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.CanaryEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var service = new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.True(decision.CandidateApplied);
        Assert.Equal("candidate_applied", decision.DecisionReason);
        Assert.Contains("Hard boundaries", decision.EffectiveInstructions, StringComparison.Ordinal);
        Assert.Equal(RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion, decision.CandidateVersion);
    }

    [Fact]
    public void ResolveWorkerSystemInstructions_KillSwitchForcesFallback()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.CanaryEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.GlobalKillSwitchEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var service = new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.False(decision.CandidateApplied);
        Assert.Equal("fallback_original", decision.DecisionMode);
        Assert.Equal("kill_switch_active", decision.DecisionReason);
        Assert.Equal("original", decision.EffectiveInstructions);
    }

    [Fact]
    public void ResolveWorkerSystemInstructions_UsesLimitedMainPathDefaultWhenFrozenScopeIsEnabled()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var service = new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.True(decision.MainPathDefaultEnabled);
        Assert.True(decision.CandidateApplied);
        Assert.Equal("limited_main_path_default", decision.DecisionMode);
        Assert.Equal("main_path_default", decision.DecisionReason);
        Assert.Contains("Hard boundaries", decision.EffectiveInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveWorkerSystemInstructions_MainPathDefaultFallsBackWhenCandidateVersionIsNotPinned()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = "wrong_version",
        };
        var service = new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.True(decision.MainPathDefaultEnabled);
        Assert.False(decision.CandidateApplied);
        Assert.Equal("fallback_original", decision.DecisionMode);
        Assert.Equal("candidate_version_not_pinned", decision.DecisionReason);
        Assert.Equal("original", decision.EffectiveInstructions);
    }

    [Fact]
    public void ResolveWorkerSystemInstructions_MainPathDefaultFallsBackWhenRequestKindIsOutsideFrozenScope()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "planner",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.instructions",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var service = new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.True(decision.MainPathDefaultEnabled);
        Assert.False(decision.CandidateApplied);
        Assert.Equal("fallback_original", decision.DecisionMode);
        Assert.Equal("request_kind_not_allowlisted", decision.DecisionReason);
        Assert.Equal("original", decision.EffectiveInstructions);
    }

    [Fact]
    public void ResolveWorkerSystemInstructions_MainPathDefaultFallsBackWhenSurfaceIsOutsideFrozenScope()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RuntimeTokenWorkerWrapperCanaryService.MainPathDefaultEnabledEnvironmentVariable] = "true",
            [RuntimeTokenWorkerWrapperCanaryService.RequestKindAllowlistEnvironmentVariable] = "worker",
            [RuntimeTokenWorkerWrapperCanaryService.SurfaceAllowlistEnvironmentVariable] = "worker:system:$.different",
            [RuntimeTokenWorkerWrapperCanaryService.CandidateVersionEnvironmentVariable] = RuntimeTokenWorkerWrapperCanaryService.ApprovedCandidateVersion,
        };
        var service = new RuntimeTokenWorkerWrapperCanaryService(name => values.TryGetValue(name, out var value) ? value : null);

        var decision = service.ResolveWorkerSystemInstructions(
            CreateTask(),
            CreatePacket(),
            CreateBudget(),
            "original");

        Assert.True(decision.MainPathDefaultEnabled);
        Assert.False(decision.CandidateApplied);
        Assert.Equal("fallback_original", decision.DecisionMode);
        Assert.Equal("surface_not_allowlisted", decision.DecisionReason);
        Assert.Equal("original", decision.EffectiveInstructions);
    }

    private static TaskNode CreateTask()
    {
        return new TaskNode
        {
            TaskId = "T-WORKER-CANARY-001",
            Title = "Apply worker canary",
            Description = "Test worker wrapper canary routing.",
            TaskType = TaskType.Execution,
            Acceptance = ["stay in scope"],
        };
    }

    private static ExecutionPacket CreatePacket()
    {
        return new ExecutionPacket
        {
            PacketId = "packet-worker-canary-001",
            PlannerIntent = PlannerIntent.Execution,
            StopConditions = ["stop if out of scope"],
            Budgets = new ExecutionPacketBudgets
            {
                MaxFilesChanged = 2,
                MaxLinesChanged = 40,
                MaxShellCommands = 5,
            },
        };
    }

    private static WorkerRequestBudget CreateBudget()
    {
        return new WorkerRequestBudget
        {
            PolicyId = "runtime_governed_dynamic_request_budget_v1",
            TimeoutSeconds = 30,
            ProviderBaselineSeconds = 30,
            ExecutionBudgetSize = ExecutionBudgetSize.Small,
            ConfidenceLevel = ExecutionConfidenceLevel.High,
            MaxDurationMinutes = 10,
            Reasons = [],
        };
    }
}
