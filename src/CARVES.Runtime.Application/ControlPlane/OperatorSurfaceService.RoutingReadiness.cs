using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectValidationCoverage(string? candidateId = null)
    {
        return OperatorSurfaceFormatter.ValidationCoverageMatrix(validationCoverageMatrixService.Build(candidateId));
    }

    public OperatorCommandResult ApiValidationCoverage(string? candidateId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(validationCoverageMatrixService.Build(candidateId)));
    }

    public OperatorCommandResult InspectPromotionReadiness(string? candidateId = null)
    {
        return OperatorSurfaceFormatter.RoutingCandidateReadiness(routingCandidateReadinessService.Build(candidateId));
    }

    public OperatorCommandResult ApiPromotionReadiness(string? candidateId = null)
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(routingCandidateReadinessService.Build(candidateId)));
    }

    public OperatorCommandResult InspectCodexRoutingEligibility()
    {
        var backends = providerRegistryService.ListWorkerBackends()
            .Where(backend => string.Equals(backend.ProviderId, "codex", StringComparison.Ordinal))
            .ToArray();
        var selection = workerSelectionPolicyService.Evaluate(task: null, repoId: null, allowFallback: true);
        return OperatorSurfaceFormatter.CodexRoutingEligibility(backends, selection);
    }

    public OperatorCommandResult ApiCodexRoutingEligibility()
    {
        var backends = providerRegistryService.ListWorkerBackends()
            .Where(backend => string.Equals(backend.ProviderId, "codex", StringComparison.Ordinal))
            .ToArray();
        var selection = workerSelectionPolicyService.Evaluate(task: null, repoId: null, allowFallback: true);
        return OperatorCommandResult.Success(operatorApiService.ToJson(new
        {
            codex_backends = backends,
            selection_candidates = selection.Candidates
                .Where(candidate => string.Equals(candidate.ProviderId, "codex", StringComparison.OrdinalIgnoreCase))
                .ToArray(),
        }));
    }
}
