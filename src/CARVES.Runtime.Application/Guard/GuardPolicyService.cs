using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Guard;

public sealed class GuardPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly HashSet<string> TopLevelFields = new(StringComparer.Ordinal)
    {
        "schema_version",
        "policy_id",
        "description",
        "path_policy",
        "change_budget",
        "dependency_policy",
        "change_shape",
        "decision",
    };

    private static readonly HashSet<string> PathPolicyFields = new(StringComparer.Ordinal)
    {
        "path_case",
        "allowed_path_prefixes",
        "protected_path_prefixes",
        "outside_allowed_action",
        "protected_path_action",
    };

    private static readonly HashSet<string> ChangeBudgetFields = new(StringComparer.Ordinal)
    {
        "max_changed_files",
        "max_total_additions",
        "max_total_deletions",
        "max_file_additions",
        "max_file_deletions",
        "max_renames",
    };

    private static readonly HashSet<string> DependencyPolicyFields = new(StringComparer.Ordinal)
    {
        "manifest_paths",
        "lockfile_paths",
        "manifest_without_lockfile_action",
        "lockfile_without_manifest_action",
        "new_dependency_action",
    };

    private static readonly HashSet<string> ChangeShapeFields = new(StringComparer.Ordinal)
    {
        "allow_rename_with_content_change",
        "allow_delete_without_replacement",
        "generated_path_prefixes",
        "generated_path_action",
        "mixed_feature_and_refactor_action",
        "require_tests_for_source_changes",
        "source_path_prefixes",
        "test_path_prefixes",
        "missing_tests_action",
    };

    private static readonly HashSet<string> DecisionFields = new(StringComparer.Ordinal)
    {
        "fail_closed",
        "default_outcome",
        "review_is_passing",
        "emit_evidence",
    };

    public GuardPolicyLoadResult Load(string repositoryRoot, string policyPath)
    {
        var fullPolicyPath = Path.GetFullPath(Path.Combine(repositoryRoot, policyPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullPolicyPath))
        {
            return GuardPolicyLoadResult.Invalid("policy.missing", $"Policy file '{policyPath}' was not found.");
        }

        try
        {
            var json = File.ReadAllText(fullPolicyPath);
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            var unknownField = FindUnknownField(document.RootElement, TopLevelFields);
            if (!string.IsNullOrWhiteSpace(unknownField))
            {
                return GuardPolicyLoadResult.Invalid("policy.unknown_field", $"Unknown top-level policy field '{unknownField}'.");
            }

            var policy = JsonSerializer.Deserialize<GuardPolicyDocument>(json, JsonOptions);
            if (policy is null)
            {
                return GuardPolicyLoadResult.Invalid("policy.invalid", "Policy JSON could not be parsed.");
            }

            return BuildSnapshot(policy, document.RootElement);
        }
        catch (JsonException exception)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid_json", exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", exception.Message);
        }
        catch (IOException exception)
        {
            return GuardPolicyLoadResult.Invalid("policy.read_failed", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return GuardPolicyLoadResult.Invalid("policy.read_failed", exception.Message);
        }
    }

    private static GuardPolicyLoadResult BuildSnapshot(GuardPolicyDocument policy, JsonElement root)
    {
        if (policy.SchemaVersion != 1)
        {
            return GuardPolicyLoadResult.Invalid("policy.unsupported_schema_version", $"Unsupported schema_version '{policy.SchemaVersion}'.");
        }

        if (string.IsNullOrWhiteSpace(policy.PolicyId))
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "policy_id is required.");
        }

        if (policy.PathPolicy is null || !root.TryGetProperty("path_policy", out var pathPolicyElement))
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "path_policy is required.");
        }

        if (policy.ChangeBudget is null || !root.TryGetProperty("change_budget", out var changeBudgetElement))
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "change_budget is required.");
        }

        if (policy.DependencyPolicy is null || !root.TryGetProperty("dependency_policy", out var dependencyPolicyElement))
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "dependency_policy is required.");
        }

        if (policy.ChangeShape is null || !root.TryGetProperty("change_shape", out var changeShapeElement))
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "change_shape is required.");
        }

        if (policy.Decision is null || !root.TryGetProperty("decision", out var decisionElement))
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "decision is required.");
        }

        var unknownSectionField =
            FindUnknownField(pathPolicyElement, PathPolicyFields, "path_policy")
            ?? FindUnknownField(changeBudgetElement, ChangeBudgetFields, "change_budget")
            ?? FindUnknownField(dependencyPolicyElement, DependencyPolicyFields, "dependency_policy")
            ?? FindUnknownField(changeShapeElement, ChangeShapeFields, "change_shape")
            ?? FindUnknownField(decisionElement, DecisionFields, "decision");
        if (!string.IsNullOrWhiteSpace(unknownSectionField))
        {
            return GuardPolicyLoadResult.Invalid("policy.unknown_field", $"Unknown policy field '{unknownSectionField}'.");
        }

        if (policy.PathPolicy.AllowedPathPrefixes is null || policy.PathPolicy.AllowedPathPrefixes.Count == 0)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "path_policy.allowed_path_prefixes must contain at least one entry.");
        }

        if (policy.PathPolicy.ProtectedPathPrefixes is null)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "path_policy.protected_path_prefixes is required.");
        }

        if (policy.ChangeBudget.MaxChangedFiles is null or < 1)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "change_budget.max_changed_files must be greater than zero.");
        }

        if (policy.DependencyPolicy.ManifestPaths is null)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "dependency_policy.manifest_paths is required.");
        }

        if (policy.DependencyPolicy.LockfilePaths is null)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "dependency_policy.lockfile_paths is required.");
        }

        if (policy.ChangeShape.GeneratedPathPrefixes is null)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "change_shape.generated_path_prefixes is required.");
        }

        if (policy.Decision.DefaultOutcome is not "allow")
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "decision.default_outcome must be allow in v1.");
        }

        if (policy.Decision.ReviewIsPassing)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "decision.review_is_passing must be false in v1.");
        }

        if (!policy.Decision.EmitEvidence)
        {
            return GuardPolicyLoadResult.Invalid("policy.invalid", "decision.emit_evidence must be true in v1.");
        }

        var pathPolicy = new GuardPathPolicy(
            CaseSensitive: !string.Equals(policy.PathPolicy.PathCase, "case_insensitive", StringComparison.Ordinal),
            AllowedPathPrefixes: NormalizePatterns(policy.PathPolicy.AllowedPathPrefixes),
            ProtectedPathPrefixes: NormalizePatterns([.. policy.PathPolicy.ProtectedPathPrefixes, ".git/"]),
            OutsideAllowedAction: ParseAction(policy.PathPolicy.OutsideAllowedAction, "path_policy.outside_allowed_action"),
            ProtectedPathAction: ParseAction(policy.PathPolicy.ProtectedPathAction, "path_policy.protected_path_action"));

        var budget = new GuardChangeBudget(
            policy.ChangeBudget.MaxChangedFiles.Value,
            policy.ChangeBudget.MaxTotalAdditions,
            policy.ChangeBudget.MaxTotalDeletions,
            policy.ChangeBudget.MaxFileAdditions,
            policy.ChangeBudget.MaxFileDeletions,
            policy.ChangeBudget.MaxRenames);

        var dependencyPolicy = new GuardDependencyPolicy(
            NormalizePatterns(policy.DependencyPolicy.ManifestPaths),
            NormalizePatterns(policy.DependencyPolicy.LockfilePaths),
            ParseAction(policy.DependencyPolicy.ManifestWithoutLockfileAction, "dependency_policy.manifest_without_lockfile_action"),
            ParseAction(policy.DependencyPolicy.LockfileWithoutManifestAction, "dependency_policy.lockfile_without_manifest_action"),
            ParseAction(policy.DependencyPolicy.NewDependencyAction, "dependency_policy.new_dependency_action"));

        var shapePolicy = new GuardChangeShapePolicy(
            policy.ChangeShape.AllowRenameWithContentChange,
            policy.ChangeShape.AllowDeleteWithoutReplacement,
            NormalizePatterns(policy.ChangeShape.GeneratedPathPrefixes),
            ParseAction(policy.ChangeShape.GeneratedPathAction, "change_shape.generated_path_action"),
            ParseAction(policy.ChangeShape.MixedFeatureAndRefactorAction, "change_shape.mixed_feature_and_refactor_action"),
            policy.ChangeShape.RequireTestsForSourceChanges,
            NormalizePatterns(policy.ChangeShape.SourcePathPrefixes ?? []),
            NormalizePatterns(policy.ChangeShape.TestPathPrefixes ?? []),
            ParseAction(policy.ChangeShape.MissingTestsAction ?? "review", "change_shape.missing_tests_action"));

        var decisionPolicy = new GuardDecisionPolicy(
            policy.Decision.FailClosed,
            GuardPolicyAction.Allow,
            policy.Decision.ReviewIsPassing,
            policy.Decision.EmitEvidence);

        return GuardPolicyLoadResult.Valid(new GuardPolicySnapshot(
            policy.SchemaVersion,
            policy.PolicyId,
            policy.Description,
            pathPolicy,
            budget,
            dependencyPolicy,
            shapePolicy,
            decisionPolicy));
    }

    private static IReadOnlyList<string> NormalizePatterns(IReadOnlyList<string> patterns)
    {
        return patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Select(pattern => pattern.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(pattern => pattern, StringComparer.Ordinal)
            .ToArray();
    }

    private static GuardPolicyAction ParseAction(string? value, string fieldName)
    {
        return value?.ToLowerInvariant() switch
        {
            "allow" => GuardPolicyAction.Allow,
            "review" => GuardPolicyAction.Review,
            "block" => GuardPolicyAction.Block,
            _ => throw new InvalidOperationException($"{fieldName} must be allow, review, or block."),
        };
    }

    private static string? FindUnknownField(JsonElement element, HashSet<string> knownFields)
    {
        return FindUnknownField(element, knownFields, null);
    }

    private static string? FindUnknownField(JsonElement element, HashSet<string> knownFields, string? sectionName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (!knownFields.Contains(property.Name))
            {
                return string.IsNullOrWhiteSpace(sectionName)
                    ? property.Name
                    : $"{sectionName}.{property.Name}";
            }
        }

        return null;
    }

    private sealed class GuardPolicyDocument
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("policy_id")]
        public string PolicyId { get; init; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; init; }

        [JsonPropertyName("path_policy")]
        public PathPolicyDocument? PathPolicy { get; init; }

        [JsonPropertyName("change_budget")]
        public ChangeBudgetDocument? ChangeBudget { get; init; }

        [JsonPropertyName("dependency_policy")]
        public DependencyPolicyDocument? DependencyPolicy { get; init; }

        [JsonPropertyName("change_shape")]
        public ChangeShapeDocument? ChangeShape { get; init; }

        [JsonPropertyName("decision")]
        public DecisionDocument? Decision { get; init; }
    }

    private sealed class PathPolicyDocument
    {
        [JsonPropertyName("path_case")]
        public string? PathCase { get; init; }

        [JsonPropertyName("allowed_path_prefixes")]
        public IReadOnlyList<string>? AllowedPathPrefixes { get; init; }

        [JsonPropertyName("protected_path_prefixes")]
        public IReadOnlyList<string>? ProtectedPathPrefixes { get; init; }

        [JsonPropertyName("outside_allowed_action")]
        public string? OutsideAllowedAction { get; init; }

        [JsonPropertyName("protected_path_action")]
        public string? ProtectedPathAction { get; init; }
    }

    private sealed class ChangeBudgetDocument
    {
        [JsonPropertyName("max_changed_files")]
        public int? MaxChangedFiles { get; init; }

        [JsonPropertyName("max_total_additions")]
        public int? MaxTotalAdditions { get; init; }

        [JsonPropertyName("max_total_deletions")]
        public int? MaxTotalDeletions { get; init; }

        [JsonPropertyName("max_file_additions")]
        public int? MaxFileAdditions { get; init; }

        [JsonPropertyName("max_file_deletions")]
        public int? MaxFileDeletions { get; init; }

        [JsonPropertyName("max_renames")]
        public int? MaxRenames { get; init; }
    }

    private sealed class DependencyPolicyDocument
    {
        [JsonPropertyName("manifest_paths")]
        public IReadOnlyList<string>? ManifestPaths { get; init; }

        [JsonPropertyName("lockfile_paths")]
        public IReadOnlyList<string>? LockfilePaths { get; init; }

        [JsonPropertyName("manifest_without_lockfile_action")]
        public string? ManifestWithoutLockfileAction { get; init; }

        [JsonPropertyName("lockfile_without_manifest_action")]
        public string? LockfileWithoutManifestAction { get; init; }

        [JsonPropertyName("new_dependency_action")]
        public string? NewDependencyAction { get; init; }
    }

    private sealed class ChangeShapeDocument
    {
        [JsonPropertyName("allow_rename_with_content_change")]
        public bool AllowRenameWithContentChange { get; init; }

        [JsonPropertyName("allow_delete_without_replacement")]
        public bool AllowDeleteWithoutReplacement { get; init; }

        [JsonPropertyName("generated_path_prefixes")]
        public IReadOnlyList<string>? GeneratedPathPrefixes { get; init; }

        [JsonPropertyName("generated_path_action")]
        public string? GeneratedPathAction { get; init; }

        [JsonPropertyName("mixed_feature_and_refactor_action")]
        public string? MixedFeatureAndRefactorAction { get; init; }

        [JsonPropertyName("require_tests_for_source_changes")]
        public bool RequireTestsForSourceChanges { get; init; }

        [JsonPropertyName("source_path_prefixes")]
        public IReadOnlyList<string>? SourcePathPrefixes { get; init; }

        [JsonPropertyName("test_path_prefixes")]
        public IReadOnlyList<string>? TestPathPrefixes { get; init; }

        [JsonPropertyName("missing_tests_action")]
        public string? MissingTestsAction { get; init; }
    }

    private sealed class DecisionDocument
    {
        [JsonPropertyName("fail_closed")]
        public bool FailClosed { get; init; }

        [JsonPropertyName("default_outcome")]
        public string? DefaultOutcome { get; init; }

        [JsonPropertyName("review_is_passing")]
        public bool ReviewIsPassing { get; init; }

        [JsonPropertyName("emit_evidence")]
        public bool EmitEvidence { get; init; }
    }
}
