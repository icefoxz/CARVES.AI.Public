using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeTargetDistBindingPlanService
{
    public const string PhaseDocumentPath = "docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md";
    public const string PreviousPhaseDocumentPath = RuntimeCliActivationPlanService.PhaseDocumentPath;
    public const string GuideDocumentPath = "docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md";
    private const string DistVersion = RuntimeAlphaVersion.Current;

    private readonly string repoRoot;
    private readonly RuntimeDocumentRootResolution documentRoot;

    public RuntimeTargetDistBindingPlanService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        documentRoot = RuntimeDocumentRootResolver.Resolve(this.repoRoot, ControlPlanePaths.FromRepoRoot(this.repoRoot));
    }

    public RuntimeTargetDistBindingPlanSurface Build()
    {
        var errors = new List<string>();
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.PhaseDocumentPath, "Product closure Phase 22 local dist freshness smoke document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistFreshnessSmokeService.GuideDocumentPath, "Local dist freshness smoke guide document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.PhaseDocumentPath, "Product closure Phase 23 frozen dist target readback proof document", errors);
        ValidateRuntimeDocument(RuntimeFrozenDistTargetReadbackProofService.GuideDocumentPath, "Frozen dist target readback proof guide document", errors);
        ValidateRuntimeDocument(PhaseDocumentPath, "Product closure Phase 21 target dist binding plan document", errors);
        ValidateRuntimeDocument(PreviousPhaseDocumentPath, "Product closure Phase 20 CLI activation plan document", errors);
        ValidateRuntimeDocument(GuideDocumentPath, "Target dist binding plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliActivationPlanService.ActivationGuideDocumentPath, "CLI activation plan guide document", errors);
        ValidateRuntimeDocument(RuntimeCliInvocationContractService.InvocationGuideDocumentPath, "CLI invocation contract guide document", errors);
        ValidateRuntimeDocument(RuntimeLocalDistHandoffService.LocalDistGuideDocumentPath, "Runtime local dist guide document", errors);

        var localDistHandoff = new RuntimeLocalDistHandoffService(repoRoot).Build();
        errors.AddRange(localDistHandoff.Errors.Select(static error => $"runtime-local-dist-handoff: {error}"));

        var runtimeRoot = documentRoot.DocumentRoot;
        var runtimeRootKind = localDistHandoff.RuntimeRootKind;
        var candidateDistRoot = ResolveCandidateDistRoot(runtimeRoot);
        var candidateManifest = ReadCandidateManifest(candidateDistRoot, errors);
        var candidateDistExists = Directory.Exists(candidateDistRoot);
        var candidateDistHasManifest = File.Exists(Path.Combine(candidateDistRoot, "MANIFEST.json"));
        var candidateDistHasVersion = File.Exists(Path.Combine(candidateDistRoot, "VERSION"));
        var candidateDistHasWrapper = RuntimeCliWrapperPaths.HasPreferredWrapper(candidateDistRoot);
        var targetRuntimeInitialized = File.Exists(Path.Combine(repoRoot, ".ai", "runtime.json"));
        var attachRuntimeRoot = ReadRuntimeRootFromAttachHandshake();
        var manifestRuntimeRoot = ReadRuntimeRootFromRuntimeManifest();
        var currentRootMatchesCandidate = PathEquals(runtimeRoot, candidateDistRoot);
        var planComplete = errors.Count == 0 && candidateDistExists && candidateDistHasManifest && candidateDistHasVersion && candidateDistHasWrapper;

        var boundToLocalDist = localDistHandoff.StableExternalConsumptionReady;
        var boundToLiveSource = string.Equals(localDistHandoff.OverallPosture, "local_dist_handoff_live_source_attached", StringComparison.Ordinal);

        return new RuntimeTargetDistBindingPlanSurface
        {
            PhaseDocumentPath = PhaseDocumentPath,
            PreviousPhaseDocumentPath = PreviousPhaseDocumentPath,
            GuideDocumentPath = GuideDocumentPath,
            RuntimeDocumentRoot = runtimeRoot,
            RuntimeDocumentRootMode = documentRoot.Mode,
            RepoRoot = repoRoot,
            OverallPosture = ResolvePosture(errors.Count, planComplete, boundToLocalDist, boundToLiveSource, targetRuntimeInitialized),
            DistBindingPlanComplete = planComplete,
            RecommendedBindingMode = ResolveRecommendedBindingMode(boundToLocalDist, boundToLiveSource, targetRuntimeInitialized, planComplete),
            RuntimeRootKind = runtimeRootKind,
            TargetRuntimeInitialized = targetRuntimeInitialized,
            TargetBoundToLocalDist = boundToLocalDist,
            TargetBoundToLiveSource = boundToLiveSource,
            CandidateDistRoot = candidateDistRoot,
            CandidateDistExists = candidateDistExists,
            CandidateDistHasManifest = candidateDistHasManifest,
            CandidateDistHasVersion = candidateDistHasVersion,
            CandidateDistHasWrapper = candidateDistHasWrapper,
            CandidateDistVersion = candidateManifest.Version,
            CandidateDistSourceCommit = candidateManifest.SourceCommit,
            CurrentRuntimeRootMatchesCandidateDist = currentRootMatchesCandidate,
            AttachHandshakeRuntimeRoot = attachRuntimeRoot,
            RuntimeManifestRuntimeRoot = manifestRuntimeRoot,
            OperatorBindingCommands = BuildOperatorBindingCommands(candidateDistRoot),
            RequiredReadbackCommands =
            [
                "carves pilot dist-smoke --json",
                "carves pilot dist-binding --json",
                "carves pilot dist --json",
                "carves pilot target-proof --json",
                "carves pilot invocation --json",
                "carves pilot activation --json",
                "carves pilot proof --json",
            ],
            BoundaryRules =
            [
                "Target dist binding is operator-owned; agents should not edit .ai/runtime.json or attach-handshake files manually.",
                "Retargeting must use the frozen dist wrapper and normal CARVES init/attach path, not direct file patching.",
                "The target repo should be clean before any operator retarget action.",
                "A live source Runtime root is acceptable for Runtime development but not the stable external-project alpha baseline.",
                "Dist binding does not grant planning, workspace, review, writeback, staging, commit, push, pack, or release authority.",
            ],
            Gaps = BuildGaps(errors, planComplete, targetRuntimeInitialized, boundToLocalDist, boundToLiveSource, candidateDistExists, candidateDistHasManifest, candidateDistHasVersion, candidateDistHasWrapper).ToArray(),
            Summary = BuildSummary(errors.Count, planComplete, boundToLocalDist, boundToLiveSource, targetRuntimeInitialized),
            RecommendedNextAction = BuildRecommendedNextAction(errors.Count, planComplete, boundToLocalDist, boundToLiveSource, targetRuntimeInitialized),
            IsValid = errors.Count == 0,
            Errors = errors,
            NonClaims =
            [
                "This surface does not create, refresh, copy, delete, or publish a Runtime dist.",
                "This surface does not rewrite target runtime bindings, attach handshakes, manifests, PATH, shell profiles, aliases, or tool installs.",
                "This surface does not initialize, plan, review, write back, stage, commit, push, tag, release, pack, or retarget anything by itself.",
                "This surface does not claim public package distribution, signed installers, automatic updates, OS sandboxing, full ACP, full MCP, or remote worker orchestration.",
            ],
        };
    }

    private static string ResolvePosture(
        int errorCount,
        bool planComplete,
        bool boundToLocalDist,
        bool boundToLiveSource,
        bool targetRuntimeInitialized)
    {
        if (errorCount > 0 || !planComplete)
        {
            return "target_dist_binding_plan_blocked_by_missing_dist_resources";
        }

        if (boundToLocalDist)
        {
            return "target_dist_binding_plan_satisfied";
        }

        if (!targetRuntimeInitialized)
        {
            return "target_dist_binding_plan_ready_for_initial_attach";
        }

        if (boundToLiveSource)
        {
            return "target_dist_binding_plan_ready_for_operator_retarget";
        }

        return "target_dist_binding_plan_ready";
    }

    private static string ResolveRecommendedBindingMode(
        bool boundToLocalDist,
        bool boundToLiveSource,
        bool targetRuntimeInitialized,
        bool planComplete)
    {
        if (!planComplete)
        {
            return "restore_or_refresh_local_dist";
        }

        if (boundToLocalDist)
        {
            return "keep_current_local_dist_binding";
        }

        if (!targetRuntimeInitialized)
        {
            return "initial_attach_via_local_dist";
        }

        return boundToLiveSource
            ? "operator_retarget_from_live_source_to_local_dist"
            : "verify_or_reattach_via_local_dist";
    }

    private static IEnumerable<string> BuildGaps(
        IReadOnlyList<string> errors,
        bool planComplete,
        bool targetRuntimeInitialized,
        bool boundToLocalDist,
        bool boundToLiveSource,
        bool candidateDistExists,
        bool candidateDistHasManifest,
        bool candidateDistHasVersion,
        bool candidateDistHasWrapper)
    {
        foreach (var error in errors)
        {
            yield return $"missing_or_invalid_dist_binding_resource:{error}";
        }

        if (!candidateDistExists)
        {
            yield return "candidate_dist_root_missing";
        }

        if (!candidateDistHasManifest)
        {
            yield return "candidate_dist_manifest_missing";
        }

        if (!candidateDistHasVersion)
        {
            yield return "candidate_dist_version_missing";
        }

        if (!candidateDistHasWrapper)
        {
            yield return "candidate_dist_wrapper_missing";
        }

        if (!targetRuntimeInitialized)
        {
            yield return "target_runtime_not_initialized";
        }

        if (planComplete && boundToLiveSource)
        {
            yield return "target_bound_to_live_source_tree";
        }

        if (planComplete && !boundToLocalDist)
        {
            yield return "target_not_bound_to_frozen_local_dist";
        }
    }

    private static string BuildSummary(
        int errorCount,
        bool planComplete,
        bool boundToLocalDist,
        bool boundToLiveSource,
        bool targetRuntimeInitialized)
    {
        if (errorCount > 0 || !planComplete)
        {
            return "Target dist binding plan is blocked until the local Runtime dist and binding guide resources are present.";
        }

        if (boundToLocalDist)
        {
            return "The current target is already bound to a frozen local Runtime dist root.";
        }

        if (!targetRuntimeInitialized)
        {
            return "The target is not initialized yet; attach it through the frozen local dist wrapper.";
        }

        if (boundToLiveSource)
        {
            return "The current target is attached to the live Runtime source tree; the plan reports the operator-owned retarget path to the frozen local dist.";
        }

        return "The local dist candidate is ready; verify or reattach the target through that dist wrapper before claiming stable external consumption.";
    }

    private static string BuildRecommendedNextAction(
        int errorCount,
        bool planComplete,
        bool boundToLocalDist,
        bool boundToLiveSource,
        bool targetRuntimeInitialized)
    {
        if (errorCount > 0 || !planComplete)
        {
            return "Refresh the local Runtime dist, then rerun carves pilot dist-smoke --json and carves pilot dist-binding --json.";
        }

        if (boundToLocalDist)
        {
            return "Run carves pilot dist --json, then carves pilot proof --json.";
        }

        if (!targetRuntimeInitialized)
        {
            return "From the target repo, run the candidate dist wrapper init command shown in operator_binding_commands, then rerun carves pilot dist-binding --json.";
        }

        return boundToLiveSource
            ? "Operator decision required: from the target repo, run the candidate dist wrapper init command shown in operator_binding_commands, then rerun carves pilot dist --json."
            : "Run carves pilot dist --json and verify whether the target root should be reattached through the candidate local dist.";
    }

    private static IReadOnlyList<string> BuildOperatorBindingCommands(string candidateDistRoot)
    {
        var wrapper = RuntimeCliWrapperPaths.PreferredWrapperPath(candidateDistRoot);
        return
        [
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "init", ".", "--json"),
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "invocation", "--json"),
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "activation", "--json"),
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "dist-smoke", "--json"),
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "dist-binding", "--json"),
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "target-proof", "--json"),
            RuntimeCliWrapperPaths.FormatShellCommand(wrapper, "pilot", "dist", "--json"),
        ];
    }

    private string ResolveCandidateDistRoot(string runtimeRoot)
    {
        if (File.Exists(Path.Combine(runtimeRoot, "MANIFEST.json"))
            && File.Exists(Path.Combine(runtimeRoot, "VERSION")))
        {
            return runtimeRoot;
        }

        var sourceRoot = runtimeRoot;
        var siblingRoot = Directory.GetParent(sourceRoot)?.FullName ?? repoRoot;
        return Path.GetFullPath(Path.Combine(siblingRoot, ".dist", $"CARVES.Runtime-{DistVersion}"));
    }

    private RuntimeDistCandidateManifest ReadCandidateManifest(string candidateDistRoot, List<string> errors)
    {
        var manifestPath = Path.Combine(candidateDistRoot, "MANIFEST.json");
        if (!File.Exists(manifestPath))
        {
            return RuntimeDistCandidateManifest.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            return new RuntimeDistCandidateManifest(ReadString(root, "version"), ReadString(root, "source_commit"));
        }
        catch (JsonException exception)
        {
            errors.Add($"Unable to parse local Runtime dist manifest '{manifestPath}': {exception.Message}");
            return RuntimeDistCandidateManifest.Empty;
        }
        catch (IOException exception)
        {
            errors.Add($"Unable to read local Runtime dist manifest '{manifestPath}': {exception.Message}");
            return RuntimeDistCandidateManifest.Empty;
        }
    }

    private string ReadRuntimeRootFromAttachHandshake()
    {
        return ReadNestedString(Path.Combine(repoRoot, ".ai", "runtime", "attach-handshake.json"), "request", "runtime_root");
    }

    private string ReadRuntimeRootFromRuntimeManifest()
    {
        return ReadString(Path.Combine(repoRoot, ".ai", "runtime.json"), "runtime_root");
    }

    private static string ReadNestedString(string path, string parentProperty, string propertyName)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return root.TryGetProperty(parentProperty, out var parent)
                   && parent.ValueKind == JsonValueKind.Object
                   && parent.TryGetProperty(propertyName, out var property)
                   && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadString(string path, string propertyName)
    {
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return ReadString(document.RootElement, propertyName);
        }
        catch
        {
            return string.Empty;
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
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private sealed record RuntimeDistCandidateManifest(string Version, string SourceCommit)
    {
        public static RuntimeDistCandidateManifest Empty { get; } = new(string.Empty, string.Empty);
    }
}
