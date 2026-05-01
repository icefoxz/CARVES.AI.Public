using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Workers;

public sealed partial class WorkerSelectionPolicyService
{
    private readonly string repoRoot;
    private readonly RepoRegistryService repoRegistryService;
    private readonly ProviderRegistryService providerRegistryService;
    private readonly ProviderRoutingService providerRoutingService;
    private readonly PlatformGovernanceService governanceService;
    private readonly WorkerAdapterRegistry workerAdapterRegistry;
    private readonly WorkerExecutionBoundaryService boundaryService;
    private readonly ProviderHealthMonitorService providerHealthMonitorService;
    private readonly WorkerOperationalPolicyService operationalPolicyService;
    private readonly RuntimePolicyBundleService? runtimePolicyBundleService;
    private readonly RuntimeRoutingProfileService? runtimeRoutingProfileService;
    private readonly RuntimeRemoteWorkerQualificationService remoteWorkerQualificationService;

    public WorkerSelectionPolicyService(
        string repoRoot,
        RepoRegistryService repoRegistryService,
        ProviderRegistryService providerRegistryService,
        ProviderRoutingService providerRoutingService,
        PlatformGovernanceService governanceService,
        WorkerAdapterRegistry workerAdapterRegistry,
        WorkerExecutionBoundaryService boundaryService,
        ProviderHealthMonitorService providerHealthMonitorService,
        WorkerOperationalPolicyService operationalPolicyService,
        RuntimePolicyBundleService? runtimePolicyBundleService = null,
        RuntimeRoutingProfileService? runtimeRoutingProfileService = null,
        RuntimeRemoteWorkerQualificationService? remoteWorkerQualificationService = null)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.repoRegistryService = repoRegistryService;
        this.providerRegistryService = providerRegistryService;
        this.providerRoutingService = providerRoutingService;
        this.governanceService = governanceService;
        this.workerAdapterRegistry = workerAdapterRegistry;
        this.boundaryService = boundaryService;
        this.providerHealthMonitorService = providerHealthMonitorService;
        this.operationalPolicyService = operationalPolicyService;
        this.runtimePolicyBundleService = runtimePolicyBundleService;
        this.runtimeRoutingProfileService = runtimeRoutingProfileService;
        this.remoteWorkerQualificationService = remoteWorkerQualificationService ?? new RuntimeRemoteWorkerQualificationService();
    }

    public WorkerSelectionPolicyService(
        string repoRoot,
        RepoRegistryService repoRegistryService,
        ProviderRegistryService providerRegistryService,
        ProviderRoutingService providerRoutingService,
        PlatformGovernanceService governanceService,
        WorkerAdapterRegistry workerAdapterRegistry,
        WorkerExecutionBoundaryService boundaryService,
        ProviderHealthMonitorService providerHealthMonitorService)
        : this(
            repoRoot,
            repoRegistryService,
            providerRegistryService,
            providerRoutingService,
            governanceService,
            workerAdapterRegistry,
            boundaryService,
            providerHealthMonitorService,
            new WorkerOperationalPolicyService(WorkerOperationalPolicy.CreateDefault()))
    {
    }
}
