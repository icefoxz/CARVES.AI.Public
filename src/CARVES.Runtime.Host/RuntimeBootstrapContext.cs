using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Persistence;

namespace Carves.Runtime.Host;

internal sealed record RuntimeBootstrapContext(
    ControlPlanePaths Paths,
    IControlPlaneConfigRepository ConfigRepository,
    SystemConfig SystemConfig,
    AiProviderConfig AiProviderConfig,
    PlannerAutonomyPolicy PlannerAutonomyPolicy,
    WorkerOperationalPolicy WorkerOperationalPolicy,
    SafetyRules SafetyRules,
    ModuleDependencyMap ModuleDependencyMap);
