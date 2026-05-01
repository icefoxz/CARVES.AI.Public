using Carves.Runtime.Application.Evidence;
using Carves.Runtime.Domain.Evidence;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    private IReadOnlyList<string> ResolveReviewSourceEvidenceIds(string taskId)
    {
        var evidenceStore = new RuntimeEvidenceStoreService(paths);
        var sourceIds = new List<string>();
        var runEvidence = evidenceStore.TryGetLatest(taskId, RuntimeEvidenceKind.ExecutionRun);
        if (runEvidence is not null)
        {
            sourceIds.Add(runEvidence.EvidenceId);
        }

        var planningEvidence = evidenceStore.TryGetLatest(taskId, RuntimeEvidenceKind.Planning);
        if (planningEvidence is not null)
        {
            sourceIds.Add(planningEvidence.EvidenceId);
        }

        return sourceIds.Distinct(StringComparer.Ordinal).ToArray();
    }
}
