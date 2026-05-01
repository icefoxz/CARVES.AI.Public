using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Tests;

public sealed class RuntimeContextPackBudgetReadbackTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private static readonly string[] RequiredContentKinds =
    [
        "summary",
        "governance_metadata",
        "acceptance_task_truth",
        "source_excerpt",
        "large_file_window",
    ];

    [Fact]
    public void Readback_DeclaresConservativeNonRuntimeEstimator()
    {
        var model = LoadModel();

        Assert.Equal(1, model.SchemaVersion);
        Assert.Equal("CARD-924", model.CardId);
        Assert.Equal("T-CARD-924-001", model.TaskId);
        Assert.Equal("context_pack_budget_readback", model.ReadModelKind);
        Assert.Equal("repo_local_read_only_projection", model.AuthorityPosture);
        Assert.Equal("docs/runtime/runtime-pack-identity-review-posture.json", model.IdentityModelRef);
        Assert.Equal("context_pack_conservative_estimator_v1", model.Estimator.EstimatorId);
        Assert.Equal("conservative_estimate", model.Estimator.EstimatorPosture);
        Assert.False(model.Estimator.TokenizerExact);
        Assert.Equal("not_tokenizer_exact", model.Estimator.PrecisionClaim);
        Assert.False(model.Estimator.AutomaticTrimming);
        Assert.False(model.Estimator.PhaseSpecificSelection);
        Assert.False(model.Estimator.StartupReadPathChanged);
        Assert.False(model.Estimator.InspectApiSurfaceAdded);
        Assert.False(model.Estimator.DefaultContextActivation);
        Assert.Contains("not_automatic_trimming", model.ClosedLines);
        Assert.Contains("not_phase_specific_selection", model.ClosedLines);
        Assert.Contains("not_tokenizer_exact_accounting", model.ClosedLines);
        Assert.Contains("not_a_runtime_inspect_or_api_surface", model.ClosedLines);
        Assert.Contains("not_startup_read_path", model.ClosedLines);
    }

    [Fact]
    public void Readback_DistinguishesRequiredContentKinds()
    {
        var model = LoadModel();
        var configuredKinds = model.ContentKindEstimates.Select(item => item.ContentKind).ToArray();

        foreach (var requiredKind in RequiredContentKinds)
        {
            Assert.Contains(requiredKind, configuredKinds);
        }

        Assert.All(model.ContentKindEstimates, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.EstimatorMethod));
            Assert.Equal("conservative_not_exact", item.PrecisionClaim);
        });
    }

    [Fact]
    public void Readback_RecordsIncludedAndOmittedSectionsWithExpansionRefs()
    {
        var model = LoadModel();
        var readback = Assert.Single(model.Readbacks);
        var errors = ValidateReadback(model, readback);

        Assert.Empty(errors);
        Assert.Equal("runtime.context_pack", readback.PackId);
        Assert.Equal("context_pack", readback.PackFamily);
        Assert.Equal("reviewed", readback.ReviewPosture);
        Assert.Equal("readback_available", readback.BudgetPosture);
        Assert.True(readback.EstimatedIncludedTokens > 0);
        Assert.True(readback.EstimatedOmittedTokens > readback.EstimatedIncludedTokens);
        Assert.Contains(readback.IncludedSections, section => section.ContentKind == "acceptance_task_truth");
        Assert.Contains(readback.IncludedSections, section => section.ContentKind == "governance_metadata");
        Assert.Contains(readback.OmittedSections, section => section.ContentKind == "large_file_window");
        Assert.All(readback.OmittedSections, section => Assert.NotEmpty(section.ExpansionRefs));
        Assert.NotEmpty(readback.LargestContributors);
    }

    [Fact]
    public void Readback_SeparatesBudgetPostureFromReviewPosture()
    {
        var model = LoadModel();

        Assert.Contains("reviewed_content_can_be_over_budget", model.SeparationRules);
        Assert.Contains("within_budget_content_can_be_unreviewed", model.SeparationRules);
        Assert.Contains("budget_success_does_not_grant_default_context", model.SeparationRules);
        Assert.Contains("budget_readback_does_not_change_review_posture", model.SeparationRules);

        var readback = Assert.Single(model.Readbacks);
        Assert.NotEqual(readback.ReviewPosture, readback.BudgetPosture);
        Assert.Contains("does_not_grant_default_context_eligibility", readback.NonClaims);
    }

    [Fact]
    public void Validation_RejectsMissingBudgetReadbackOrExpansionRefs()
    {
        var model = LoadModel();
        var sample = Assert.Single(model.Readbacks);

        var missingBudget = sample with { EstimatedIncludedTokens = 0 };
        Assert.Contains("budget_readback_required", ValidateReadback(model, missingBudget));

        var missingOmittedExpansion = sample with
        {
            OmittedSections =
            [
                sample.OmittedSections[0] with { ExpansionRefs = [] },
            ],
        };
        Assert.Contains("omitted_expansion_refs_required", ValidateReadback(model, missingOmittedExpansion));
    }

    [Fact]
    public void Validation_RejectsTokenizerExactClaimsAndAutomaticTrimming()
    {
        var model = LoadModel() with
        {
            Estimator = LoadModel().Estimator with
            {
                TokenizerExact = true,
                PrecisionClaim = "tokenizer_exact",
                AutomaticTrimming = true,
            },
        };

        var errors = ValidateModel(model);

        Assert.Contains("tokenizer_exact_claim_forbidden", errors);
        Assert.Contains("automatic_trimming_forbidden", errors);
    }

    [Fact]
    public void Readback_DefersPhaseSpecificSelectionAndStartupChanges()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ModelPath()));

        Assert.False(ContainsPropertyValue(document.RootElement, "automatic_phase_selection"));
        Assert.False(ContainsPropertyValue(document.RootElement, "startup_read_path_added"));
        Assert.False(ContainsPropertyValue(document.RootElement, "runtime_inspect_surface_added"));
        Assert.False(ContainsPropertyValue(document.RootElement, "runtime_api_surface_added"));
    }

    private static IReadOnlyList<string> ValidateModel(ContextPackBudgetReadbackModel model)
    {
        var errors = new List<string>();

        if (model.Estimator.TokenizerExact || string.Equals(model.Estimator.PrecisionClaim, "tokenizer_exact", StringComparison.Ordinal))
        {
            errors.Add("tokenizer_exact_claim_forbidden");
        }

        if (model.Estimator.AutomaticTrimming)
        {
            errors.Add("automatic_trimming_forbidden");
        }

        if (model.Estimator.PhaseSpecificSelection)
        {
            errors.Add("phase_specific_selection_forbidden");
        }

        if (model.Estimator.StartupReadPathChanged)
        {
            errors.Add("startup_read_path_change_forbidden");
        }

        if (model.Estimator.InspectApiSurfaceAdded)
        {
            errors.Add("inspect_api_surface_forbidden");
        }

        foreach (var readback in model.Readbacks)
        {
            errors.AddRange(ValidateReadback(model, readback));
        }

        return errors;
    }

    private static IReadOnlyList<string> ValidateReadback(ContextPackBudgetReadbackModel model, ContextPackBudgetReadback readback)
    {
        var errors = new List<string>();
        Require(readback.PackId, "missing_pack_id", errors);
        Require(readback.PackFamily, "missing_pack_family", errors);
        Require(readback.IdentityRef, "missing_identity_ref", errors);
        Require(readback.ReviewPosture, "missing_review_posture", errors);
        Require(readback.BudgetPosture, "missing_budget_posture", errors);

        if (readback.EstimatedIncludedTokens <= 0)
        {
            errors.Add("budget_readback_required");
        }

        if (readback.EstimatedOmittedTokens < 0)
        {
            errors.Add("invalid_omitted_token_estimate");
        }

        if (string.Equals(readback.EstimatePrecision, "tokenizer_exact", StringComparison.Ordinal))
        {
            errors.Add("tokenizer_exact_claim_forbidden");
        }

        if (readback.IncludedSections.Length == 0)
        {
            errors.Add("included_sections_required");
        }

        if (readback.OmittedSections.Length == 0)
        {
            errors.Add("omitted_sections_required");
        }

        if (readback.ExpansionRefs.Length == 0)
        {
            errors.Add("readback_expansion_refs_required");
        }

        if (readback.NonClaims.Length == 0)
        {
            errors.Add("non_claims_required");
        }

        foreach (var section in readback.IncludedSections)
        {
            Require(section.SectionId, "included_section_id_required", errors);
            Require(section.ContentKind, "included_content_kind_required", errors);
            Require(section.PreserveReason, "included_preserve_reason_required", errors);
            if (section.EstimatedTokens <= 0)
            {
                errors.Add("included_estimate_required");
            }
        }

        foreach (var section in readback.OmittedSections)
        {
            Require(section.SectionId, "omitted_section_id_required", errors);
            Require(section.ContentKind, "omitted_content_kind_required", errors);
            Require(section.TrimReason, "omitted_trim_reason_required", errors);
            if (section.EstimatedTokens <= 0)
            {
                errors.Add("omitted_estimate_required");
            }

            if (section.ExpansionRefs.Length == 0)
            {
                errors.Add("omitted_expansion_refs_required");
            }
        }

        var configuredKinds = model.ContentKindEstimates.Select(item => item.ContentKind).ToHashSet(StringComparer.Ordinal);
        foreach (var kind in readback.IncludedSections.Select(section => section.ContentKind)
                     .Concat(readback.OmittedSections.Select(section => section.ContentKind)))
        {
            if (!configuredKinds.Contains(kind))
            {
                errors.Add("unknown_content_kind");
            }
        }

        return errors;
    }

    private static void Require(string value, string errorCode, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(errorCode);
        }
    }

    private static bool ContainsPropertyValue(JsonElement element, string value)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return string.Equals(element.GetString(), value, StringComparison.Ordinal);
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (ContainsPropertyValue(property.Value, value))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsPropertyValue(item, value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ContextPackBudgetReadbackModel LoadModel()
    {
        using var stream = File.OpenRead(ModelPath());
        return JsonSerializer.Deserialize<ContextPackBudgetReadbackModel>(stream, JsonOptions)
               ?? throw new InvalidOperationException("Unable to read Runtime Context Pack budget readback model.");
    }

    private static string ModelPath()
    {
        return Path.Combine(RepoRoot(), "docs", "runtime", "runtime-context-pack-budget-readback.json");
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

    private sealed record ContextPackBudgetReadbackModel
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

        [JsonPropertyName("identity_model_ref")]
        public string IdentityModelRef { get; init; } = "";

        [JsonPropertyName("estimator")]
        public ContextPackEstimator Estimator { get; init; } = new();

        [JsonPropertyName("required_readback_fields")]
        public string[] RequiredReadbackFields { get; init; } = [];

        [JsonPropertyName("content_kind_estimates")]
        public ContextPackContentKindEstimate[] ContentKindEstimates { get; init; } = [];

        [JsonPropertyName("separation_rules")]
        public string[] SeparationRules { get; init; } = [];

        [JsonPropertyName("closed_lines")]
        public string[] ClosedLines { get; init; } = [];

        [JsonPropertyName("readbacks")]
        public ContextPackBudgetReadback[] Readbacks { get; init; } = [];
    }

    private sealed record ContextPackEstimator
    {
        [JsonPropertyName("estimator_id")]
        public string EstimatorId { get; init; } = "";

        [JsonPropertyName("estimator_posture")]
        public string EstimatorPosture { get; init; } = "";

        [JsonPropertyName("tokenizer_exact")]
        public bool TokenizerExact { get; init; }

        [JsonPropertyName("precision_claim")]
        public string PrecisionClaim { get; init; } = "";

        [JsonPropertyName("budget_unit")]
        public string BudgetUnit { get; init; } = "";

        [JsonPropertyName("automatic_trimming")]
        public bool AutomaticTrimming { get; init; }

        [JsonPropertyName("phase_specific_selection")]
        public bool PhaseSpecificSelection { get; init; }

        [JsonPropertyName("startup_read_path_changed")]
        public bool StartupReadPathChanged { get; init; }

        [JsonPropertyName("inspect_api_surface_added")]
        public bool InspectApiSurfaceAdded { get; init; }

        [JsonPropertyName("default_context_activation")]
        public bool DefaultContextActivation { get; init; }
    }

    private sealed record ContextPackContentKindEstimate
    {
        [JsonPropertyName("content_kind")]
        public string ContentKind { get; init; } = "";

        [JsonPropertyName("estimator_method")]
        public string EstimatorMethod { get; init; } = "";

        [JsonPropertyName("precision_claim")]
        public string PrecisionClaim { get; init; } = "";
    }

    private sealed record ContextPackBudgetReadback
    {
        [JsonPropertyName("pack_id")]
        public string PackId { get; init; } = "";

        [JsonPropertyName("pack_family")]
        public string PackFamily { get; init; } = "";

        [JsonPropertyName("identity_ref")]
        public string IdentityRef { get; init; } = "";

        [JsonPropertyName("review_posture")]
        public string ReviewPosture { get; init; } = "";

        [JsonPropertyName("budget_posture")]
        public string BudgetPosture { get; init; } = "";

        [JsonPropertyName("budget_limit_estimated_tokens")]
        public int BudgetLimitEstimatedTokens { get; init; }

        [JsonPropertyName("estimated_included_tokens")]
        public int EstimatedIncludedTokens { get; init; }

        [JsonPropertyName("estimated_omitted_tokens")]
        public int EstimatedOmittedTokens { get; init; }

        [JsonPropertyName("estimate_precision")]
        public string EstimatePrecision { get; init; } = "";

        [JsonPropertyName("included_sections")]
        public ContextPackIncludedSection[] IncludedSections { get; init; } = [];

        [JsonPropertyName("omitted_sections")]
        public ContextPackOmittedSection[] OmittedSections { get; init; } = [];

        [JsonPropertyName("largest_contributors")]
        public ContextPackLargestContributor[] LargestContributors { get; init; } = [];

        [JsonPropertyName("expansion_refs")]
        public string[] ExpansionRefs { get; init; } = [];

        [JsonPropertyName("non_claims")]
        public string[] NonClaims { get; init; } = [];
    }

    private sealed record ContextPackIncludedSection
    {
        [JsonPropertyName("section_id")]
        public string SectionId { get; init; } = "";

        [JsonPropertyName("content_kind")]
        public string ContentKind { get; init; } = "";

        [JsonPropertyName("estimated_tokens")]
        public int EstimatedTokens { get; init; }

        [JsonPropertyName("source_refs")]
        public string[] SourceRefs { get; init; } = [];

        [JsonPropertyName("preserve_reason")]
        public string PreserveReason { get; init; } = "";
    }

    private sealed record ContextPackOmittedSection
    {
        [JsonPropertyName("section_id")]
        public string SectionId { get; init; } = "";

        [JsonPropertyName("content_kind")]
        public string ContentKind { get; init; } = "";

        [JsonPropertyName("estimated_tokens")]
        public int EstimatedTokens { get; init; }

        [JsonPropertyName("source_refs")]
        public string[] SourceRefs { get; init; } = [];

        [JsonPropertyName("trim_reason")]
        public string TrimReason { get; init; } = "";

        [JsonPropertyName("expansion_refs")]
        public string[] ExpansionRefs { get; init; } = [];
    }

    private sealed record ContextPackLargestContributor
    {
        [JsonPropertyName("section_id")]
        public string SectionId { get; init; } = "";

        [JsonPropertyName("estimated_tokens")]
        public int EstimatedTokens { get; init; }

        [JsonPropertyName("included")]
        public bool Included { get; init; }
    }
}
