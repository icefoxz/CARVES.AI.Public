using System.Text.Json;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private CodeUnderstandingEnginePolicy LoadOrCreatePolicy()
    {
        if (File.Exists(paths.PlatformCodeUnderstandingEngineFile))
        {
            var persisted = JsonSerializer.Deserialize<CodeUnderstandingEnginePolicy>(
                                File.ReadAllText(paths.PlatformCodeUnderstandingEngineFile),
                                JsonOptions)
                            ?? BuildDefaultPolicy();
            if (NeedsRefresh(persisted))
            {
                var refreshed = BuildDefaultPolicy();
                File.WriteAllText(paths.PlatformCodeUnderstandingEngineFile, JsonSerializer.Serialize(refreshed, JsonOptions));
                return refreshed;
            }

            return persisted;
        }

        var policy = BuildDefaultPolicy();
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        File.WriteAllText(paths.PlatformCodeUnderstandingEngineFile, JsonSerializer.Serialize(policy, JsonOptions));
        return policy;
    }

    private string ToRepoRelative(string path)
    {
        return Path.GetRelativePath(repoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
    }
}
