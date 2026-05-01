using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public interface IOpportunityRepository
{
    OpportunitySnapshot Load();

    void Save(OpportunitySnapshot snapshot);
}
