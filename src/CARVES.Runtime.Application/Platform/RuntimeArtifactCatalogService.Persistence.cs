using Carves.Runtime.Application.ControlPlane;
using System.Text.Json;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    public RuntimeArtifactCatalog LoadOrBuild(bool persist = true)
    {
        var existing = TryLoad();
        if (existing is not null && !NeedsRefresh(existing))
        {
            return existing;
        }

        var catalog = Build();
        if (persist)
        {
            Save(catalog);
        }

        return catalog;
    }

    public RuntimeArtifactCatalog? TryLoad()
    {
        var path = GetCatalogPath(paths);
        if (!File.Exists(path))
        {
            return null;
        }

        return JsonSerializer.Deserialize<RuntimeArtifactCatalog>(File.ReadAllText(path), JsonOptions);
    }

    public RuntimeArtifactCatalog Save(RuntimeArtifactCatalog catalog)
    {
        var path = GetCatalogPath(paths);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(catalog, JsonOptions));
        return catalog;
    }

    public static string GetCatalogPath(ControlPlanePaths paths)
    {
        return Path.Combine(GetSustainabilityRoot(paths), "artifact-catalog.json");
    }

    public static string GetSustainabilityRoot(ControlPlanePaths paths)
    {
        return Path.Combine(paths.RuntimeRoot, "sustainability");
    }
}
