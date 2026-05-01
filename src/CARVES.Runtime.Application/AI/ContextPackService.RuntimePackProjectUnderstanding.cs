using System.Text.Json;
using Carves.Runtime.Application.Guard;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed partial class ContextPackService
{
    private const int ProjectUnderstandingCandidateLimit = 24;
    private static readonly string[] ProjectUnderstandingPrunedDirectories =
    [
        ".git",
        ".ai",
        ".carves-platform",
        "node_modules",
        "bin",
        "obj",
    ];

    private RuntimePackProjectUnderstandingSelection BuildProjectUnderstandingSelection(IEnumerable<string> baseCandidateFiles)
    {
        var normalizedBaseCandidates = NormalizeCandidateFiles(baseCandidateFiles);
        var influence = TryLoadCurrentProjectUnderstandingInfluence();
        if (influence is null)
        {
            return new RuntimePackProjectUnderstandingSelection(
                normalizedBaseCandidates,
                normalizedBaseCandidates.Take(5).ToArray(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<ContextPackArtifactReference>(),
                HasRuntimePackInfluence: false);
        }

        var baseCandidateSet = normalizedBaseCandidates.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scoredPackCandidates = EnumerateRepoFilesForProjectUnderstanding()
            .Where(path => !baseCandidateSet.Contains(path))
            .Where(path => MatchesAny(path, influence.AllowedReadGlobs))
            .Where(path => MatchesAny(path, influence.IncludeGlobs) || MatchesAny(path, influence.RepoSignals))
            .Where(path => !MatchesAny(path, influence.ExcludeGlobs))
            .Select(path => new
            {
                Path = path,
                Score = ComputeProjectUnderstandingScore(path, anchoredToTaskScope: false, influence),
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(ProjectUnderstandingCandidateLimit)
            .Select(item => item.Path)
            .ToArray();

        var mergedCandidates = normalizedBaseCandidates
            .Concat(scoredPackCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(path => ComputeProjectUnderstandingScore(path, baseCandidateSet.Contains(path), influence))
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var constraints = new List<string>
        {
            $"runtime_pack:{influence.PackId}@{influence.PackVersion}({influence.Channel})",
        };

        constraints.AddRange(
            influence.Recipes
                .Select(recipe => $"project_understanding_recipe:{recipe.Id}")
                .Distinct(StringComparer.Ordinal));

        var codeHints = new List<string>
        {
            $"runtime pack current: {influence.PackId}@{influence.PackVersion} ({influence.Channel})",
        };

        if (influence.FrameworkHints.Count > 0)
        {
            codeHints.Add($"runtime pack framework hints: {string.Join(", ", influence.FrameworkHints)}");
        }

        if (influence.RepoSignals.Count > 0)
        {
            codeHints.Add($"runtime pack repo signals: {string.Join(", ", influence.RepoSignals.Take(5))}");
        }

        codeHints.Add($"runtime pack project understanding recipes: {string.Join(", ", influence.Recipes.Select(recipe => recipe.Id))}");

        var references = new[]
        {
            new ContextPackArtifactReference
            {
                Kind = "runtime_pack_manifest",
                Path = influence.ManifestPath,
                Summary = $"Selected declarative pack manifest for {influence.PackId}@{influence.PackVersion} remains on disk.",
            },
        };

        return new RuntimePackProjectUnderstandingSelection(
            mergedCandidates,
            mergedCandidates.Take(5).ToArray(),
            constraints,
            codeHints,
            references,
            HasRuntimePackInfluence: true);
    }

    private RuntimePackProjectUnderstandingInfluence? TryLoadCurrentProjectUnderstandingInfluence()
    {
        var selection = artifactRepository?.TryLoadCurrentRuntimePackSelectionArtifact();
        if (selection is null)
        {
            return null;
        }

        if (!string.Equals(selection.AdmissionSource.AssignmentMode, "overlay_assignment", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(selection.AdmissionSource.AssignmentRef))
        {
            return null;
        }

        var manifestPath = ResolveManifestPath(selection.AdmissionSource.AssignmentRef!);
        if (manifestPath is null || !File.Exists(manifestPath))
        {
            return null;
        }

        RuntimePackV1ProjectUnderstandingManifestDocument? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RuntimePackV1ProjectUnderstandingManifestDocument>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (manifest is null
            || manifest.Recipes.ProjectUnderstandingRecipes.Length == 0
            || !manifest.CapabilityKinds.Contains("project_understanding_recipe", StringComparer.Ordinal))
        {
            return null;
        }

        return new RuntimePackProjectUnderstandingInfluence(
            selection.PackId,
            selection.PackVersion,
            selection.Channel,
            ToRuntimeRelativePath(manifestPath),
            manifest.Compatibility.FrameworkHints
                .Concat(manifest.Recipes.ProjectUnderstandingRecipes.SelectMany(recipe => recipe.FrameworkHints))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            manifest.Compatibility.RepoSignals
                .Concat(manifest.Recipes.ProjectUnderstandingRecipes.SelectMany(recipe => recipe.RepoSignals))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            manifest.RequestedPermissions.ReadPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            manifest.Recipes.ProjectUnderstandingRecipes.Select(recipe => new RuntimePackProjectUnderstandingRecipe(
                recipe.Id,
                recipe.Description,
                recipe.RepoSignals.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                recipe.FrameworkHints.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                recipe.IncludeGlobs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                recipe.ExcludeGlobs.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                recipe.PriorityRules
                    .Select(rule => new RuntimePackProjectUnderstandingPriorityRule(rule.Id, rule.Glob, rule.Weight))
                    .ToArray()))
                .ToArray());
    }

    private string[] EnumerateRepoFilesForProjectUnderstanding()
    {
        var files = new List<string>();
        EnumerateRepoFiles(paths.RepoRoot, relativePrefix: string.Empty, files);
        return files.ToArray();
    }

    private void EnumerateRepoFiles(string directory, string relativePrefix, List<string> files)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            var relativePath = string.IsNullOrWhiteSpace(relativePrefix)
                ? name
                : $"{relativePrefix}/{name}";
            files.Add(relativePath.Replace('\\', '/'));
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(directory))
        {
            var name = Path.GetFileName(subDirectory);
            if (ProjectUnderstandingPrunedDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var nextPrefix = string.IsNullOrWhiteSpace(relativePrefix)
                ? name
                : $"{relativePrefix}/{name}";
            EnumerateRepoFiles(subDirectory, nextPrefix, files);
        }
    }

    private int ComputeProjectUnderstandingScore(
        string path,
        bool anchoredToTaskScope,
        RuntimePackProjectUnderstandingInfluence influence)
    {
        var score = anchoredToTaskScope ? 1000 : 0;
        if (MatchesAny(path, influence.RepoSignals))
        {
            score += 200;
        }

        if (MatchesAny(path, influence.IncludeGlobs))
        {
            score += 20;
        }

        foreach (var priorityRule in influence.Recipes.SelectMany(recipe => recipe.PriorityRules))
        {
            if (GuardDiffAdapter.GlobMatches(path, priorityRule.Glob, caseSensitive: false))
            {
                score += priorityRule.Weight;
            }
        }

        return score;
    }

    private static bool MatchesAny(string path, IReadOnlyCollection<string> patterns)
    {
        if (patterns.Count == 0)
        {
            return false;
        }

        return patterns.Any(pattern => GuardDiffAdapter.GlobMatches(path, pattern, caseSensitive: false));
    }

    private static string[] NormalizeCandidateFiles(IEnumerable<string> candidateFiles)
    {
        return candidateFiles
            .Select(path => path.Replace('\\', '/').Trim())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string? ResolveManifestPath(string assignmentRef)
    {
        var normalized = assignmentRef.Replace('/', Path.DirectorySeparatorChar);
        var rooted = Path.IsPathRooted(normalized)
            ? Path.GetFullPath(normalized)
            : Path.GetFullPath(Path.Combine(paths.RepoRoot, normalized));
        if (!rooted.StartsWith(paths.RepoRoot, StringComparison.Ordinal))
        {
            return null;
        }

        return rooted;
    }

    private sealed record RuntimePackProjectUnderstandingSelection(
        IReadOnlyList<string> CandidateFiles,
        IReadOnlyList<string> ScopeFiles,
        IReadOnlyList<string> Constraints,
        IReadOnlyList<string> CodeHints,
        IReadOnlyList<ContextPackArtifactReference> ExpandableReferences,
        bool HasRuntimePackInfluence);

    private sealed record RuntimePackProjectUnderstandingInfluence(
        string PackId,
        string PackVersion,
        string Channel,
        string ManifestPath,
        IReadOnlyList<string> FrameworkHints,
        IReadOnlyList<string> RepoSignals,
        IReadOnlyList<string> AllowedReadGlobs,
        IReadOnlyList<RuntimePackProjectUnderstandingRecipe> Recipes)
    {
        public IReadOnlyList<string> IncludeGlobs =>
            Recipes.SelectMany(recipe => recipe.IncludeGlobs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        public IReadOnlyList<string> ExcludeGlobs =>
            Recipes.SelectMany(recipe => recipe.ExcludeGlobs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    private sealed record RuntimePackProjectUnderstandingRecipe(
        string Id,
        string Description,
        IReadOnlyList<string> RepoSignals,
        IReadOnlyList<string> FrameworkHints,
        IReadOnlyList<string> IncludeGlobs,
        IReadOnlyList<string> ExcludeGlobs,
        IReadOnlyList<RuntimePackProjectUnderstandingPriorityRule> PriorityRules);

    private sealed record RuntimePackProjectUnderstandingPriorityRule(string Id, string Glob, int Weight);

    private sealed class RuntimePackV1ProjectUnderstandingManifestDocument
    {
        public string PackId { get; init; } = string.Empty;

        public string PackVersion { get; init; } = string.Empty;

        public string[] CapabilityKinds { get; init; } = [];

        public RuntimePackV1ProjectUnderstandingCompatibilityDocument Compatibility { get; init; } = new();

        public RuntimePackV1ProjectUnderstandingRequestedPermissionsDocument RequestedPermissions { get; init; } = new();

        public RuntimePackV1ProjectUnderstandingRecipesDocument Recipes { get; init; } = new();
    }

    private sealed class RuntimePackV1ProjectUnderstandingCompatibilityDocument
    {
        public string[] FrameworkHints { get; init; } = [];

        public string[] RepoSignals { get; init; } = [];
    }

    private sealed class RuntimePackV1ProjectUnderstandingRequestedPermissionsDocument
    {
        public string[] ReadPaths { get; init; } = [];
    }

    private sealed class RuntimePackV1ProjectUnderstandingRecipesDocument
    {
        public RuntimePackV1ProjectUnderstandingRecipeDocument[] ProjectUnderstandingRecipes { get; init; } = [];
    }

    private sealed class RuntimePackV1ProjectUnderstandingRecipeDocument
    {
        public string Id { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public string[] RepoSignals { get; init; } = [];

        public string[] FrameworkHints { get; init; } = [];

        public string[] IncludeGlobs { get; init; } = [];

        public string[] ExcludeGlobs { get; init; } = [];

        public RuntimePackV1ProjectUnderstandingPriorityRuleDocument[] PriorityRules { get; init; } = [];
    }

    private sealed class RuntimePackV1ProjectUnderstandingPriorityRuleDocument
    {
        public string Id { get; init; } = string.Empty;

        public string Glob { get; init; } = string.Empty;

        public int Weight { get; init; }
    }
}
