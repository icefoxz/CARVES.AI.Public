using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    public RuntimeArtifactCatalog Build()
    {
        var worktreeRoot = Path.GetFullPath(Path.Combine(repoRoot, systemConfig.WorktreeRoot));
        return new RuntimeArtifactCatalog
        {
            SchemaVersion = CurrentSchemaVersion,
            Families =
            [
                .. BuildCoreTruthAndMirrorFamilies(),
                .. BuildCodeUnderstandingAndArtifactFamilies(),
                .. BuildOperationalHistoryFamilies(),
                .. BuildLiveStateAndArchiveFamilies(worktreeRoot),
            ],
        };
    }
}
