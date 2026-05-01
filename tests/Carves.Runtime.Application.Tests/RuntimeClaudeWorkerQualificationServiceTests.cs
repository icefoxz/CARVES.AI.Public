using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeClaudeWorkerQualificationServiceTests
{
    [Fact]
    public void BuildSurface_ProjectsQualifiedAndClosedClaudeLanes()
    {
        var service = new RuntimeClaudeWorkerQualificationService();

        var surface = service.BuildSurface();

        Assert.Equal("runtime-claude-worker-qualification", surface.SurfaceId);
        Assert.Contains(surface.CurrentPolicy.Lanes, item => item.RoutingIntent == "review_summary" && item.Allowed);
        Assert.Contains(surface.CurrentPolicy.Lanes, item => item.RoutingIntent == "failure_summary" && item.Allowed);
        Assert.Contains(surface.CurrentPolicy.Lanes, item => item.RoutingIntent == "reasoning_summary" && item.Allowed);
        Assert.Contains(surface.CurrentPolicy.Lanes, item => item.RoutingIntent == "structured_output" && item.Allowed);
        Assert.Contains(surface.CurrentPolicy.Lanes, item => item.RoutingIntent == "patch_draft" && !item.Allowed);
    }

    [Fact]
    public void Evaluate_AllowsQualifiedReviewLaneAndRejectsPatchDraft()
    {
        var service = new RuntimeClaudeWorkerQualificationService();
        var backend = CreateClaudeBackend();

        var reviewTask = new TaskNode
        {
            TaskId = "T-CLAUDE-REVIEW",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "review_summary",
            },
        };
        var patchTask = new TaskNode
        {
            TaskId = "T-CLAUDE-PATCH",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "patch_draft",
            },
        };

        var reviewDecision = service.Evaluate(reviewTask, backend);
        var patchDecision = service.Evaluate(patchTask, backend);

        Assert.True(reviewDecision.Allowed);
        Assert.Equal("claude_worker_lane_qualified", reviewDecision.ReasonCode);
        Assert.Equal("claude-review-summary", reviewDecision.LaneId);
        Assert.False(patchDecision.Allowed);
        Assert.Equal("claude_worker_lane_out_of_scope", patchDecision.ReasonCode);
        Assert.Equal("claude-patch-draft", patchDecision.LaneId);
    }

    [Fact]
    public void Evaluate_RequiresExplicitRoutingIntentForClaudeTaskSelection()
    {
        var service = new RuntimeClaudeWorkerQualificationService();
        var backend = CreateClaudeBackend();

        var decision = service.Evaluate(new TaskNode
        {
            TaskId = "T-CLAUDE-NO-INTENT",
        }, backend);

        Assert.False(decision.Allowed);
        Assert.Equal("claude_worker_requires_explicit_routing_intent", decision.ReasonCode);
    }

    private static WorkerBackendDescriptor CreateClaudeBackend()
    {
        return new WorkerBackendDescriptor
        {
            BackendId = "claude_api",
            ProviderId = "claude",
            AdapterId = "ClaudeWorkerAdapter",
            DisplayName = "Claude API Worker",
            ProtocolFamily = "anthropic_native",
            RequestFamily = "messages_api",
            RoutingProfiles = ["claude-worker-bounded"],
            CompatibleTrustProfiles = ["workspace_build_test"],
            Capabilities = new WorkerProviderCapabilities
            {
                SupportsExecution = true,
                SupportsNetworkAccess = true,
                SupportsDotNetBuild = true,
                SupportsSystemPrompt = true,
            },
        };
    }
}
