using System.Text.Json;
using System.Text.Json.Nodes;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Host;

internal sealed partial class LocalHostSurfaceService
{
    private JsonObject? BuildReviewEvidenceGateNode(
        TaskNode task,
        PlannerReviewArtifact? reviewArtifact,
        WorkerExecutionArtifact? workerArtifact)
    {
        if (reviewArtifact is null)
        {
            return null;
        }

        var projectionService = new ReviewEvidenceProjectionService(services.Paths.RepoRoot, services.GitClient);
        return JsonSerializer.SerializeToNode(
                projectionService.Build(task, reviewArtifact, workerArtifact),
                JsonOptions)
            ?.AsObject();
    }

    private JsonObject? BuildReviewClosureBundleNode(PlannerReviewArtifact? reviewArtifact)
    {
        return reviewArtifact is null
            ? null
            : JsonSerializer.SerializeToNode(reviewArtifact.ClosureBundle, JsonOptions)?.AsObject();
    }
}
