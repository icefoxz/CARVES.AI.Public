using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Persistence;

namespace Carves.Runtime.Infrastructure.Persistence;

public sealed class FileControlPlaneConfigRepository : IControlPlaneConfigRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ControlPlanePaths paths;

    public FileControlPlaneConfigRepository(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public SystemConfig LoadSystemConfig()
    {
        var dto = LoadJson<SystemConfigDto>(paths.SystemConfigFile);
        return ControlPlaneConfigMapper.ToSystemConfig(dto, Path.GetFileName(paths.RepoRoot));
    }

    public AiProviderConfig LoadAiProviderConfig()
    {
        var dto = LoadJson<AiProviderConfigDto>(paths.AiProviderConfigFile);
        return ControlPlaneConfigMapper.ToAiProviderConfig(dto);
    }

    public PlannerAutonomyPolicy LoadPlannerAutonomyPolicy()
    {
        var dto = LoadJson<PlannerAutonomyPolicyDto>(paths.PlannerAutonomyConfigFile);
        return ControlPlaneConfigMapper.ToPlannerAutonomyPolicy(dto);
    }

    public CarvesCodeStandard LoadCarvesCodeStandard()
    {
        var dto = LoadJson<CarvesCodeStandardDto>(paths.CarvesCodeStandardFile);
        return ControlPlaneConfigMapper.ToCarvesCodeStandard(dto);
    }

    public WorkerOperationalPolicy LoadWorkerOperationalPolicy()
    {
        var dto = LoadJson<WorkerOperationalPolicyDto>(paths.WorkerOperationalPolicyFile);
        return ControlPlaneConfigMapper.ToWorkerOperationalPolicy(dto);
    }

    public SafetyRules LoadSafetyRules()
    {
        var dto = LoadJson<SafetyRulesDto>(paths.SafetyRulesFile);
        return ControlPlaneConfigMapper.ToSafetyRules(dto);
    }

    public ModuleDependencyMap LoadModuleDependencyMap()
    {
        var raw = LoadJson<Dictionary<string, string[]?>>(paths.ModuleDependenciesFile);
        return ControlPlaneConfigMapper.ToModuleDependencyMap(raw);
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
