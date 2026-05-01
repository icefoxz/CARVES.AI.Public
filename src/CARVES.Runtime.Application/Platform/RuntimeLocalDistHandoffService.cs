using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeLocalDistHandoffService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md";
    public const string LocalDistGuideDocumentPath = "docs/guides/CARVES_RUNTIME_LOCAL_DIST.md";
    public const string CliDistributionGuideDocumentPath = "docs/guides/CARVES_CLI_DISTRIBUTION.md";

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeLocalDistHandoffService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeLocalDistHandoffSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 16 local dist handoff document", errors);
        ValidateRuntimeDocument(RuntimeTargetCommitClosureService.PhaseDocumentPath, "Product closure Phase 15 target commit closure document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(LocalDistGuideDocumentPath, "Runtime local dist guide document", errors);
        ValidateRuntimeDocument(CliDistributionGuideDocumentPath, "CLI distribution guide document", errors);
        ValidateRuntimeDocument(RuntimeTargetDistBindingPlanService.GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);

        var runtimeRoot = documentRoot.DocumentRoot;
        var manifestPath = Path.Combine(runtimeRoot, "MANIFEST.json");
        var versionPath = Path.Combine(runtimeRoot, "VERSION");
        var gitDirectoryPath = Path.Combine(runtimeRoot, ".git");
        var solutionPath = Path.Combine(runtimeRoot, "CARVES.Runtime.sln");

        var hasManifest = File.Exists(manifestPath);
        var hasVersion = File.Exists(versionPath);
        var hasWrapper = RuntimeCliWrapperPaths.HasPreferredWrapper(runtimeRoot);
        var hasGitDirectory = Directory.Exists(gitDirectoryPath) || File.Exists(gitDirectoryPath);
        var hasSolution = File.Exists(solutionPath);
        var versionFileValue = hasVersion ? File.ReadAllText(versionPath).Trim() : string.Empty;
        var manifest = hasManifest ? ReadManifest(manifestPath, errors) : RuntimeLocalDistManifest.Empty;
        var rootKind = ResolveRuntimeRootKind(hasManifest, hasVersion, hasWrapper, hasGitDirectory, hasSolution);
        var rootMatchesRepoRoot = PathEquals(runtimeRoot, repoRoot);
        var stableExternalConsumptionReady = string.Equals(rootKind, "local_dist", StringComparison.Ordinal)
                                             && !rootMatchesRepoRoot
                                             && string.Equals(documentRoot.Mode, "attach_handshake_runtime_root", StringComparison.Ordinal);
        var externalTargetBoundToRuntimeRoot = !rootMatchesRepoRoot
                                               && (string.Equals(documentRoot.Mode, "attach_handshake_runtime_root", StringComparison.Ordinal)
                                                   || string.Equals(documentRoot.Mode, "runtime_manifest_root", StringComparison.Ordinal));
        var gaps = BuildGaps(rootKind, stableExternalConsumptionReady, externalTargetBoundToRuntimeRoot, rootMatchesRepoRoot, hasManifest, hasVersion, hasWrapper).ToArray();

        return new RuntimeLocalDistHandoffSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            LocalDistGuideDocumentPath = LocalDistGuideDocumentPath,
            CliDistributionGuideDocumentPath = CliDistributionGuideDocumentPath,
            RuntimeDocumentRoot = runtimeRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(errors.Count, rootKind, stableExternalConsumptionReady, externalTargetBoundToRuntimeRoot, rootMatchesRepoRoot),
            RuntimeRootKind = rootKind,
            StableExternalConsumptionReady = stableExternalConsumptionReady,
            ExternalTargetBoundToRuntimeRoot = externalTargetBoundToRuntimeRoot,
            RuntimeRootMatchesRepoRoot = rootMatchesRepoRoot,
            RuntimeRootHasManifest = hasManifest,
            RuntimeRootHasVersion = hasVersion,
            RuntimeRootHasWrapper = hasWrapper,
            RuntimeRootHasGitDirectory = hasGitDirectory,
            RuntimeRootHasSolution = hasSolution,
            ManifestSchemaVersion = manifest.SchemaVersion,
            ManifestVersion = manifest.Version,
            ManifestSourceCommit = manifest.SourceCommit,
            ManifestOutputPath = manifest.OutputPath,
            VersionFileValue = versionFileValue,
            RequiredSmokeCommands =
            [
                "carves pilot invocation --json",
                "carves pilot activation --json",
                "carves pilot dist-smoke --json",
                "carves pilot dist-binding --json",
                "carves pilot dist --json",
                "carves pilot target-proof --json",
                "carves pilot status --json",
                "carves agent handoff --json",
            ],
            Gaps = gaps,
            Summary = BuildSummary(rootKind, stableExternalConsumptionReady, externalTargetBoundToRuntimeRoot, rootMatchesRepoRoot, errors.Count),
            RecommendedNextAction = BuildRecommendedNextAction(rootKind, stableExternalConsumptionReady, externalTargetBoundToRuntimeRoot, rootMatchesRepoRoot, errors.Count),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create, pack, copy, delete, or replace a Runtime distribution folder.",
                "This surface does not initialize, repair, or retarget the current repo's Runtime binding.",
                "This surface does not claim public package distribution, signed releases, update channels, ACP, MCP, or remote orchestration.",
                "This surface only proves whether the current repo is reading Runtime doctrine from a local frozen dist-shaped root.",
            ],
        };
    }

    private static IEnumerable<string> BuildGaps(
        string rootKind,
        bool stableExternalConsumptionReady,
        bool externalTargetBoundToRuntimeRoot,
        bool rootMatchesRepoRoot,
        bool hasManifest,
        bool hasVersion,
        bool hasWrapper)
    {
        if (stableExternalConsumptionReady)
        {
            yield break;
        }

        if (!externalTargetBoundToRuntimeRoot)
        {
            yield return "external_target_runtime_root_binding_missing";
        }

        if (rootMatchesRepoRoot)
        {
            yield return "runtime_root_matches_current_repo";
        }

        if (!hasManifest)
        {
            yield return "runtime_dist_manifest_missing";
        }

        if (!hasVersion)
        {
            yield return "runtime_dist_version_missing";
        }

        if (!hasWrapper)
        {
            yield return "runtime_dist_wrapper_missing";
        }

        if (string.Equals(rootKind, "source_tree", StringComparison.Ordinal))
        {
            yield return "runtime_root_is_live_source_tree";
        }
    }

    private static string ResolveRuntimeRootKind(bool hasManifest, bool hasVersion, bool hasWrapper, bool hasGitDirectory, bool hasSolution)
    {
        if (hasManifest && hasVersion && hasWrapper && !hasGitDirectory)
        {
            return "local_dist";
        }

        if (hasGitDirectory || hasSolution)
        {
            return "source_tree";
        }

        return "unknown_runtime_root";
    }

    private static string ResolvePosture(
        int errorCount,
        string rootKind,
        bool stableExternalConsumptionReady,
        bool externalTargetBoundToRuntimeRoot,
        bool rootMatchesRepoRoot)
    {
        if (errorCount > 0)
        {
            return "local_dist_handoff_blocked_by_surface_gaps";
        }

        if (stableExternalConsumptionReady)
        {
            return "local_dist_handoff_ready";
        }

        if (string.Equals(rootKind, "local_dist", StringComparison.Ordinal) && rootMatchesRepoRoot)
        {
            return "local_dist_handoff_dist_self_readback";
        }

        if (string.Equals(rootKind, "source_tree", StringComparison.Ordinal) && externalTargetBoundToRuntimeRoot)
        {
            return "local_dist_handoff_live_source_attached";
        }

        if (string.Equals(rootKind, "source_tree", StringComparison.Ordinal))
        {
            return "local_dist_handoff_source_tree_development_mode";
        }

        return "local_dist_handoff_blocked_by_dist_contract_gap";
    }

    private static string BuildSummary(
        string rootKind,
        bool stableExternalConsumptionReady,
        bool externalTargetBoundToRuntimeRoot,
        bool rootMatchesRepoRoot,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Local dist handoff cannot be trusted until required phase and distribution documents are restored.";
        }

        if (stableExternalConsumptionReady)
        {
            return "The current target repo is bound to a frozen local Runtime dist root for stable external consumption.";
        }

        if (string.Equals(rootKind, "source_tree", StringComparison.Ordinal) && externalTargetBoundToRuntimeRoot)
        {
            return "The current target repo is attached to a live Runtime source tree; this is acceptable for development but not the stable external-project consumption posture.";
        }

        if (string.Equals(rootKind, "local_dist", StringComparison.Ordinal) && rootMatchesRepoRoot)
        {
            return "The Runtime root is a local dist, but this command is running from the dist root itself rather than an attached target repo.";
        }

        if (string.Equals(rootKind, "source_tree", StringComparison.Ordinal))
        {
            return "This repo is running in Runtime source-tree development mode, not as an external target bound to a frozen dist.";
        }

        return "The Runtime document root does not yet satisfy the local dist handoff shape.";
    }

    private static string BuildRecommendedNextAction(
        string rootKind,
        bool stableExternalConsumptionReady,
        bool externalTargetBoundToRuntimeRoot,
        bool rootMatchesRepoRoot,
        int errorCount)
    {
        if (errorCount > 0)
        {
            return "Restore the Phase 16 local dist handoff docs and distribution guides, then rerun carves pilot dist --json.";
        }

        if (stableExternalConsumptionReady)
        {
            return "Run carves pilot status --json and continue with the governed pilot stage it selects.";
        }

        if (string.Equals(rootKind, "source_tree", StringComparison.Ordinal) && externalTargetBoundToRuntimeRoot)
        {
            return "Run carves pilot dist-smoke --json and carves pilot dist-binding --json, then follow the operator-owned dist wrapper binding command before rerunning carves pilot dist --json.";
        }

        if (string.Equals(rootKind, "local_dist", StringComparison.Ordinal) && rootMatchesRepoRoot)
        {
            return "Run this command from an attached external target repo to prove target-to-dist handoff.";
        }

        return rootMatchesRepoRoot
            ? "Use a target repo outside the Runtime root, then attach it to a frozen local Runtime dist."
            : "Attach this target repo to a frozen local Runtime dist, then rerun carves pilot dist --json.";
    }

    private RuntimeLocalDistManifest ReadManifest(string manifestPath, List<string> errors)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            return new RuntimeLocalDistManifest(
                ReadString(root, "schema_version"),
                ReadString(root, "version"),
                ReadString(root, "source_commit"),
                ReadString(root, "output_path"));
        }
        catch (JsonException exception)
        {
            errors.Add($"Unable to parse Runtime dist manifest '{manifestPath}': {exception.Message}");
            return RuntimeLocalDistManifest.Empty;
        }
        catch (IOException exception)
        {
            errors.Add($"Unable to read Runtime dist manifest '{manifestPath}': {exception.Message}");
            return RuntimeLocalDistManifest.Empty;
        }
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private void ValidateRuntimeDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(documentRoot.DocumentRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }

    private static bool PathEquals(string first, string second)
    {
        return string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private sealed record RuntimeLocalDistManifest(string SchemaVersion, string Version, string SourceCommit, string OutputPath)
    {
        public static RuntimeLocalDistManifest Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
