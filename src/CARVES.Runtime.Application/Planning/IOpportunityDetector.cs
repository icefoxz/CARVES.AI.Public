using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public interface IOpportunityDetector
{
    string Name { get; }

    IReadOnlyList<OpportunityObservation> Detect();
}
