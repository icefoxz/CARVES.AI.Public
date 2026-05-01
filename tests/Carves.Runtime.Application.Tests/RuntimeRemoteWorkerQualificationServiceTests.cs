using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeRemoteWorkerQualificationServiceTests
{
    [Fact]
    public void BuildSurface_ProjectsClaudeAndGeminiPolicies()
    {
        var service = new RuntimeRemoteWorkerQualificationService();

        var surface = service.BuildSurface();

        Assert.Equal("runtime-remote-worker-qualification", surface.SurfaceId);
        Assert.Contains(surface.CurrentPolicies, item => item.ProviderId == "claude" && item.BackendId == "claude_api");
        Assert.Contains(surface.CurrentPolicies, item => item.ProviderId == "gemini" && item.BackendId == "gemini_api");
    }

    [Fact]
    public void Evaluate_AllowsGeminiStructuredOutputAndRejectsPatchDraft()
    {
        var service = new RuntimeRemoteWorkerQualificationService();
        var backend = new WorkerBackendDescriptor
        {
            BackendId = "gemini_api",
            ProviderId = "gemini",
            AdapterId = "GeminiWorkerAdapter",
            ProtocolFamily = "gemini_native",
        };
        var structuredTask = new TaskNode
        {
            TaskId = "T-GEMINI-STRUCTURED",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "structured_output",
            },
        };
        var patchTask = new TaskNode
        {
            TaskId = "T-GEMINI-PATCH",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["routing_intent"] = "patch_draft",
            },
        };

        var structuredDecision = service.Evaluate(structuredTask, backend);
        var patchDecision = service.Evaluate(patchTask, backend);

        Assert.True(structuredDecision.Allowed);
        Assert.Equal("remote_worker_lane_qualified", structuredDecision.ReasonCode);
        Assert.Equal("gemini-structured-output", structuredDecision.LaneId);
        Assert.False(patchDecision.Allowed);
        Assert.Equal("remote_worker_lane_out_of_scope", patchDecision.ReasonCode);
        Assert.Equal("gemini-patch-draft", patchDecision.LaneId);
    }
}
