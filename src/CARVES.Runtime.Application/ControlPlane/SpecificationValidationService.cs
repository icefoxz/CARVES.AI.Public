using System.Text.Json;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Persistence;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Tasks;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.ControlPlane;

public sealed class SpecificationValidationService
{
    private static readonly HashSet<string> CanonicalTaskStatuses = Enum
        .GetNames<DomainTaskStatus>()
        .Select(ToSnakeCase)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> CanonicalTaskTypes = Enum
        .GetNames<TaskType>()
        .Select(ToSnakeCase)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> CanonicalPlannerVerdicts = Enum
        .GetNames<PlannerVerdict>()
        .Select(ToSnakeCase)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> CanonicalPackTypes =
    [
        "runtime_pack",
        "vertical_runtime_pack",
        "enterprise_profile_pack",
        "offline_bundle_pack",
    ];

    private static readonly HashSet<string> CanonicalAttributionModes =
    [
        "local_pin",
        "overlay_assignment",
        "channel_resolution",
        "offline_bundle",
    ];

    private static readonly HashSet<string> CanonicalPackV1CapabilityKinds =
    [
        "project_understanding_recipe",
        "verification_recipe",
        "review_rubric",
    ];

    private static readonly HashSet<string> CanonicalPackV1PublisherTrustLevels =
    [
        "first_party",
        "verified",
        "community",
        "internal",
    ];

    private static readonly HashSet<string> CanonicalPackV1VerificationCommandKinds =
    [
        "known_tool_command",
        "package_manager_script",
        "repo_script",
        "shell_command",
    ];

    private static readonly HashSet<string> CanonicalPackV1ChecklistSeverities =
    [
        "info",
        "review",
        "warn",
        "block",
    ];

    private static readonly string[] PlannerWritablePrefixes =
    [
        ".ai/tasks/",
        ".ai/reviews/",
        ".ai/refactoring/",
        ".ai/opportunities/",
        ".ai/runtime/planning/",
        ".ai/runtime/sustainability/",
    ];

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly IControlPlaneConfigRepository configRepository;

    public SpecificationValidationService(string repoRoot, ControlPlanePaths paths, IControlPlaneConfigRepository configRepository)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.configRepository = configRepository;
    }

    public SpecificationValidationResult ValidateCard(string cardPath, bool strict)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(cardPath);
        if (!File.Exists(resolvedPath))
        {
            issues.Add(Error("card_missing", "Card file does not exist.", resolvedPath));
            return Finalize("card", resolvedPath, issues, strict);
        }

        if (!string.Equals(Path.GetExtension(resolvedPath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("card_extension", "Card should be stored as a Markdown file.", resolvedPath));
        }

        var lines = File.ReadAllLines(resolvedPath);
        if (!lines.Any(line => line.StartsWith("# ", StringComparison.Ordinal)))
        {
            issues.Add(Error("card_heading_missing", "Card is missing the '# CARD-...' heading.", resolvedPath));
        }

        if (!lines.Any(line => line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Error("card_title_missing", "Card is missing a Title: line.", resolvedPath));
        }

        if (!ContainsNonEmptySection(lines, "Goal"))
        {
            issues.Add(Error("card_goal_missing", "Card must contain a non-empty Goal section.", resolvedPath));
        }

        if (!ContainsNonEmptySection(lines, "Acceptance"))
        {
            issues.Add(Error("card_acceptance_missing", "Card must contain a non-empty Acceptance section.", resolvedPath));
        }

        if (!ContainsNonEmptySection(lines, "Scope"))
        {
            issues.Add(Warning("card_scope_missing", "Card should contain an explicit Scope section.", resolvedPath));
        }

        try
        {
            var card = new CardParser().Parse(resolvedPath);
            var methodology = new RuntimeMethodologyComplianceService(paths).AssessCard(card, resolvedPath);
            if (methodology.Applies && !methodology.Acknowledged)
            {
                issues.Add(Warning(
                    "methodology_acknowledgment_missing",
                    "Card requires methodology acknowledgment before execution work.",
                    resolvedPath));
            }

            var expectedCardId = Path.GetFileNameWithoutExtension(resolvedPath);
            if (!string.Equals(card.CardId, expectedCardId, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Warning(
                    "card_filename_mismatch",
                    $"Card heading id '{card.CardId}' does not match file name '{expectedCardId}'.",
                    resolvedPath));
            }
        }
        catch (Exception exception)
        {
            issues.Add(Error("card_parse_failed", exception.Message, resolvedPath));
        }

        return Finalize("card", resolvedPath, issues, strict);
    }

    public SpecificationValidationResult ValidateTask(string taskPath)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(taskPath);
        if (!File.Exists(resolvedPath))
        {
            issues.Add(Error("task_missing", "Task node file does not exist.", resolvedPath));
            return Finalize("task", resolvedPath, issues, strict: false);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(resolvedPath));
        }
        catch (Exception exception)
        {
            issues.Add(Error("task_json_invalid", exception.Message, resolvedPath));
            return Finalize("task", resolvedPath, issues, strict: false);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("task_root_invalid", "Task node must be a JSON object.", resolvedPath));
                return Finalize("task", resolvedPath, issues, strict: false);
            }

            ValidateRequiredInt(root, "schema_version", expectedValue: 1, issues, resolvedPath);
            ValidateRequiredString(root, "task_id", issues, resolvedPath, value =>
            {
                if (!value.StartsWith("T-", StringComparison.Ordinal))
                {
                    issues.Add(Error("task_id_invalid", "task_id must start with 'T-'.", resolvedPath, "task_id"));
                }
            });

            ValidateRequiredString(root, "title", issues, resolvedPath);
            ValidateRequiredString(root, "status", issues, resolvedPath, value =>
            {
                if (!CanonicalTaskStatuses.Contains(value))
                {
                    issues.Add(Error(
                        "task_status_invalid",
                        $"status '{value}' is not a canonical task status ({string.Join(", ", CanonicalTaskStatuses.OrderBy(item => item, StringComparer.Ordinal))}).",
                        resolvedPath,
                        "status"));
                }
            });
            ValidateRequiredString(root, "task_type", issues, resolvedPath, value =>
            {
                if (!CanonicalTaskTypes.Contains(value))
                {
                    issues.Add(Error(
                        "task_type_invalid",
                        $"task_type '{value}' is not canonical.",
                        resolvedPath,
                        "task_type"));
                }
            });
            ValidateRequiredString(root, "priority", issues, resolvedPath, value =>
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, "^P[0-9]+$"))
                {
                    issues.Add(Error("task_priority_invalid", $"priority '{value}' must match P<n>.", resolvedPath, "priority"));
                }
            });
            ValidateRequiredString(root, "source", issues, resolvedPath);
            ValidateRequiredArray(root, "dependencies", issues, resolvedPath);
            ValidateRequiredArray(root, "scope", issues, resolvedPath);
            ValidateRequiredArray(root, "acceptance", issues, resolvedPath);
            ValidateRequiredObject(root, "validation", issues, resolvedPath, validation =>
            {
                ValidateRequiredArray(validation, "commands", issues, resolvedPath, "validation.commands");
                ValidateRequiredArray(validation, "checks", issues, resolvedPath, "validation.checks");
                ValidateRequiredArray(validation, "expected_evidence", issues, resolvedPath, "validation.expected_evidence");
            });
            ValidateRequiredInt(root, "retry_count", expectedValue: null, issues, resolvedPath);
            ValidateRequiredDate(root, "created_at", issues, resolvedPath);
            ValidateRequiredDate(root, "updated_at", issues, resolvedPath);

            if (root.TryGetProperty("planner_review", out var plannerReview) && plannerReview.ValueKind == JsonValueKind.Object)
            {
                if (plannerReview.TryGetProperty("verdict", out var verdict) && verdict.ValueKind == JsonValueKind.String)
                {
                    var verdictValue = verdict.GetString() ?? string.Empty;
                    if (!CanonicalPlannerVerdicts.Contains(verdictValue))
                    {
                        issues.Add(Error(
                            "planner_review_verdict_invalid",
                            $"planner_review.verdict '{verdictValue}' is not canonical.",
                            resolvedPath,
                            "planner_review.verdict"));
                    }
                }
            }
        }

        return Finalize("task", resolvedPath, issues, strict: false);
    }

    public SpecificationValidationResult ValidateMemory(string memoryMetaPath, bool strict)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(memoryMetaPath);
        if (!File.Exists(resolvedPath))
        {
            issues.Add(Error("memory_meta_missing", "Memory meta file does not exist.", resolvedPath));
            return Finalize("memory", resolvedPath, issues, strict);
        }

        if (!string.Equals(Path.GetExtension(resolvedPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("memory_meta_extension", "Memory metadata should be a JSON file.", resolvedPath));
        }

        if (!resolvedPath.EndsWith(".meta.json", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("memory_meta_naming", "Memory metadata should use the '*.meta.json' naming convention.", resolvedPath));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(resolvedPath));
        }
        catch (Exception exception)
        {
            issues.Add(Error("memory_meta_json_invalid", exception.Message, resolvedPath));
            return Finalize("memory", resolvedPath, issues, strict);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("memory_meta_root_invalid", "Memory metadata must be a JSON object.", resolvedPath));
                return Finalize("memory", resolvedPath, issues, strict);
            }

            WarnIfMissingString(root, "entity_id", issues, resolvedPath);
            WarnIfMissingString(root, "kind", issues, resolvedPath);
            WarnIfMissingString(root, "scope", issues, resolvedPath);
            WarnIfMissingString(root, "change_control", issues, resolvedPath);
        }

        return Finalize("memory", resolvedPath, issues, strict);
    }

    public SpecificationValidationResult ValidateRuntimePackPolicyPackage(string packagePath)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(packagePath);
        var validation = new RuntimePackPolicyPackageValidationService(configRepository).Validate(resolvedPath);
        foreach (var failureCode in validation.FailureCodes)
        {
            issues.Add(Error(
                failureCode,
                RuntimePackPolicyPackageValidationService.DescribeFailureCode(failureCode),
                resolvedPath));
        }

        return Finalize("runtime_pack_policy_package", resolvedPath, issues, strict: false);
    }

    public SpecificationValidationResult ValidateSafety(string actor, string operation, IReadOnlyList<string> targetPaths)
    {
        var issues = new List<ValidationIssue>();
        var normalizedActor = actor.Trim().ToLowerInvariant();
        var normalizedOperation = NormalizeOperation(operation);
        if (normalizedActor is not ("worker" or "planner" or "operator"))
        {
            issues.Add(Error("actor_invalid", "actor must be planner, worker, or operator.", actor, "actor"));
        }

        if (normalizedOperation is null)
        {
            issues.Add(Error("operation_invalid", "operation must be read, write, delete, or execute.", operation, "operation"));
        }

        if (targetPaths.Count == 0)
        {
            issues.Add(Error("path_missing", "At least one target path is required.", repoRoot, "path"));
        }

        if (issues.Any(issue => string.Equals(issue.Severity, "error", StringComparison.Ordinal)))
        {
            return Finalize("safety", repoRoot, issues, strict: false);
        }

        var rules = configRepository.LoadSafetyRules();
        foreach (var rawPath in targetPaths)
        {
            EvaluateSafetyPath(normalizedActor, normalizedOperation!, rawPath, rules, issues);
        }

        return Finalize("safety", repoRoot, issues, strict: false);
    }

    public SpecificationValidationResult ValidatePackArtifact(string packArtifactPath)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(packArtifactPath);
        var root = LoadObjectDocument("pack_artifact", resolvedPath, issues);
        if (root is null)
        {
            return Finalize("pack_artifact", resolvedPath, issues, strict: false);
        }

        ValidateRequiredString(root.Value, "schemaVersion", issues, resolvedPath, "schemaVersion", value =>
        {
            if (!string.Equals(value, "1.0", StringComparison.Ordinal))
            {
                issues.Add(Error("schemaVersion_invalid", "'schemaVersion' must equal '1.0'.", resolvedPath, "schemaVersion"));
            }
        });
        ValidateRequiredString(root.Value, "packId", issues, resolvedPath, "packId");
        ValidateRequiredString(root.Value, "packVersion", issues, resolvedPath, "packVersion");
        ValidateRequiredString(root.Value, "packType", issues, resolvedPath, "packType", value =>
        {
            if (!IsOneOf(value, CanonicalPackTypes))
            {
                issues.Add(Error(
                    "packType_invalid",
                    $"'packType' must be one of: {string.Join(", ", CanonicalPackTypes.OrderBy(item => item, StringComparer.Ordinal))}.",
                    resolvedPath,
                    "packType"));
            }
        });
        ValidateRequiredString(root.Value, "channel", issues, resolvedPath, "channel");

        ValidateRequiredObject(root.Value, "runtimeCompatibility", issues, resolvedPath, compatibility =>
        {
            ValidateRequiredString(compatibility, "minVersion", issues, resolvedPath, "runtimeCompatibility.minVersion");
            ValidateOptionalStringOrNull(compatibility, "maxVersion", issues, resolvedPath, "runtimeCompatibility.maxVersion");
        }, "runtimeCompatibility");

        ValidateRequiredObject(root.Value, "kernelCompatibility", issues, resolvedPath, compatibility =>
        {
            ValidateRequiredString(compatibility, "minVersion", issues, resolvedPath, "kernelCompatibility.minVersion");
            ValidateOptionalStringOrNull(compatibility, "maxVersion", issues, resolvedPath, "kernelCompatibility.maxVersion");
        }, "kernelCompatibility");

        ValidateRequiredObject(root.Value, "executionProfiles", issues, resolvedPath, profiles =>
        {
            ValidateRequiredString(profiles, "policyPreset", issues, resolvedPath, "executionProfiles.policyPreset");
            ValidateRequiredString(profiles, "gatePreset", issues, resolvedPath, "executionProfiles.gatePreset");
            ValidateRequiredString(profiles, "validatorProfile", issues, resolvedPath, "executionProfiles.validatorProfile");
            ValidateRequiredString(profiles, "environmentProfile", issues, resolvedPath, "executionProfiles.environmentProfile");
            ValidateOptionalStringOrNull(profiles, "routingProfile", issues, resolvedPath, "executionProfiles.routingProfile");
            ValidateOptionalStringArray(profiles, "providerAllowlist", issues, resolvedPath, "executionProfiles.providerAllowlist");
        }, "executionProfiles");

        ValidateRequiredStringArray(root.Value, "operatorChecklistRefs", issues, resolvedPath, "operatorChecklistRefs");

        ValidateRequiredObject(root.Value, "signature", issues, resolvedPath, signature =>
        {
            ValidateRequiredString(signature, "scheme", issues, resolvedPath, "signature.scheme");
            ValidateRequiredString(signature, "keyId", issues, resolvedPath, "signature.keyId");
            ValidateRequiredString(signature, "digest", issues, resolvedPath, "signature.digest");
        }, "signature");

        ValidateRequiredObject(root.Value, "provenance", issues, resolvedPath, provenance =>
        {
            ValidateRequiredDate(provenance, "publishedAtUtc", issues, resolvedPath, "provenance.publishedAtUtc");
            ValidateRequiredString(provenance, "publishedBy", issues, resolvedPath, "provenance.publishedBy");
            ValidateRequiredString(provenance, "sourcePackLine", issues, resolvedPath, "provenance.sourcePackLine");
            ValidateRequiredString(provenance, "sourceGenerationId", issues, resolvedPath, "provenance.sourceGenerationId");
        }, "provenance");

        ValidateOptionalStringOrNull(root.Value, "releaseNoteRef", issues, resolvedPath, "releaseNoteRef");
        ValidateOptionalStringOrNull(root.Value, "parentPackVersion", issues, resolvedPath, "parentPackVersion");
        ValidateOptionalStringOrNull(root.Value, "approvalRef", issues, resolvedPath, "approvalRef");
        ValidateOptionalStringArray(root.Value, "supersedes", issues, resolvedPath, "supersedes");

        return Finalize("pack_artifact", resolvedPath, issues, strict: false);
    }

    public SpecificationValidationResult ValidateRuntimePackAttribution(string attributionPath)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(attributionPath);
        var root = LoadObjectDocument("runtime_pack_attribution", resolvedPath, issues);
        if (root is null)
        {
            return Finalize("runtime_pack_attribution", resolvedPath, issues, strict: false);
        }

        ValidateRequiredString(root.Value, "schemaVersion", issues, resolvedPath, "schemaVersion", value =>
        {
            if (!string.Equals(value, "1.0", StringComparison.Ordinal))
            {
                issues.Add(Error("schemaVersion_invalid", "'schemaVersion' must equal '1.0'.", resolvedPath, "schemaVersion"));
            }
        });
        ValidateRequiredString(root.Value, "packId", issues, resolvedPath, "packId");
        ValidateRequiredString(root.Value, "packVersion", issues, resolvedPath, "packVersion");
        ValidateRequiredString(root.Value, "channel", issues, resolvedPath, "channel");
        ValidateOptionalStringOrNull(root.Value, "artifactRef", issues, resolvedPath, "artifactRef");

        ValidateRequiredObject(root.Value, "executionProfiles", issues, resolvedPath, profiles =>
        {
            ValidateRequiredString(profiles, "policyPreset", issues, resolvedPath, "executionProfiles.policyPreset");
            ValidateRequiredString(profiles, "gatePreset", issues, resolvedPath, "executionProfiles.gatePreset");
            ValidateRequiredString(profiles, "validatorProfile", issues, resolvedPath, "executionProfiles.validatorProfile");
            ValidateRequiredString(profiles, "environmentProfile", issues, resolvedPath, "executionProfiles.environmentProfile");
            ValidateOptionalStringOrNull(profiles, "routingProfile", issues, resolvedPath, "executionProfiles.routingProfile");
        }, "executionProfiles");

        ValidateRequiredObject(root.Value, "source", issues, resolvedPath, source =>
        {
            ValidateRequiredString(source, "assignmentMode", issues, resolvedPath, "source.assignmentMode", value =>
            {
                if (!IsOneOf(value, CanonicalAttributionModes))
                {
                    issues.Add(Error(
                        "runtime_pack_attribution_assignment_mode_invalid",
                        $"'source.assignmentMode' must be one of: {string.Join(", ", CanonicalAttributionModes.OrderBy(item => item, StringComparer.Ordinal))}.",
                        resolvedPath,
                        "source.assignmentMode"));
                }
            });
            ValidateOptionalStringOrNull(source, "assignmentRef", issues, resolvedPath, "source.assignmentRef");
        }, "source");

        ValidateRequiredDate(root.Value, "attributedAtUtc", issues, resolvedPath, "attributedAtUtc");

        return Finalize("runtime_pack_attribution", resolvedPath, issues, strict: false);
    }

    public SpecificationValidationResult ValidateRuntimePackV1(string manifestPath)
    {
        var issues = new List<ValidationIssue>();
        var resolvedPath = ResolveAbsolutePath(manifestPath);
        var root = LoadObjectDocument("runtime_pack_v1", resolvedPath, issues);
        if (root is null)
        {
            return Finalize("runtime_pack_v1", resolvedPath, issues, strict: false);
        }

        ValidateNoUnexpectedProperties(
            root.Value,
            [
                "schemaVersion",
                "packId",
                "packVersion",
                "name",
                "description",
                "publisher",
                "license",
                "compatibility",
                "capabilityKinds",
                "requestedPermissions",
                "recipes",
            ],
            issues,
            resolvedPath,
            "runtime_pack_v1");

        ValidateRequiredString(root.Value, "schemaVersion", issues, resolvedPath, "schemaVersion", value =>
        {
            if (!string.Equals(value, "carves.pack.v1", StringComparison.Ordinal))
            {
                issues.Add(Error("runtime_pack_v1_schema_version_invalid", "'schemaVersion' must equal 'carves.pack.v1'.", resolvedPath, "schemaVersion"));
            }
        });
        ValidateRequiredStableId(root.Value, "packId", issues, resolvedPath, "packId");
        ValidateRequiredString(root.Value, "packVersion", issues, resolvedPath, "packVersion");
        ValidateRequiredString(root.Value, "name", issues, resolvedPath, "name");
        ValidateOptionalStringOrNull(root.Value, "description", issues, resolvedPath, "description");

        ValidateRequiredObject(root.Value, "publisher", issues, resolvedPath, publisher =>
        {
            ValidateNoUnexpectedProperties(
                publisher,
                ["name", "trustLevel", "url"],
                issues,
                resolvedPath,
                "publisher");
            ValidateRequiredString(publisher, "name", issues, resolvedPath, "publisher.name");
            ValidateRequiredString(publisher, "trustLevel", issues, resolvedPath, "publisher.trustLevel", value =>
            {
                if (!IsOneOf(value, CanonicalPackV1PublisherTrustLevels))
                {
                    issues.Add(Error(
                        "runtime_pack_v1_publisher_trust_level_invalid",
                        $"'publisher.trustLevel' must be one of: {string.Join(", ", CanonicalPackV1PublisherTrustLevels.OrderBy(item => item, StringComparer.Ordinal))}.",
                        resolvedPath,
                        "publisher.trustLevel"));
                }
            });
            ValidateOptionalStringOrNull(publisher, "url", issues, resolvedPath, "publisher.url");
        }, "publisher");

        ValidateRequiredObject(root.Value, "license", issues, resolvedPath, license =>
        {
            ValidateNoUnexpectedProperties(
                license,
                ["expression", "url"],
                issues,
                resolvedPath,
                "license");
            ValidateRequiredString(license, "expression", issues, resolvedPath, "license.expression");
            ValidateOptionalStringOrNull(license, "url", issues, resolvedPath, "license.url");
        }, "license");

        ValidateRequiredObject(root.Value, "compatibility", issues, resolvedPath, compatibility =>
        {
            ValidateNoUnexpectedProperties(
                compatibility,
                ["carvesRuntime", "languages", "frameworkHints", "repoSignals"],
                issues,
                resolvedPath,
                "compatibility");
            ValidateRequiredString(compatibility, "carvesRuntime", issues, resolvedPath, "compatibility.carvesRuntime");
            ValidateOptionalStringArrayUnique(compatibility, "languages", issues, resolvedPath, "compatibility.languages");
            ValidateOptionalStringArrayUnique(compatibility, "frameworkHints", issues, resolvedPath, "compatibility.frameworkHints");
            ValidateOptionalStringArrayUnique(compatibility, "repoSignals", issues, resolvedPath, "compatibility.repoSignals");
        }, "compatibility");

        var declaredCapabilities = ValidateCapabilityKinds(root.Value, issues, resolvedPath);

        ValidateRequiredObject(root.Value, "requestedPermissions", issues, resolvedPath, permissions =>
        {
            ValidateNoUnexpectedProperties(
                permissions,
                ["readPaths", "network", "env", "secrets", "truthWrite"],
                issues,
                resolvedPath,
                "requestedPermissions");
            ValidateRequiredStringArrayUnique(permissions, "readPaths", issues, resolvedPath, "requestedPermissions.readPaths");
            ValidateRequiredFalse(permissions, "network", issues, resolvedPath, "requestedPermissions.network");
            ValidateRequiredFalse(permissions, "env", issues, resolvedPath, "requestedPermissions.env");
            ValidateRequiredFalse(permissions, "secrets", issues, resolvedPath, "requestedPermissions.secrets");
            ValidateRequiredFalse(permissions, "truthWrite", issues, resolvedPath, "requestedPermissions.truthWrite");
        }, "requestedPermissions");

        ValidateRequiredObject(root.Value, "recipes", issues, resolvedPath, recipes =>
        {
            ValidateNoUnexpectedProperties(
                recipes,
                ["projectUnderstandingRecipes", "verificationRecipes", "reviewRubrics"],
                issues,
                resolvedPath,
                "recipes");

            ValidateProjectUnderstandingRecipeArray(
                recipes,
                "projectUnderstandingRecipes",
                issues,
                resolvedPath,
                declaredCapabilities.Contains("project_understanding_recipe"));
            ValidateVerificationRecipeArray(
                recipes,
                "verificationRecipes",
                issues,
                resolvedPath,
                declaredCapabilities.Contains("verification_recipe"));
            ValidateReviewRubricArray(
                recipes,
                "reviewRubrics",
                issues,
                resolvedPath,
                declaredCapabilities.Contains("review_rubric"));
        }, "recipes");

        return Finalize("runtime_pack_v1", resolvedPath, issues, strict: false);
    }

    private void EvaluateSafetyPath(string actor, string operation, string rawPath, SafetyRules rules, List<ValidationIssue> issues)
    {
        var (relativePath, insideRepo) = NormalizeRepoRelativePath(rawPath);
        var location = insideRepo ? relativePath : rawPath;

        if (!insideRepo && actor is "worker" or "planner")
        {
            issues.Add(Error("outside_repo_path", "Only operator actions may target paths outside the repo root.", location));
            return;
        }

        if (actor == "operator")
        {
            if (!insideRepo)
            {
                issues.Add(Warning("operator_override_outside_repo", "Operator action targets a path outside the repo root.", location));
                return;
            }

            if (MatchesAny(relativePath, rules.ProtectedPaths))
            {
                issues.Add(Warning("operator_override_protected_path", "Operator action targets a protected path.", location));
            }

            return;
        }

        if (operation == "read")
        {
            return;
        }

        if (MatchesAny(relativePath, rules.ProtectedPaths))
        {
            issues.Add(Error("protected_path", "Path is protected.", location));
            return;
        }

        if (MatchesAny(relativePath, rules.MemoryWritePaths))
        {
            issues.Add(Error("memory_write_forbidden", "Path requires a memory-specific approval path.", location));
            return;
        }

        if (actor == "worker")
        {
            if (MatchesAny(relativePath, rules.ManagedControlPlanePaths))
            {
                issues.Add(Error("managed_control_plane_write_forbidden", "Worker may not write managed control-plane truth directly.", location));
                return;
            }

            if (MatchesAny(relativePath, rules.RestrictedPaths))
            {
                issues.Add(Error("restricted_path", "Path is inside a restricted root.", location));
                return;
            }

            if (!MatchesAny(relativePath, rules.WorkerWritablePaths))
            {
                issues.Add(Error("unwritable_path", "Path is outside worker writable roots.", location));
            }

            return;
        }

        if (actor == "planner")
        {
            if (!MatchesAny(relativePath, PlannerWritablePrefixes) && !MatchesAny(relativePath, rules.ManagedControlPlanePaths))
            {
                issues.Add(Error("planner_write_outside_control_plane", "Planner writes must stay inside governed control-plane paths.", location));
            }
        }
    }

    private static SpecificationValidationResult Finalize(string validator, string target, List<ValidationIssue> issues, bool strict)
    {
        if (strict)
        {
            issues = issues
                .Select(issue => string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase)
                    ? issue with { Severity = "error", Code = $"{issue.Code}_strict" }
                    : issue)
                .ToList();
        }

        return new SpecificationValidationResult(validator, target, issues);
    }

    private static bool ContainsNonEmptySection(IReadOnlyList<string> lines, string sectionName)
    {
        var inSection = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line[3..].Trim(), sectionName, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static void ValidateRequiredString(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, Action<string>? validate = null)
        => ValidateRequiredString(root, propertyName, issues, path, propertyName, validate);

    private static void ValidateRequiredString(
        JsonElement root,
        string propertyName,
        List<ValidationIssue> issues,
        string path,
        string location,
        Action<string>? validate = null)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be a non-empty string.", path, location));
            return;
        }

        validate?.Invoke(property.GetString()!);
    }

    private static void WarnIfMissingString(JsonElement root, string propertyName, List<ValidationIssue> issues, string path)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(Warning($"{propertyName}_missing", $"'{propertyName}' is recommended for memory metadata.", path, propertyName));
        }
    }

    private static void ValidateRequiredArray(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string? location = null)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an array.", path, location ?? propertyName));
        }
    }

    private static void ValidateRequiredStringArray(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string? location = null)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an array.", path, location ?? propertyName));
            return;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Error($"{propertyName}_entry_invalid", $"'{propertyName}' entries must be non-empty strings.", path, location ?? propertyName));
                return;
            }
        }
    }

    private static void ValidateRequiredStringArrayUnique(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string? location = null)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an array.", path, location ?? propertyName));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Error($"{propertyName}_entry_invalid", $"'{propertyName}' entries must be non-empty strings.", path, location ?? propertyName));
                return;
            }

            if (!seen.Add(item.GetString()!))
            {
                issues.Add(Error($"{propertyName}_duplicate", $"'{propertyName}' entries must be unique.", path, location ?? propertyName));
                return;
            }
        }
    }

    private static void ValidateRequiredObject(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, Action<JsonElement>? validate = null)
        => ValidateRequiredObject(root, propertyName, issues, path, validate, propertyName);

    private static void ValidateRequiredObject(
        JsonElement root,
        string propertyName,
        List<ValidationIssue> issues,
        string path,
        Action<JsonElement>? validate,
        string location)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an object.", path, location));
            return;
        }

        validate?.Invoke(property);
    }

    private static void ValidateRequiredInt(JsonElement root, string propertyName, int? expectedValue, List<ValidationIssue> issues, string path)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value))
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an integer.", path, propertyName));
            return;
        }

        if (expectedValue is not null && value != expectedValue.Value)
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must equal {expectedValue.Value}.", path, propertyName));
        }
    }

    private static void ValidateRequiredDate(JsonElement root, string propertyName, List<ValidationIssue> issues, string path)
        => ValidateRequiredDate(root, propertyName, issues, path, propertyName);

    private static void ValidateRequiredDate(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String || !DateTimeOffset.TryParse(property.GetString(), out _))
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must be an ISO-8601 date-time string.", path, location));
        }
    }

    private static void ValidateOptionalStringOrNull(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must be null or a non-empty string.", path, location));
        }
    }

    private static void ValidateOptionalStringArray(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must be an array when provided.", path, location));
            return;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Error($"{propertyName}_entry_invalid", $"'{propertyName}' entries must be non-empty strings.", path, location));
                return;
            }
        }
    }

    private static void ValidateOptionalStringArrayUnique(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must be an array when provided.", path, location));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Error($"{propertyName}_entry_invalid", $"'{propertyName}' entries must be non-empty strings.", path, location));
                return;
            }

            if (!seen.Add(item.GetString()!))
            {
                issues.Add(Error($"{propertyName}_duplicate", $"'{propertyName}' entries must be unique.", path, location));
                return;
            }
        }
    }

    private static void ValidateRequiredFalse(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.False)
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must equal false.", path, location));
        }
    }

    private static void ValidateRequiredBoolean(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        if (!root.TryGetProperty(propertyName, out var property) || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must be a boolean.", path, location));
        }
    }

    private static void ValidateRequiredStableId(JsonElement root, string propertyName, List<ValidationIssue> issues, string path, string location)
    {
        ValidateRequiredString(root, propertyName, issues, path, location, value =>
        {
            if (!IsStableId(value))
            {
                issues.Add(Error($"{propertyName}_invalid", $"'{propertyName}' must be a canonical stable id.", path, location));
            }
        });
    }

    private static HashSet<string> ValidateCapabilityKinds(JsonElement root, List<ValidationIssue> issues, string path)
    {
        var capabilityKinds = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("capabilityKinds", out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("capabilityKinds_missing", "'capabilityKinds' must be an array.", path, "capabilityKinds"));
            return capabilityKinds;
        }

        if (property.GetArrayLength() == 0)
        {
            issues.Add(Error("capabilityKinds_empty", "'capabilityKinds' must contain at least one value.", path, "capabilityKinds"));
            return capabilityKinds;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(Error("capabilityKinds_entry_invalid", "'capabilityKinds' entries must be non-empty strings.", path, "capabilityKinds"));
                continue;
            }

            var value = item.GetString()!;
            if (!IsOneOf(value, CanonicalPackV1CapabilityKinds))
            {
                issues.Add(Error(
                    "runtime_pack_v1_capability_kind_invalid",
                    $"'capabilityKinds' entries must be one of: {string.Join(", ", CanonicalPackV1CapabilityKinds.OrderBy(item => item, StringComparer.Ordinal))}.",
                    path,
                    "capabilityKinds"));
                continue;
            }

            if (!capabilityKinds.Add(value))
            {
                issues.Add(Error("capabilityKinds_duplicate", "'capabilityKinds' entries must be unique.", path, "capabilityKinds"));
            }
        }

        return capabilityKinds;
    }

    private static void ValidateProjectUnderstandingRecipeArray(
        JsonElement root,
        string propertyName,
        List<ValidationIssue> issues,
        string path,
        bool requiredByCapability)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an array.", path, $"recipes.{propertyName}"));
            return;
        }

        if (requiredByCapability && property.GetArrayLength() == 0)
        {
            issues.Add(Error($"{propertyName}_required", $"'{propertyName}' must contain at least one recipe when its capability kind is declared.", path, $"recipes.{propertyName}"));
        }

        if (!requiredByCapability && property.GetArrayLength() > 0)
        {
            issues.Add(Error($"{propertyName}_unexpected", $"'{propertyName}' must be empty unless its capability kind is declared.", path, $"recipes.{propertyName}"));
        }

        ValidateProjectUnderstandingRecipes(property, issues, path, $"recipes.{propertyName}");
    }

    private static void ValidateVerificationRecipeArray(
        JsonElement root,
        string propertyName,
        List<ValidationIssue> issues,
        string path,
        bool requiredByCapability)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an array.", path, $"recipes.{propertyName}"));
            return;
        }

        if (requiredByCapability && property.GetArrayLength() == 0)
        {
            issues.Add(Error($"{propertyName}_required", $"'{propertyName}' must contain at least one recipe when its capability kind is declared.", path, $"recipes.{propertyName}"));
        }

        if (!requiredByCapability && property.GetArrayLength() > 0)
        {
            issues.Add(Error($"{propertyName}_unexpected", $"'{propertyName}' must be empty unless its capability kind is declared.", path, $"recipes.{propertyName}"));
        }

        ValidateVerificationRecipes(property, issues, path, $"recipes.{propertyName}");
    }

    private static void ValidateReviewRubricArray(
        JsonElement root,
        string propertyName,
        List<ValidationIssue> issues,
        string path,
        bool requiredByCapability)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error($"{propertyName}_missing", $"'{propertyName}' must be an array.", path, $"recipes.{propertyName}"));
            return;
        }

        if (requiredByCapability && property.GetArrayLength() == 0)
        {
            issues.Add(Error($"{propertyName}_required", $"'{propertyName}' must contain at least one rubric when its capability kind is declared.", path, $"recipes.{propertyName}"));
        }

        if (!requiredByCapability && property.GetArrayLength() > 0)
        {
            issues.Add(Error($"{propertyName}_unexpected", $"'{propertyName}' must be empty unless its capability kind is declared.", path, $"recipes.{propertyName}"));
        }

        ValidateReviewRubrics(property, issues, path, $"recipes.{propertyName}");
    }

    private static void ValidateProjectUnderstandingRecipes(JsonElement array, List<ValidationIssue> issues, string path, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemLocation = $"{location}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("project_understanding_recipe_invalid", "Project understanding recipe entries must be objects.", path, itemLocation));
                index++;
                continue;
            }

            ValidateNoUnexpectedProperties(
                item,
                ["id", "description", "repoSignals", "frameworkHints", "includeGlobs", "excludeGlobs", "priorityRules"],
                issues,
                path,
                itemLocation);
            ValidateRequiredStableId(item, "id", issues, path, $"{itemLocation}.id");
            if (item.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idProperty.GetString()))
            {
                var id = idProperty.GetString()!;
                if (!seen.Add(id))
                {
                    issues.Add(Error("project_understanding_recipe_id_duplicate", "Project understanding recipe ids must be unique.", path, $"{itemLocation}.id"));
                }
            }
            ValidateRequiredString(item, "description", issues, path, $"{itemLocation}.description");
            ValidateOptionalStringArrayUnique(item, "repoSignals", issues, path, $"{itemLocation}.repoSignals");
            ValidateOptionalStringArrayUnique(item, "frameworkHints", issues, path, $"{itemLocation}.frameworkHints");
            ValidateOptionalStringArrayUnique(item, "includeGlobs", issues, path, $"{itemLocation}.includeGlobs");
            ValidateOptionalStringArrayUnique(item, "excludeGlobs", issues, path, $"{itemLocation}.excludeGlobs");
            if (item.TryGetProperty("priorityRules", out var priorityRules))
            {
                ValidatePriorityRules(priorityRules, issues, path, $"{itemLocation}.priorityRules");
            }

            index++;
        }
    }

    private static void ValidatePriorityRules(JsonElement array, List<ValidationIssue> issues, string path, string location)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Error("priorityRules_invalid", "'priorityRules' must be an array when provided.", path, location));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemLocation = $"{location}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("priority_rule_invalid", "Priority rule entries must be objects.", path, itemLocation));
                index++;
                continue;
            }

            ValidateNoUnexpectedProperties(item, ["id", "glob", "weight"], issues, path, itemLocation);
            ValidateRequiredStableId(item, "id", issues, path, $"{itemLocation}.id");
            if (item.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idProperty.GetString()))
            {
                var id = idProperty.GetString()!;
                if (!seen.Add(id))
                {
                    issues.Add(Error("priority_rule_id_duplicate", "Priority rule ids must be unique within one recipe.", path, $"{itemLocation}.id"));
                }
            }

            ValidateRequiredString(item, "glob", issues, path, $"{itemLocation}.glob");
            if (!item.TryGetProperty("weight", out var weightProperty) || weightProperty.ValueKind != JsonValueKind.Number || !weightProperty.TryGetInt32(out var weight) || weight is < 0 or > 100)
            {
                issues.Add(Error("priority_rule_weight_invalid", "'weight' must be an integer between 0 and 100.", path, $"{itemLocation}.weight"));
            }

            index++;
        }
    }

    private static void ValidateVerificationRecipes(JsonElement array, List<ValidationIssue> issues, string path, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemLocation = $"{location}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("verification_recipe_invalid", "Verification recipe entries must be objects.", path, itemLocation));
                index++;
                continue;
            }

            ValidateNoUnexpectedProperties(item, ["id", "description", "commands"], issues, path, itemLocation);
            ValidateRequiredStableId(item, "id", issues, path, $"{itemLocation}.id");
            if (item.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idProperty.GetString()))
            {
                var id = idProperty.GetString()!;
                if (!seen.Add(id))
                {
                    issues.Add(Error("verification_recipe_id_duplicate", "Verification recipe ids must be unique.", path, $"{itemLocation}.id"));
                }
            }

            ValidateRequiredString(item, "description", issues, path, $"{itemLocation}.description");
            if (!item.TryGetProperty("commands", out var commands) || commands.ValueKind != JsonValueKind.Array)
            {
                issues.Add(Error("verification_recipe_commands_missing", "'commands' must be an array.", path, $"{itemLocation}.commands"));
            }
            else
            {
                if (commands.GetArrayLength() == 0)
                {
                    issues.Add(Error("verification_recipe_commands_empty", "'commands' must contain at least one command.", path, $"{itemLocation}.commands"));
                }

                ValidateVerificationCommands(commands, issues, path, $"{itemLocation}.commands");
            }

            index++;
        }
    }

    private static void ValidateVerificationCommands(JsonElement array, List<ValidationIssue> issues, string path, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemLocation = $"{location}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("verification_command_invalid", "Verification command entries must be objects.", path, itemLocation));
                index++;
                continue;
            }

            ValidateNoUnexpectedProperties(item, ["id", "kind", "executable", "args", "cwd", "required"], issues, path, itemLocation);
            ValidateRequiredStableId(item, "id", issues, path, $"{itemLocation}.id");
            if (item.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idProperty.GetString()))
            {
                var id = idProperty.GetString()!;
                if (!seen.Add(id))
                {
                    issues.Add(Error("verification_command_id_duplicate", "Verification command ids must be unique within one recipe.", path, $"{itemLocation}.id"));
                }
            }

            ValidateRequiredString(item, "kind", issues, path, $"{itemLocation}.kind", value =>
            {
                if (!IsOneOf(value, CanonicalPackV1VerificationCommandKinds))
                {
                    issues.Add(Error(
                        "verification_command_kind_invalid",
                        $"'kind' must be one of: {string.Join(", ", CanonicalPackV1VerificationCommandKinds.OrderBy(entry => entry, StringComparer.Ordinal))}.",
                        path,
                        $"{itemLocation}.kind"));
                }
            });
            ValidateRequiredString(item, "executable", issues, path, $"{itemLocation}.executable");
            ValidateRequiredStringArray(item, "args", issues, path, $"{itemLocation}.args");
            ValidateRequiredString(item, "cwd", issues, path, $"{itemLocation}.cwd");
            ValidateRequiredBoolean(item, "required", issues, path, $"{itemLocation}.required");

            index++;
        }
    }

    private static void ValidateReviewRubrics(JsonElement array, List<ValidationIssue> issues, string path, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemLocation = $"{location}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("review_rubric_invalid", "Review rubric entries must be objects.", path, itemLocation));
                index++;
                continue;
            }

            ValidateNoUnexpectedProperties(item, ["id", "description", "checklistItems"], issues, path, itemLocation);
            ValidateRequiredStableId(item, "id", issues, path, $"{itemLocation}.id");
            if (item.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idProperty.GetString()))
            {
                var id = idProperty.GetString()!;
                if (!seen.Add(id))
                {
                    issues.Add(Error("review_rubric_id_duplicate", "Review rubric ids must be unique.", path, $"{itemLocation}.id"));
                }
            }

            ValidateRequiredString(item, "description", issues, path, $"{itemLocation}.description");
            if (!item.TryGetProperty("checklistItems", out var checklistItems) || checklistItems.ValueKind != JsonValueKind.Array)
            {
                issues.Add(Error("review_rubric_checklist_missing", "'checklistItems' must be an array.", path, $"{itemLocation}.checklistItems"));
            }
            else
            {
                if (checklistItems.GetArrayLength() == 0)
                {
                    issues.Add(Error("review_rubric_checklist_empty", "'checklistItems' must contain at least one item.", path, $"{itemLocation}.checklistItems"));
                }

                ValidateChecklistItems(checklistItems, issues, path, $"{itemLocation}.checklistItems");
            }

            index++;
        }
    }

    private static void ValidateChecklistItems(JsonElement array, List<ValidationIssue> issues, string path, string location)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            var itemLocation = $"{location}[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("review_check_invalid", "Review checklist items must be objects.", path, itemLocation));
                index++;
                continue;
            }

            ValidateNoUnexpectedProperties(item, ["id", "severity", "text"], issues, path, itemLocation);
            ValidateRequiredStableId(item, "id", issues, path, $"{itemLocation}.id");
            if (item.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(idProperty.GetString()))
            {
                var id = idProperty.GetString()!;
                if (!seen.Add(id))
                {
                    issues.Add(Error("review_check_id_duplicate", "Review checklist item ids must be unique within one rubric.", path, $"{itemLocation}.id"));
                }
            }

            ValidateRequiredString(item, "severity", issues, path, $"{itemLocation}.severity", value =>
            {
                if (!IsOneOf(value, CanonicalPackV1ChecklistSeverities))
                {
                    issues.Add(Error(
                        "review_check_severity_invalid",
                        $"'severity' must be one of: {string.Join(", ", CanonicalPackV1ChecklistSeverities.OrderBy(entry => entry, StringComparer.Ordinal))}.",
                        path,
                        $"{itemLocation}.severity"));
                }
            });
            ValidateRequiredString(item, "text", issues, path, $"{itemLocation}.text");

            index++;
        }
    }

    private static void ValidateNoUnexpectedProperties(JsonElement root, IReadOnlyCollection<string> allowedProperties, List<ValidationIssue> issues, string path, string location)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!allowedProperties.Contains(property.Name))
            {
                issues.Add(Error("unknown_property", $"Unknown property '{property.Name}' is not allowed.", path, location));
            }
        }
    }

    private static bool IsStableId(string value)
        => System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+([._-][a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static ValidationIssue Error(string code, string message, string location, string? field = null)
        => new("error", code, message, location, field);

    private static ValidationIssue Warning(string code, string message, string location, string? field = null)
        => new("warning", code, message, location, field);

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0)
            {
                chars.Add('_');
            }

            chars.Add(char.ToLowerInvariant(character));
        }

        return new string(chars.ToArray());
    }

    private static bool IsOneOf(string value, IReadOnlySet<string> allowedValues)
        => allowedValues.Contains(value);

    private static JsonElement? LoadObjectDocument(string validator, string resolvedPath, List<ValidationIssue> issues)
    {
        if (!File.Exists(resolvedPath))
        {
            issues.Add(Error($"{validator}_missing", "JSON file does not exist.", resolvedPath));
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(resolvedPath));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error($"{validator}_root_invalid", "JSON root must be an object.", resolvedPath));
                return null;
            }

            return document.RootElement.Clone();
        }
        catch (Exception exception)
        {
            issues.Add(Error($"{validator}_json_invalid", exception.Message, resolvedPath));
            return null;
        }
    }

    private string ResolveAbsolutePath(string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private (string RelativePath, bool InsideRepo) NormalizeRepoRelativePath(string path)
    {
        var fullPath = ResolveAbsolutePath(path);
        var repoPath = Path.GetFullPath(repoRoot);
        var repoPrefix = repoPath.EndsWith(Path.DirectorySeparatorChar) || repoPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? repoPath
            : repoPath + Path.DirectorySeparatorChar;
        var insideRepo = string.Equals(fullPath, repoPath, StringComparison.OrdinalIgnoreCase)
            || fullPath.StartsWith(repoPrefix, StringComparison.OrdinalIgnoreCase);
        if (!insideRepo)
        {
            return (fullPath.Replace('\\', '/'), false);
        }

        var relative = Path.GetRelativePath(repoPath, fullPath).Replace('\\', '/');
        return (relative, true);
    }

    private static string? NormalizeOperation(string operation)
    {
        var normalized = operation.Trim().TrimStart('-').ToLowerInvariant();
        return normalized switch
        {
            "read" => "read",
            "write" => "write",
            "delete" => "delete",
            "execute" => "execute",
            _ => null,
        };
    }

    private static bool MatchesAny(string path, IReadOnlyList<string> prefixes)
    {
        return prefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record SpecificationValidationResult(
    string Validator,
    string Target,
    IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => !string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase));
}

public sealed record ValidationIssue(
    string Severity,
    string Code,
    string Message,
    string Location,
    string? Field = null);
