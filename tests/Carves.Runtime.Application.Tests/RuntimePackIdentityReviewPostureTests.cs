using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimePackIdentityReviewPostureTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static readonly string[] RequiredFields =
    [
        "pack_id",
        "pack_family",
        "schema_version",
        "lifecycle_state",
        "review_posture",
        "review_evidence_refs",
        "truth_source_refs",
        "default_context_posture",
        "expansion_refs",
        "non_claims",
    ];

    [Fact]
    public void ReadModel_DeclaresRequiredIdentityFieldsAndClosedLines()
    {
        var model = LoadModel();

        Assert.Equal(1, model.SchemaVersion);
        Assert.Equal("CARD-922", model.CardId);
        Assert.Equal("T-CARD-922-001", model.TaskId);
        Assert.Equal("pack_identity_review_posture", model.ReadModelKind);
        Assert.Equal("repo_local_read_only_projection", model.AuthorityPosture);
        Assert.Equal(RequiredFields, model.RequiredIdentityFields);
        Assert.Contains("not_a_runtime_inspect_or_api_surface", model.ClosedLines);
        Assert.Contains("not_a_pack_registry", model.ClosedLines);
        Assert.Contains("not_rollout_assignment", model.ClosedLines);
        Assert.Contains("not_automatic_activation", model.ClosedLines);
        Assert.Contains("not_multi_pack_orchestration", model.ClosedLines);
        Assert.Contains("not_context_budget_calculation", model.ClosedLines);
        Assert.Contains("not_startup_read_path", model.ClosedLines);
    }

    [Fact]
    public void ReadModel_ClassifiesPackFamiliesWithEvidenceBackedReviewPosture()
    {
        var model = LoadModel();
        var packIds = model.Entries.Select(entry => entry.PackId).ToArray();

        Assert.Equal(packIds.Length, packIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(model.Entries, entry => entry.PackFamily == "context_pack");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "runtime_pack");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "release_pack");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "external_resource_pack");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "proof_bundle");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "runtime_state_package");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "policy_package");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "trial_starter_pack");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "instruction_pack");

        foreach (var entry in model.Entries)
        {
            var errors = ValidateEntry(model, entry);
            Assert.Empty(errors);
            Assert.NotEmpty(entry.TruthSourceRefs);
            Assert.NotEmpty(entry.ExpansionRefs);
            Assert.NotEmpty(entry.NonClaims);
        }
    }

    [Fact]
    public void ReadModel_KeepsDefaultContextPostureCandidateOnly()
    {
        var model = LoadModel();

        Assert.DoesNotContain(model.Entries, entry => entry.DefaultContextPosture == "eligible_candidate");
        Assert.Contains(model.Entries, entry => entry.PackFamily == "context_pack" && entry.DefaultContextPosture == "pointer_only");
        Assert.Contains(model.Entries, entry => entry.DefaultContextPosture == "blocked");
        Assert.Contains(model.Entries, entry => entry.DefaultContextPosture == "manual_review_required");
        Assert.All(model.Entries, entry => Assert.DoesNotContain("auto_activate", entry.NonClaims, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_RejectsSelfCertifiedOrUnknownReviewPosture()
    {
        var model = LoadModel();
        var sample = model.Entries[0];

        var selfCertified = sample with { ReviewEvidenceRefs = [] };
        Assert.Contains("review_evidence_required", ValidateEntry(model, selfCertified));

        var unknownReview = sample with { ReviewPosture = "approved_by_assumption" };
        Assert.Contains("unknown_review_posture", ValidateEntry(model, unknownReview));
    }

    [Fact]
    public void Validation_BlocksInvalidDefaultContextClaims()
    {
        var model = LoadModel();
        var sample = model.Entries[0] with { DefaultContextPosture = "eligible_candidate" };

        var draft = sample with { LifecycleState = "draft" };
        Assert.Contains("default_context_ineligible_lifecycle", ValidateEntry(model, draft));

        var deprecated = sample with { LifecycleState = "deprecated" };
        Assert.Contains("default_context_ineligible_lifecycle", ValidateEntry(model, deprecated));

        var stale = sample with { ReviewPosture = "stale" };
        Assert.Contains("default_context_ineligible_review", ValidateEntry(model, stale));

        var unreviewed = sample with { ReviewPosture = "unreviewed" };
        Assert.Contains("default_context_ineligible_review", ValidateEntry(model, unreviewed));

        var needsReview = sample with { ReviewPosture = "needs_review" };
        Assert.Contains("default_context_ineligible_review", ValidateEntry(model, needsReview));
    }

    [Fact]
    public void ReadModel_DefersContentRevisionAndSupersessionChains()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ModelPath()));

        Assert.False(ContainsProperty(document.RootElement, "pack_revision"));
        Assert.False(ContainsProperty(document.RootElement, "content_revision"));
        Assert.False(ContainsProperty(document.RootElement, "supersedes"));
        Assert.False(ContainsProperty(document.RootElement, "superseded_by"));
        Assert.False(ContainsProperty(document.RootElement, "compatibility_matrix"));
    }

    private static IReadOnlyList<string> ValidateEntry(PackIdentityReadModel model, PackIdentityEntry entry)
    {
        var errors = new List<string>();
        Require(entry.PackId, "missing_pack_id", errors);
        Require(entry.PackFamily, "missing_pack_family", errors);
        Require(entry.LifecycleState, "missing_lifecycle_state", errors);
        Require(entry.ReviewPosture, "missing_review_posture", errors);
        Require(entry.DefaultContextPosture, "missing_default_context_posture", errors);

        if (entry.SchemaVersion != 1)
        {
            errors.Add("unsupported_schema_version");
        }

        if (!model.AllowedValues.PackFamily.Contains(entry.PackFamily, StringComparer.Ordinal))
        {
            errors.Add("unknown_pack_family");
        }

        if (!model.AllowedValues.LifecycleState.Contains(entry.LifecycleState, StringComparer.Ordinal))
        {
            errors.Add("unknown_lifecycle_state");
        }

        if (!model.AllowedValues.ReviewPosture.Contains(entry.ReviewPosture, StringComparer.Ordinal))
        {
            errors.Add("unknown_review_posture");
        }

        if (!model.AllowedValues.DefaultContextPosture.Contains(entry.DefaultContextPosture, StringComparer.Ordinal))
        {
            errors.Add("unknown_default_context_posture");
        }

        if (RequiresEvidence(entry.ReviewPosture))
        {
            if (entry.ReviewEvidenceRefs.Length == 0)
            {
                errors.Add("review_evidence_required");
            }

            foreach (var evidence in entry.ReviewEvidenceRefs)
            {
                Require(evidence.Ref, "review_evidence_ref_required", errors);
                Require(evidence.Scope, "review_evidence_scope_required", errors);
                Require(evidence.Reason, "review_evidence_reason_required", errors);
            }
        }

        if (entry.TruthSourceRefs.Length == 0)
        {
            errors.Add("truth_source_refs_required");
        }

        if (entry.ExpansionRefs.Length == 0)
        {
            errors.Add("expansion_refs_required");
        }

        if (entry.NonClaims.Length == 0)
        {
            errors.Add("non_claims_required");
        }

        if (entry.DefaultContextPosture == "eligible_candidate")
        {
            if (entry.LifecycleState != "active")
            {
                errors.Add("default_context_ineligible_lifecycle");
            }

            if (entry.ReviewPosture != "reviewed")
            {
                errors.Add("default_context_ineligible_review");
            }
        }

        return errors;
    }

    private static bool RequiresEvidence(string reviewPosture)
    {
        return reviewPosture is "reviewed" or "stale" or "needs_review";
    }

    private static void Require(string value, string errorCode, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(errorCode);
        }
    }

    private static bool ContainsProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                {
                    return true;
                }

                if (ContainsProperty(property.Value, propertyName))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsProperty(item, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static PackIdentityReadModel LoadModel()
    {
        using var stream = File.OpenRead(ModelPath());
        return JsonSerializer.Deserialize<PackIdentityReadModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime pack identity review posture model.");
    }

    private static string ModelPath()
    {
        return Path.Combine(RepoRoot(), "docs", "runtime", "runtime-pack-identity-review-posture.json");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && Directory.Exists(Path.Combine(directory.FullName, ".ai")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to find repository root.");
    }

    private sealed record PackIdentityReadModel
    {
        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("card_id")]
        public string CardId { get; init; } = "";

        [JsonPropertyName("task_id")]
        public string TaskId { get; init; } = "";

        [JsonPropertyName("read_model_kind")]
        public string ReadModelKind { get; init; } = "";

        [JsonPropertyName("authority_posture")]
        public string AuthorityPosture { get; init; } = "";

        [JsonPropertyName("required_identity_fields")]
        public string[] RequiredIdentityFields { get; init; } = [];

        [JsonPropertyName("allowed_values")]
        public PackIdentityAllowedValues AllowedValues { get; init; } = new();

        [JsonPropertyName("closed_lines")]
        public string[] ClosedLines { get; init; } = [];

        [JsonPropertyName("entries")]
        public PackIdentityEntry[] Entries { get; init; } = [];
    }

    private sealed record PackIdentityAllowedValues
    {
        [JsonPropertyName("pack_family")]
        public string[] PackFamily { get; init; } = [];

        [JsonPropertyName("lifecycle_state")]
        public string[] LifecycleState { get; init; } = [];

        [JsonPropertyName("review_posture")]
        public string[] ReviewPosture { get; init; } = [];

        [JsonPropertyName("default_context_posture")]
        public string[] DefaultContextPosture { get; init; } = [];
    }

    private sealed record PackIdentityEntry
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("pack_family")]
        public string PackFamily { get; init; } = "";

        [JsonPropertyName("schema_version")]
        public int SchemaVersion { get; init; }

        [JsonPropertyName("lifecycle_state")]
        public string LifecycleState { get; init; } = "";

        [JsonPropertyName("review_posture")]
        public string ReviewPosture { get; init; } = "";

        [JsonPropertyName("review_evidence_refs")]
        public PackIdentityReviewEvidence[] ReviewEvidenceRefs { get; init; } = [];

        [JsonPropertyName("truth_source_refs")]
        public string[] TruthSourceRefs { get; init; } = [];

        [JsonPropertyName("default_context_posture")]
        public string DefaultContextPosture { get; init; } = "";

        [JsonPropertyName("expansion_refs")]
        public string[] ExpansionRefs { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }

    private sealed record PackIdentityReviewEvidence
    {
        [JsonPropertyName("ref")]
        public string Ref { get; init; } = "";

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; init; } = "";
    }
}
