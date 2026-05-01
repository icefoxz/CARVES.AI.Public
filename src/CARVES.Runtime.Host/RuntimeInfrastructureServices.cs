using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.Processes;
using Carves.Runtime.Application.Workers;

namespace Carves.Runtime.Host;

internal sealed record RuntimeInfrastructureServices(
    IProcessRunner ProcessRunner,
    IGitClient GitClient,
    IAiClient AiClient,
    PlannerAdapterRegistry PlannerAdapterRegistry,
    WorkerAdapterRegistry WorkerAdapterRegistry,
    IRuntimeArtifactRepository ArtifactRepository,
    IWorkerExecutionAuditReadModel WorkerExecutionAuditReadModel);
