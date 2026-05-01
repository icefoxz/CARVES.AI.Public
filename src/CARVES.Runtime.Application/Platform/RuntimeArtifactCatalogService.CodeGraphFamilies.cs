using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeArtifactCatalogService
{
    private RuntimeArtifactFamilyPolicy[] BuildCodeGraphArtifactFamilies()
    {
        return
        [
            new RuntimeArtifactFamilyPolicy
            {
                FamilyId = "codegraph_derived",
                DisplayName = "CodeGraph summary-derived truth",
                ArtifactClass = RuntimeArtifactClass.DerivedTruth,
                RetentionMode = RuntimeArtifactRetentionMode.SingleVersion,
                DefaultReadVisibility = RuntimeArtifactReadVisibility.Summary,
                RebuildEligible = true,
                Roots =
                [
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "manifest.json")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "index.json")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "search")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "modules")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "dependencies")),
                    ToRepoRelative(Path.Combine(paths.AiRoot, "codegraph", "summaries")),
                ],
                AllowedContents = ["manifest", "summary index", "search index", "module summaries", "dependency shards"],
                ForbiddenContents = ["full graph detail", "bin/", "obj/", "test results", "raw build logs"],
                Budget = new RuntimeArtifactBudgetPolicy { MaxOnlineFiles = 2000, MaxOnlineBytes = 12 * 1024 * 1024 },
                Summary = "CodeGraph summary outputs are rebuildable derived truth and must stay summary-first.",
            },
        ];
    }
}
