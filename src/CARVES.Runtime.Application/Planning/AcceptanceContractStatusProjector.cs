using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public static class AcceptanceContractStatusProjector
{
    public static AcceptanceContract? MoveToHumanReview(AcceptanceContract? contract, params string[] relatedArtifacts)
    {
        return Project(contract, AcceptanceContractLifecycleStatus.HumanReview, relatedArtifacts);
    }

    public static AcceptanceContract? ApplyHumanDecision(
        AcceptanceContract? contract,
        AcceptanceContractHumanDecision decision,
        params string[] relatedArtifacts)
    {
        var status = decision switch
        {
            AcceptanceContractHumanDecision.Accept => AcceptanceContractLifecycleStatus.Accepted,
            AcceptanceContractHumanDecision.ProvisionalAccept => AcceptanceContractLifecycleStatus.ProvisionalAccepted,
            AcceptanceContractHumanDecision.Reject => AcceptanceContractLifecycleStatus.Rejected,
            AcceptanceContractHumanDecision.Reopen => AcceptanceContractLifecycleStatus.Reopened,
            _ => contract?.Status ?? AcceptanceContractLifecycleStatus.Draft,
        };

        return Project(contract, status, relatedArtifacts);
    }

    private static AcceptanceContract? Project(
        AcceptanceContract? contract,
        AcceptanceContractLifecycleStatus status,
        IReadOnlyList<string> relatedArtifacts)
    {
        if (contract is null)
        {
            return null;
        }

        var mergedArtifacts = contract.Traceability.RelatedArtifacts
            .Concat(relatedArtifacts.Where(path => !string.IsNullOrWhiteSpace(path)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new AcceptanceContract
        {
            ContractId = contract.ContractId,
            Title = contract.Title,
            Status = status,
            Owner = contract.Owner,
            CreatedAtUtc = contract.CreatedAtUtc,
            Intent = contract.Intent,
            AcceptanceExamples = contract.AcceptanceExamples,
            Checks = contract.Checks,
            Constraints = contract.Constraints,
            NonGoals = contract.NonGoals,
            AutoCompleteAllowed = contract.AutoCompleteAllowed,
            EvidenceRequired = contract.EvidenceRequired,
            HumanReview = contract.HumanReview,
            Traceability = new AcceptanceContractTraceability
            {
                SourceCardId = contract.Traceability.SourceCardId,
                SourceTaskId = contract.Traceability.SourceTaskId,
                DerivedTaskIds = contract.Traceability.DerivedTaskIds,
                RelatedArtifacts = mergedArtifacts,
            },
        };
    }
}
