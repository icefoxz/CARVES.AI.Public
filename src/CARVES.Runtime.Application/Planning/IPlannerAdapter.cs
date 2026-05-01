using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public interface IPlannerAdapter
{
    string AdapterId { get; }

    string ProviderId { get; }

    string? ProfileId { get; }

    bool IsConfigured { get; }

    bool IsRealAdapter { get; }

    string SelectionReason { get; }

    PlannerProposalEnvelope Run(PlannerRunRequest request);
}
