using Carves.Runtime.Application.Configuration;

namespace Carves.Runtime.Application.Persistence;

public interface IControlPlaneConfigRepository
{
    SystemConfig LoadSystemConfig();

    AiProviderConfig LoadAiProviderConfig();

    PlannerAutonomyPolicy LoadPlannerAutonomyPolicy();

    CarvesCodeStandard LoadCarvesCodeStandard();

    WorkerOperationalPolicy LoadWorkerOperationalPolicy();

    SafetyRules LoadSafetyRules();

    ModuleDependencyMap LoadModuleDependencyMap();
}
