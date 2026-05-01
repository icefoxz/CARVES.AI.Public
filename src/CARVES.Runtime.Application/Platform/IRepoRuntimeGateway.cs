using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.Platform;

public interface IRepoRuntimeGateway
{
    RepoRuntimeGatewayMode GatewayMode { get; }

    RepoRuntimeGatewayHealth GetHealth(RepoDescriptor descriptor);

    RepoRuntimeCommandResult Execute(RepoRuntimeCommandRequest request);

    RepoRuntimeSummary LoadSummary(RepoDescriptor descriptor);

    RuntimeSessionState Start(RepoDescriptor descriptor, bool dryRun);

    RuntimeSessionState Resume(RepoDescriptor descriptor, string reason);

    RuntimeSessionState Pause(RepoDescriptor descriptor, string reason);

    RuntimeSessionState Stop(RepoDescriptor descriptor, string reason);

    RuntimeSessionState? LoadSession(RepoDescriptor descriptor);

    OpportunitySnapshot LoadOpportunities(RepoDescriptor descriptor);

    DomainTaskGraph LoadTaskGraph(RepoDescriptor descriptor);
}
