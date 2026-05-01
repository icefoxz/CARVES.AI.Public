using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Domain.Execution;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimePackDeclarativeContributionSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string repoRoot;

    public RuntimePackDeclarativeContributionSnapshotService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimePackDeclarativeContribution? TryBuild(string? assignmentMode, string? assignmentRef)
    {
        if (!string.Equals(assignmentMode, "overlay_assignment", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(assignmentRef))
        {
            return null;
        }

        var manifestPath = ResolveManifestPath(assignmentRef);
        if (manifestPath is null || !File.Exists(manifestPath))
        {
            return null;
        }

        RuntimePackV1ContributionManifestDocument? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RuntimePackV1ContributionManifestDocument>(
                File.ReadAllText(manifestPath),
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (manifest is null)
        {
            return null;
        }

        var capabilityKinds = DistinctOrdered(manifest.CapabilityKinds);
        var projectUnderstandingRecipeIds = DistinctOrdered(manifest.Recipes.ProjectUnderstandingRecipes.Select(item => item.Id));
        var verificationRecipeIds = DistinctOrdered(manifest.Recipes.VerificationRecipes.Select(item => item.Id));
        var verificationCommandIds = DistinctOrdered(
            manifest.Recipes.VerificationRecipes.SelectMany(item => item.Commands.Select(command => command.Id)));
        var reviewRubricIds = DistinctOrdered(manifest.Recipes.ReviewRubrics.Select(item => item.Id));
        var reviewChecklistItemIds = DistinctOrdered(
            manifest.Recipes.ReviewRubrics.SelectMany(item => item.ChecklistItems.Select(check => check.Id)));

        var fingerprint = ComputeFingerprint(
            capabilityKinds,
            projectUnderstandingRecipeIds,
            verificationRecipeIds,
            verificationCommandIds,
            reviewRubricIds,
            reviewChecklistItemIds);

        return new RuntimePackDeclarativeContribution
        {
            ManifestPath = ToRepoRelativeOrAbsolute(manifestPath),
            ContributionFingerprint = fingerprint,
            CapabilityKinds = capabilityKinds,
            ProjectUnderstandingRecipeIds = projectUnderstandingRecipeIds,
            VerificationRecipeIds = verificationRecipeIds,
            VerificationCommandIds = verificationCommandIds,
            ReviewRubricIds = reviewRubricIds,
            ReviewChecklistItemIds = reviewChecklistItemIds,
            Summary = $"Declarative contribution kinds={JoinOrNone(capabilityKinds)}; project_understanding={projectUnderstandingRecipeIds.Count}; verification_recipes={verificationRecipeIds.Count}; verification_commands={verificationCommandIds.Count}; review_rubrics={reviewRubricIds.Count}; review_checks={reviewChecklistItemIds.Count}.",
        };
    }

    private string? ResolveManifestPath(string assignmentRef)
    {
        if (string.IsNullOrWhiteSpace(assignmentRef))
        {
            return null;
        }

        return Path.IsPathRooted(assignmentRef)
            ? assignmentRef
            : Path.GetFullPath(Path.Combine(repoRoot, assignmentRef.Replace('/', Path.DirectorySeparatorChar)));
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

    private static IReadOnlyList<string> DistinctOrdered(IEnumerable<string?> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string JoinOrNone(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "none" : string.Join(", ", values);
    }

    private static string ComputeFingerprint(params IReadOnlyList<string>[] segments)
    {
        var payload = string.Join(
            "\n",
            segments.Select((segment, index) => $"segment:{index}:{string.Join("|", segment)}"));
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(digest);
    }

    private sealed class RuntimePackV1ContributionManifestDocument
    {
        public string[] CapabilityKinds { get; init; } = [];

        public RuntimePackV1ContributionRecipesDocument Recipes { get; init; } = new();
    }

    private sealed class RuntimePackV1ContributionRecipesDocument
    {
        public RuntimePackV1ProjectUnderstandingRecipeDocument[] ProjectUnderstandingRecipes { get; init; } = [];

        public RuntimePackV1VerificationRecipeDocument[] VerificationRecipes { get; init; } = [];

        public RuntimePackV1ReviewRubricDocument[] ReviewRubrics { get; init; } = [];
    }

    private sealed class RuntimePackV1ProjectUnderstandingRecipeDocument
    {
        public string Id { get; init; } = string.Empty;
    }

    private sealed class RuntimePackV1VerificationRecipeDocument
    {
        public string Id { get; init; } = string.Empty;

        public RuntimePackV1VerificationCommandDocument[] Commands { get; init; } = [];
    }

    private sealed class RuntimePackV1VerificationCommandDocument
    {
        public string Id { get; init; } = string.Empty;
    }

    private sealed class RuntimePackV1ReviewRubricDocument
    {
        public string Id { get; init; } = string.Empty;

        public RuntimePackV1ReviewChecklistItemDocument[] ChecklistItems { get; init; } = [];
    }

    private sealed class RuntimePackV1ReviewChecklistItemDocument
    {
        public string Id { get; init; } = string.Empty;
    }
}
