using System.Text.Json;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Platform.SurfaceModels;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackReviewRubricProjectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string repoRoot;
    private readonly IRuntimeArtifactRepository artifactRepository;

    public RuntimePackReviewRubricProjectionService(string repoRoot, IRuntimeArtifactRepository artifactRepository)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.artifactRepository = artifactRepository;
    }

    public RuntimePackReviewRubricProjection? TryBuildCurrentProjection()
    {
        var selection = artifactRepository.TryLoadCurrentRuntimePackSelectionArtifact();
        if (selection is null
            || !string.Equals(selection.AdmissionSource.AssignmentMode, "overlay_assignment", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(selection.AdmissionSource.AssignmentRef))
        {
            return null;
        }

        var manifestPath = ResolveManifestPath(selection.AdmissionSource.AssignmentRef!);
        if (manifestPath is null || !File.Exists(manifestPath))
        {
            return null;
        }

        RuntimePackV1ReviewRubricManifestDocument? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RuntimePackV1ReviewRubricManifestDocument>(
                File.ReadAllText(manifestPath),
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (manifest is null
            || !manifest.CapabilityKinds.Contains("review_rubric", StringComparer.Ordinal)
            || manifest.Recipes.ReviewRubrics.Length == 0)
        {
            return null;
        }

        var rubrics = manifest.Recipes.ReviewRubrics
            .Select(rubric => new RuntimePackReviewRubricProjectionEntry
            {
                RubricId = rubric.Id,
                Description = rubric.Description,
                ChecklistItems = rubric.ChecklistItems
                    .Select(item => new RuntimePackReviewRubricChecklistItem
                    {
                        ChecklistItemId = item.Id,
                        Severity = item.Severity,
                        Text = item.Text,
                    })
                    .ToArray(),
            })
            .ToArray();

        var checklistItemCount = rubrics.Sum(rubric => rubric.ChecklistItems.Count);
        return new RuntimePackReviewRubricProjection
        {
            PackId = selection.PackId,
            PackVersion = selection.PackVersion,
            Channel = selection.Channel,
            ManifestPath = ToRepoRelativeOrAbsolute(manifestPath),
            RubricCount = rubrics.Length,
            ChecklistItemCount = checklistItemCount,
            Rubrics = rubrics,
            Summary = $"Runtime Pack review rubric projection resolved {rubrics.Length} rubric(s) and {checklistItemCount} checklist item(s) from {selection.PackId}@{selection.PackVersion} ({selection.Channel}) without changing review verdict authority.",
            Notes =
            [
                "Review rubric projection is read-only and derived from the current selected declarative manifest.",
                "The projection may inform review preparation and explainability, but it does not approve, reject, reopen, or merge anything.",
                "Review gate authority remains Runtime-owned."
            ],
        };
    }

    private string? ResolveManifestPath(string assignmentRef)
    {
        if (string.IsNullOrWhiteSpace(assignmentRef))
        {
            return null;
        }

        var rooted = Path.IsPathRooted(assignmentRef)
            ? assignmentRef
            : Path.GetFullPath(Path.Combine(repoRoot, assignmentRef.Replace('/', Path.DirectorySeparatorChar)));
        return rooted;
    }

    private string ToRepoRelativeOrAbsolute(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(repoRoot, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal))
        {
            return fullPath.Replace('\\', '/');
        }

        return relative.Replace('\\', '/');
    }

    private sealed class RuntimePackV1ReviewRubricManifestDocument
    {
        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public string[] CapabilityKinds { get; init; } = [];

        public RuntimePackV1ReviewRubricRecipesDocument Recipes { get; init; } = new();
    }

    private sealed class RuntimePackV1ReviewRubricRecipesDocument
    {
        public RuntimePackV1ReviewRubricDocument[] ReviewRubrics { get; init; } = [];
    }

    private sealed class RuntimePackV1ReviewRubricDocument
    {
        public string Id { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public RuntimePackV1ReviewChecklistItemDocument[] ChecklistItems { get; init; } = [];
    }

    private sealed class RuntimePackV1ReviewChecklistItemDocument
    {
        public string Id { get; init; } = string.Empty;

        public string Severity { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;
    }
}
