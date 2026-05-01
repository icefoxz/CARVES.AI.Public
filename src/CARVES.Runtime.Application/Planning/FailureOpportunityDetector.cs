using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class FailureOpportunityDetector : IOpportunityDetector
{
    private readonly IRuntimeArtifactRepository artifactRepository;

    public FailureOpportunityDetector(IRuntimeArtifactRepository artifactRepository)
    {
        this.artifactRepository = artifactRepository;
    }

    public string Name => "runtime-failure";

    public IReadOnlyList<OpportunityObservation> Detect()
    {
        var failure = artifactRepository.TryLoadLatestRuntimeFailure();
        if (failure is null)
        {
            return Array.Empty<OpportunityObservation>();
        }

        var relatedFiles = failure.TaskId is null
            ? Array.Empty<string>()
            : [$".ai/tasks/nodes/{failure.TaskId}.json"];

        return
        [
            new OpportunityObservation(
                OpportunitySource.FailureRecovery,
                failure.FailureId,
                $"Recover from runtime failure {failure.FailureType}",
                failure.Reason,
                failure.Reason,
                OpportunitySeverity.High,
                0.85,
                relatedFiles,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["failure_id"] = failure.FailureId,
                    ["failure_type"] = failure.FailureType.ToString(),
                    ["failure_action"] = failure.Action.ToString(),
                    ["task_id"] = failure.TaskId ?? string.Empty,
                })
        ];
    }
}
