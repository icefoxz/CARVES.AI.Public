using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeExportProfileService
{
    private static readonly string[] RequiredProfileIds =
    [
        "source_review",
        "proof_bundle",
        "runtime_state_package",
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly SystemConfig systemConfig;

    public RuntimeExportProfileService(string repoRoot, ControlPlanePaths paths, SystemConfig systemConfig)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
        this.paths = paths;
        this.systemConfig = systemConfig;
        EnsurePersistedDefaults();
    }

    public static string GetPolicyPath(ControlPlanePaths paths)
    {
        return Path.Combine(paths.PlatformPoliciesRoot, "export-profiles.policy.json");
    }

    public RuntimeExportProfilePolicy LoadPolicy()
    {
        return LoadInternal().Policy;
    }

    public RuntimePolicyValidationResult Validate()
    {
        var result = LoadInternal();
        return new RuntimePolicyValidationResult(
            result.Errors.Count == 0,
            result.Errors,
            result.Warnings);
    }

    public RuntimeExportProfilesSurface BuildSurface(string? profileId = null)
    {
        var result = LoadInternal();
        var familyIndex = result.ArtifactCatalog.Families.ToDictionary(family => family.FamilyId, StringComparer.Ordinal);
        var profiles = result.Policy.Profiles;

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            profiles = profiles
                .Where(profile => string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
                .ToArray();
            if (profiles.Count == 0)
            {
                throw new InvalidOperationException($"Runtime export profile '{profileId}' was not found.");
            }
        }

        var resolvedProfiles = profiles
            .Select(profile => ResolveProfile(
                profile,
                familyIndex,
                result.ProfileErrors.TryGetValue(profile.ProfileId, out var profileErrors) ? profileErrors : [],
                result.ProfileWarnings.TryGetValue(profile.ProfileId, out var profileWarnings) ? profileWarnings : []))
            .ToArray();

        return new RuntimeExportProfilesSurface
        {
            PolicyFile = GetPolicyPath(paths),
            ArtifactCatalogSchemaVersion = result.ArtifactCatalog.SchemaVersion,
            Summary = $"Runtime export profiles freeze source_review, proof_bundle, and runtime_state_package packaging rules through explicit repo-local truth.",
            IsValid = result.Errors.Count == 0,
            Errors = result.Errors,
            Warnings = result.Warnings,
            Profiles = resolvedProfiles,
        };
    }

    private void EnsurePersistedDefaults()
    {
        Directory.CreateDirectory(paths.PlatformPoliciesRoot);
        var policyPath = GetPolicyPath(paths);
        if (!File.Exists(policyPath))
        {
            File.WriteAllText(policyPath, JsonSerializer.Serialize(BuildDefaultPolicy(), JsonOptions));
        }
    }

    private RuntimeExportProfileLoadResult LoadInternal()
    {
        EnsurePersistedDefaults();
        var errors = new List<string>();
        var warnings = new List<string>();
        var profileErrors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var profileWarnings = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var defaults = BuildDefaultPolicy();
        var policy = LoadFile(GetPolicyPath(paths), defaults, errors);
        var artifactCatalog = new RuntimeArtifactCatalogService(repoRoot, paths, systemConfig).LoadOrBuild(persist: false);
        ValidatePolicy(policy, artifactCatalog, errors, warnings, profileErrors, profileWarnings);
        return new RuntimeExportProfileLoadResult(policy, artifactCatalog, errors, warnings, profileErrors, profileWarnings);
    }

    private static RuntimeExportProfilePolicy BuildDefaultPolicy()
    {
        return new RuntimeExportProfilePolicy
        {
            Profiles =
            [
                new RuntimeExportProfilePolicyProfile
                {
                    ProfileId = "source_review",
                    DisplayName = "Source review pack",
                    Summary = "Source review packages carry repo-local code/docs plus compact truth context, while excluding bulky runtime-local history and live state by default.",
                    FamilyRules =
                    [
                        new RuntimeExportProfileFamilyRule { FamilyId = "task_truth", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Card and task lineage belongs in review context." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "governed_markdown_mirror", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Current governed mirrors keep review context human-readable." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "platform_definition_truth", PackagingMode = RuntimeExportPackagingMode.ManifestOnly, Reason = "Policy/config context should remain explicit without exporting unrelated local churn." },
                    ],
                    IncludedPathRoots = ["README.md", "AGENTS.md", "src/", "tests/", "docs/"],
                    ExcludedFamilyIds =
                    [
                        "execution_memory_truth",
                        "runtime_pack_selection_audit_evidence",
                        "runtime_pack_policy_audit_evidence",
                        "planning_runtime_history",
                        "execution_surface_history",
                        "validation_trace_history",
                        "execution_run_detail_history",
                        "execution_run_report_history",
                        "runtime_failure_detail_history",
                        "worker_execution_artifact_history",
                        "runtime_live_state",
                        "platform_live_state",
                        "platform_provider_live_state",
                        "ephemeral_runtime_residue",
                    ],
                    Notes =
                    [
                        "Source review stays code/doc-first and does not silently absorb live runtime clutter.",
                    ],
                },
                new RuntimeExportProfilePolicyProfile
                {
                    ProfileId = "proof_bundle",
                    DisplayName = "Proof bundle",
                    Summary = "Proof bundles carry the bounded task/evidence spine, while keeping bulky runtime-local audit families pointer-first instead of assuming full payload export.",
                    FamilyRules =
                    [
                        new RuntimeExportProfileFamilyRule { FamilyId = "task_truth", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Proof bundles need the canonical task/card lineage." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "execution_memory_truth", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Execution outcome memory remains governed truth for review and proof." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "governed_markdown_mirror", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Summary mirrors help bounded human review." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "validation_suite_truth", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Validation task definitions stay part of proof context." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "worker_execution_artifact_history", PackagingMode = RuntimeExportPackagingMode.PointerOnly, Reason = "Worker/provider/review evidence can be referenced without bloating every export." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "runtime_failure_detail_history", PackagingMode = RuntimeExportPackagingMode.PointerOnly, Reason = "Failure detail stays pointer-first unless a later bounded lane needs the raw payload." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "runtime_pack_policy_audit_evidence", PackagingMode = RuntimeExportPackagingMode.PointerOnly, Reason = "Pack policy audit history remains compact evidence, not default full payload." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "runtime_pack_selection_audit_evidence", PackagingMode = RuntimeExportPackagingMode.PointerOnly, Reason = "Selection audit history stays pointer-first by default." },
                    ],
                    IncludedPathRoots = ["docs/runtime/", "docs/contracts/"],
                    ExcludedFamilyIds =
                    [
                        "runtime_live_state",
                        "platform_live_state",
                        "platform_provider_live_state",
                        "ephemeral_runtime_residue",
                    ],
                    Notes =
                    [
                        "Pointer-only handling is the bounded default for high-growth operational-history families.",
                    ],
                },
                new RuntimeExportProfilePolicyProfile
                {
                    ProfileId = "runtime_state_package",
                    DisplayName = "Runtime-state package",
                    Summary = "Runtime-state packages carry current state and routing truth, while rebuildable projections and high-growth runtime-local families stay manifest-first or pointer-first.",
                    FamilyRules =
                    [
                        new RuntimeExportProfileFamilyRule { FamilyId = "routing_truth", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Routing and qualification truth belongs in runtime-state exchange." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "platform_definition_truth", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Current policy/definition truth anchors runtime-state interpretation." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "runtime_live_state", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Current runtime live state is the primary payload." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "platform_live_state", PackagingMode = RuntimeExportPackagingMode.Full, Reason = "Platform live state belongs in current runtime-state exchange." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "platform_provider_live_state", PackagingMode = RuntimeExportPackagingMode.ManifestOnly, Reason = "Provider live state remains summary-first by default." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "governed_markdown_mirror", PackagingMode = RuntimeExportPackagingMode.ManifestOnly, Reason = "Markdown mirrors stay explanatory rather than payload-dominant." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "context_pack_projection", PackagingMode = RuntimeExportPackagingMode.PointerOnly, Reason = "Context packs are rebuildable projections and should not silently bulk up runtime-state packages." },
                        new RuntimeExportProfileFamilyRule { FamilyId = "execution_packet_mirror", PackagingMode = RuntimeExportPackagingMode.PointerOnly, Reason = "Execution packets are governed mirrors and stay pointer-first unless explicitly requested." },
                    ],
                    ExcludedFamilyIds =
                    [
                        "task_truth",
                        "execution_memory_truth",
                        "worker_execution_artifact_history",
                        "runtime_failure_detail_history",
                        "planning_runtime_history",
                        "execution_surface_history",
                        "validation_trace_history",
                        "execution_run_detail_history",
                        "execution_run_report_history",
                        "ephemeral_runtime_residue",
                    ],
                    Notes =
                    [
                        "Runtime-state exchange remains current-state-first, not a bulk archive of every local history family.",
                    ],
                },
            ],
        };
    }

    private static void ValidatePolicy(
        RuntimeExportProfilePolicy policy,
        RuntimeArtifactCatalog artifactCatalog,
        List<string> errors,
        List<string> warnings,
        Dictionary<string, List<string>> profileErrors,
        Dictionary<string, List<string>> profileWarnings)
    {
        if (policy.Profiles.Count == 0)
        {
            errors.Add("Export profile policy requires at least one profile.");
            return;
        }

        var familyIds = artifactCatalog.Families
            .Select(family => family.FamilyId)
            .ToHashSet(StringComparer.Ordinal);
        var familyIndex = artifactCatalog.Families
            .ToDictionary(family => family.FamilyId, StringComparer.Ordinal);
        foreach (var duplicate in policy.Profiles
                     .GroupBy(profile => profile.ProfileId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            errors.Add($"Export profile policy contains duplicate profile id '{duplicate}'.");
        }

        var profileIds = policy.Profiles
            .Select(profile => profile.ProfileId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var requiredProfileId in RequiredProfileIds.Where(requiredProfileId => !profileIds.Contains(requiredProfileId)))
        {
            errors.Add($"Export profile policy is missing required profile '{requiredProfileId}'.");
        }

        foreach (var profile in policy.Profiles)
        {
            var currentProfileErrors = GetBucket(profileErrors, profile.ProfileId);
            var currentProfileWarnings = GetBucket(profileWarnings, profile.ProfileId);

            if (string.IsNullOrWhiteSpace(profile.ProfileId))
            {
                errors.Add("Export profile policy contains a profile with an empty profile_id.");
                continue;
            }

            if (profile.FamilyRules.Count == 0 && profile.IncludedPathRoots.Count == 0)
            {
                errors.Add($"Export profile '{profile.ProfileId}' must include at least one family rule or included path root.");
            }

            foreach (var duplicate in profile.FamilyRules
                         .GroupBy(rule => rule.FamilyId, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                errors.Add($"Export profile '{profile.ProfileId}' contains duplicate family rule '{duplicate}'.");
            }

            foreach (var familyRule in profile.FamilyRules)
            {
                if (string.IsNullOrWhiteSpace(familyRule.FamilyId))
                {
                    errors.Add($"Export profile '{profile.ProfileId}' contains an empty family rule id.");
                    continue;
                }

                if (!familyIds.Contains(familyRule.FamilyId))
                {
                    errors.Add($"Export profile '{profile.ProfileId}' references unknown artifact family '{familyRule.FamilyId}'.");
                }

                if (string.IsNullOrWhiteSpace(familyRule.Reason))
                {
                    errors.Add($"Export profile '{profile.ProfileId}' contains family rule '{familyRule.FamilyId}' without a reason.");
                }
            }

            foreach (var excludedFamilyId in profile.ExcludedFamilyIds)
            {
                if (!familyIds.Contains(excludedFamilyId))
                {
                    errors.Add($"Export profile '{profile.ProfileId}' excludes unknown artifact family '{excludedFamilyId}'.");
                }
            }

            foreach (var overlapping in profile.FamilyRules.Select(rule => rule.FamilyId).Intersect(profile.ExcludedFamilyIds, StringComparer.Ordinal))
            {
                warnings.Add($"Export profile '{profile.ProfileId}' both includes and excludes family '{overlapping}'.");
            }

            if (profile.IncludedPathRoots.Any(path => string.IsNullOrWhiteSpace(path))
                || profile.ExcludedPathRoots.Any(path => string.IsNullOrWhiteSpace(path)))
            {
                errors.Add($"Export profile '{profile.ProfileId}' contains an empty included or excluded path root.");
            }

            ValidatePathRoots(profile.IncludedPathRoots, currentProfileErrors);
            ValidatePathRoots(profile.ExcludedPathRoots, currentProfileErrors);

            var normalizedIncludedRoots = profile.IncludedPathRoots.Select(NormalizePathRoot).ToArray();
            var normalizedExcludedRoots = profile.ExcludedPathRoots.Select(NormalizePathRoot).ToArray();
            foreach (var duplicate in normalizedIncludedRoots.GroupBy(path => path, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                currentProfileErrors.Add($"duplicate included path root '{duplicate}'");
            }

            foreach (var duplicate in normalizedExcludedRoots.GroupBy(path => path, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key))
            {
                currentProfileErrors.Add($"duplicate excluded path root '{duplicate}'");
            }

            foreach (var overlap in normalizedIncludedRoots.Intersect(normalizedExcludedRoots, StringComparer.Ordinal))
            {
                currentProfileErrors.Add($"path root '{overlap}' cannot be both included and excluded");
            }

            ValidateRequiredDiscipline(profile, familyIndex, currentProfileErrors);
            ValidatePackagingDiscipline(profile, familyIndex, currentProfileErrors, currentProfileWarnings);

            errors.AddRange(currentProfileErrors.Select(error => $"Export profile '{profile.ProfileId}' {error}."));
            warnings.AddRange(currentProfileWarnings.Select(warning => $"Export profile '{profile.ProfileId}' {warning}."));
        }
    }

    private RuntimeExportProfileSurfaceProfile ResolveProfile(
        RuntimeExportProfilePolicyProfile profile,
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyIndex,
        IReadOnlyList<string> disciplineErrors,
        IReadOnlyList<string> disciplineWarnings)
    {
        var includedFamilies = profile.FamilyRules
            .Where(rule => familyIndex.ContainsKey(rule.FamilyId))
            .Select(rule =>
            {
                var family = familyIndex[rule.FamilyId];
                return new RuntimeExportProfileResolvedFamily
                {
                    FamilyId = family.FamilyId,
                    DisplayName = family.DisplayName,
                    ArtifactClass = family.ArtifactClass,
                    PackagingMode = rule.PackagingMode,
                    Roots = family.Roots,
                    Summary = family.Summary,
                    Reason = rule.Reason,
                };
            })
            .ToArray();

        var fullFamilyIds = includedFamilies
            .Where(family => family.PackagingMode == RuntimeExportPackagingMode.Full)
            .Select(family => family.FamilyId)
            .ToArray();
        var manifestOnlyFamilyIds = includedFamilies
            .Where(family => family.PackagingMode == RuntimeExportPackagingMode.ManifestOnly)
            .Select(family => family.FamilyId)
            .ToArray();
        var pointerOnlyFamilyIds = includedFamilies
            .Where(family => family.PackagingMode == RuntimeExportPackagingMode.PointerOnly)
            .Select(family => family.FamilyId)
            .ToArray();

        return new RuntimeExportProfileSurfaceProfile
        {
            ProfileId = profile.ProfileId,
            DisplayName = profile.DisplayName,
            Summary = profile.Summary,
            IncludedFamilies = includedFamilies,
            ExcludedFamilies = profile.ExcludedFamilyIds
                .Where(familyIndex.ContainsKey)
                .Select(familyId =>
                {
                    var family = familyIndex[familyId];
                    return new RuntimeExportProfileExcludedFamily
                    {
                        FamilyId = family.FamilyId,
                        DisplayName = family.DisplayName,
                    };
                })
                .ToArray(),
            IncludedPathRoots = profile.IncludedPathRoots.Select(NormalizePathRoot).ToArray(),
            ExcludedPathRoots = profile.ExcludedPathRoots.Select(NormalizePathRoot).ToArray(),
            Notes = profile.Notes,
            Discipline = new RuntimeExportProfileDisciplineSurface
            {
                IsValid = disciplineErrors.Count == 0,
                FullFamilyCount = fullFamilyIds.Length,
                ManifestOnlyFamilyCount = manifestOnlyFamilyIds.Length,
                PointerOnlyFamilyCount = pointerOnlyFamilyIds.Length,
                FullFamilyIds = fullFamilyIds,
                ManifestOnlyFamilyIds = manifestOnlyFamilyIds,
                PointerOnlyFamilyIds = pointerOnlyFamilyIds,
                Errors = disciplineErrors,
                Warnings = disciplineWarnings,
            },
        };
    }

    private static void ValidatePathRoots(IReadOnlyList<string> pathRoots, List<string> errors)
    {
        foreach (var pathRoot in pathRoots.Select(NormalizePathRoot))
        {
            if (Path.IsPathRooted(pathRoot) || pathRoot.StartsWith('/'))
            {
                errors.Add($"path root '{pathRoot}' must stay repo-relative");
            }

            if (pathRoot.Contains("../", StringComparison.Ordinal) || pathRoot.Contains("..\\", StringComparison.Ordinal))
            {
                errors.Add($"path root '{pathRoot}' must not traverse outside the repo root");
            }
        }
    }

    private static void ValidateRequiredDiscipline(
        RuntimeExportProfilePolicyProfile profile,
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyIndex,
        List<string> errors)
    {
        var normalizedIncludedRoots = profile.IncludedPathRoots.Select(NormalizePathRoot).ToHashSet(StringComparer.Ordinal);
        var familyModes = profile.FamilyRules.ToDictionary(rule => rule.FamilyId, rule => rule.PackagingMode, StringComparer.Ordinal);
        var excludedFamilies = profile.ExcludedFamilyIds.ToHashSet(StringComparer.Ordinal);

        switch (profile.ProfileId)
        {
            case "source_review":
                RequireMode(familyModes, "task_truth", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "governed_markdown_mirror", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "platform_definition_truth", RuntimeExportPackagingMode.ManifestOnly, errors);
                RequirePaths(normalizedIncludedRoots, ["README.md", "AGENTS.md", "src/", "tests/", "docs/"], errors);
                RequireExcludedFamilies(excludedFamilies,
                [
                    "execution_memory_truth",
                    "planning_runtime_history",
                    "execution_surface_history",
                    "validation_trace_history",
                    "execution_run_detail_history",
                    "execution_run_report_history",
                    "runtime_failure_detail_history",
                    "worker_execution_artifact_history",
                    "runtime_live_state",
                    "platform_live_state",
                    "platform_provider_live_state",
                    "ephemeral_runtime_residue",
                ], errors);
                break;

            case "proof_bundle":
                RequireMode(familyModes, "task_truth", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "execution_memory_truth", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "governed_markdown_mirror", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "validation_suite_truth", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "worker_execution_artifact_history", RuntimeExportPackagingMode.PointerOnly, errors);
                RequireMode(familyModes, "runtime_failure_detail_history", RuntimeExportPackagingMode.PointerOnly, errors);
                RequireMode(familyModes, "runtime_pack_policy_audit_evidence", RuntimeExportPackagingMode.PointerOnly, errors);
                RequireMode(familyModes, "runtime_pack_selection_audit_evidence", RuntimeExportPackagingMode.PointerOnly, errors);
                RequirePaths(normalizedIncludedRoots, ["docs/runtime/", "docs/contracts/"], errors);
                RequireExcludedFamilies(excludedFamilies,
                [
                    "runtime_live_state",
                    "platform_live_state",
                    "platform_provider_live_state",
                    "ephemeral_runtime_residue",
                ], errors);
                break;

            case "runtime_state_package":
                RequireMode(familyModes, "routing_truth", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "platform_definition_truth", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "runtime_live_state", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "platform_live_state", RuntimeExportPackagingMode.Full, errors);
                RequireMode(familyModes, "platform_provider_live_state", RuntimeExportPackagingMode.ManifestOnly, errors);
                RequireMode(familyModes, "governed_markdown_mirror", RuntimeExportPackagingMode.ManifestOnly, errors);
                RequireMode(familyModes, "context_pack_projection", RuntimeExportPackagingMode.PointerOnly, errors);
                RequireMode(familyModes, "execution_packet_mirror", RuntimeExportPackagingMode.PointerOnly, errors);
                RequireExcludedFamilies(excludedFamilies,
                [
                    "task_truth",
                    "execution_memory_truth",
                    "worker_execution_artifact_history",
                    "runtime_failure_detail_history",
                    "planning_runtime_history",
                    "execution_surface_history",
                    "validation_trace_history",
                    "execution_run_detail_history",
                    "execution_run_report_history",
                    "ephemeral_runtime_residue",
                ], errors);
                break;
        }
    }

    private static void ValidatePackagingDiscipline(
        RuntimeExportProfilePolicyProfile profile,
        IReadOnlyDictionary<string, RuntimeArtifactFamilyPolicy> familyIndex,
        List<string> errors,
        List<string> warnings)
    {
        foreach (var familyRule in profile.FamilyRules.Where(rule => familyIndex.ContainsKey(rule.FamilyId)))
        {
            var family = familyIndex[familyRule.FamilyId];

            switch (profile.ProfileId)
            {
                case "source_review":
                    if (familyRule.PackagingMode == RuntimeExportPackagingMode.Full
                        && family.ArtifactClass is RuntimeArtifactClass.LiveState or RuntimeArtifactClass.OperationalHistory or RuntimeArtifactClass.EphemeralResidue or RuntimeArtifactClass.AuditArchive)
                    {
                        errors.Add($"must not full-package {familyRule.FamilyId} ({ToToken(family.ArtifactClass)}) in source review");
                    }

                    break;

                case "proof_bundle":
                    if (familyRule.PackagingMode == RuntimeExportPackagingMode.Full
                        && family.ArtifactClass is RuntimeArtifactClass.LiveState or RuntimeArtifactClass.OperationalHistory or RuntimeArtifactClass.EphemeralResidue or RuntimeArtifactClass.AuditArchive)
                    {
                        errors.Add($"must not full-package {familyRule.FamilyId} ({ToToken(family.ArtifactClass)}) in proof bundle");
                    }

                    if (family.ArtifactClass == RuntimeArtifactClass.OperationalHistory
                        && familyRule.PackagingMode == RuntimeExportPackagingMode.ManifestOnly)
                    {
                        warnings.Add($"keeps operational-history family '{familyRule.FamilyId}' manifest_only; pointer_only remains the default bulky-history posture");
                    }

                    break;

                case "runtime_state_package":
                    if (familyRule.FamilyId is "task_truth" or "execution_memory_truth")
                    {
                        errors.Add($"must not include {familyRule.FamilyId} in runtime_state_package");
                    }

                    if (family.ArtifactClass == RuntimeArtifactClass.EphemeralResidue)
                    {
                        errors.Add($"must not include cleanup-only family '{familyRule.FamilyId}' in runtime_state_package");
                    }

                    break;
            }
        }
    }

    private static void RequireMode(
        IReadOnlyDictionary<string, RuntimeExportPackagingMode> familyModes,
        string familyId,
        RuntimeExportPackagingMode requiredMode,
        List<string> errors)
    {
        if (!familyModes.TryGetValue(familyId, out var actualMode))
        {
            errors.Add($"is missing required family rule '{familyId}'");
            return;
        }

        if (actualMode != requiredMode)
        {
            errors.Add($"must keep family '{familyId}' on {ToToken(requiredMode)} packaging (found {ToToken(actualMode)})");
        }
    }

    private static void RequirePaths(
        IReadOnlySet<string> normalizedIncludedRoots,
        IReadOnlyList<string> requiredRoots,
        List<string> errors)
    {
        foreach (var requiredRoot in requiredRoots)
        {
            var normalizedRoot = NormalizePathRoot(requiredRoot);
            if (!normalizedIncludedRoots.Contains(normalizedRoot))
            {
                errors.Add($"is missing required included path root '{normalizedRoot}'");
            }
        }
    }

    private static void RequireExcludedFamilies(
        IReadOnlySet<string> excludedFamilies,
        IReadOnlyList<string> requiredFamilyIds,
        List<string> errors)
    {
        foreach (var requiredFamilyId in requiredFamilyIds)
        {
            if (!excludedFamilies.Contains(requiredFamilyId))
            {
                errors.Add($"is missing required excluded family '{requiredFamilyId}'");
            }
        }
    }

    private static T LoadFile<T>(string path, T defaults, List<string> errors)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? defaults;
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to load {Path.GetFileName(path)}: {ex.Message}");
            return defaults;
        }
    }

    private static string NormalizePathRoot(string pathRoot)
    {
        return pathRoot.Trim().Replace('\\', '/');
    }

    private static string ToToken(Enum value)
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }

    private static List<string> GetBucket(Dictionary<string, List<string>> buckets, string profileId)
    {
        if (!buckets.TryGetValue(profileId, out var bucket))
        {
            bucket = [];
            buckets[profileId] = bucket;
        }

        return bucket;
    }

    private sealed record RuntimeExportProfileLoadResult(
        RuntimeExportProfilePolicy Policy,
        RuntimeArtifactCatalog ArtifactCatalog,
        List<string> Errors,
        List<string> Warnings,
        Dictionary<string, List<string>> ProfileErrors,
        Dictionary<string, List<string>> ProfileWarnings);
}
