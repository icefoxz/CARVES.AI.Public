using System.Globalization;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenWrapperPolicyInvariantManifestService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly string[] GlobalForbiddenTransforms =
    [
        "do not rewrite hard boundaries into opaque shortcodes",
        "do not merge scope, sandbox, and approval constraints into generic safety prose",
        "do not demote system-role policy into user-content wording",
        "do not remove request-kind-specific gating when deduplicating repeated wrapper text",
        "do not trade token savings for weaker salience on stop conditions, budget, or source-grounding rules"
    ];

    private static readonly string[] GlobalValidatorOutputs =
    [
        "schema_validity_report",
        "wrapper_invariant_coverage_report",
        "semantic_preservation_report",
        "salience_preservation_report",
        "priority_preservation_report",
        "manual_review_queue"
    ];

    private readonly ControlPlanePaths paths;

    public RuntimeTokenWrapperPolicyInvariantManifestService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenWrapperPolicyInvariantManifestResult Persist(RuntimeTokenWrapperPolicyInventoryResult inventoryResult)
    {
        return Persist(paths, inventoryResult, inventoryResult.ResultDate);
    }

    internal static RuntimeTokenWrapperPolicyInvariantManifestResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperPolicyInventoryResult inventoryResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(inventoryResult, resultDate);

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var surfaceManifests = inventoryResult.TopWrapperSurfaces
            .Select(BuildSurfaceManifest)
            .OrderByDescending(item => item.ShareP95)
            .ThenBy(item => item.ManifestId, StringComparer.Ordinal)
            .ToArray();

        var result = new RuntimeTokenWrapperPolicyInvariantManifestResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = inventoryResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            InventoryMarkdownArtifactPath = inventoryResult.MarkdownArtifactPath,
            InventoryJsonArtifactPath = inventoryResult.JsonArtifactPath,
            TrustLineClassification = inventoryResult.TrustLineClassification,
            Phase11WrapperInvariantManifestMayReferenceThisLine = true,
            Phase10Decision = inventoryResult.Phase10Decision,
            Phase10NextTrack = inventoryResult.Phase10NextTrack,
            RequiredNextGate = "wrapper_offline_validator",
            Phase12WrapperCandidateAllowed = false,
            RequestKindsCovered = inventoryResult.RequestKindsCovered,
            CoverageLimitations = inventoryResult.CoverageLimitations,
            SurfaceManifests = surfaceManifests,
            GlobalForbiddenTransforms = GlobalForbiddenTransforms,
            RequiredValidatorOutputs = GlobalValidatorOutputs,
            WhatMustNotHappenNext =
            [
                "do not enable runtime shadow execution",
                "do not start active canary",
                "do not replace the main renderer",
                "do not start Phase 1.2 wrapper candidate work before offline validator coverage passes",
                "do not use lossy paraphrase or shortcodes on policy-critical wrapper text"
            ],
            Notes = BuildNotes(inventoryResult, surfaceManifests),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenWrapperPolicyInvariantManifestResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 1.1-W Wrapper Policy Invariant Manifest");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Wrapper invariant manifest may reference this line: `{(result.Phase11WrapperInvariantManifestMayReferenceThisLine ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 decision: `{result.Phase10Decision}`");
        builder.AppendLine($"- Phase 1.0 next track: `{result.Phase10NextTrack}`");
        builder.AppendLine($"- Required next gate: `{result.RequiredNextGate}`");
        builder.AppendLine($"- Phase 1.2 wrapper candidate allowed: `{(result.Phase12WrapperCandidateAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Inventory markdown artifact: `{result.InventoryMarkdownArtifactPath}`");
        builder.AppendLine($"- Inventory json artifact: `{result.InventoryJsonArtifactPath}`");
        builder.AppendLine();

        builder.AppendLine("## Covered Wrapper Surfaces");
        builder.AppendLine();
        builder.AppendLine("| Manifest Id | Inventory Id | Request Kind | Segment | Share P95 | Tokens P95 | Compression | Candidate Strategy |");
        builder.AppendLine("| --- | --- | --- | --- | ---: | ---: | --- | --- |");
        foreach (var surface in result.SurfaceManifests)
        {
            builder.AppendLine($"| `{surface.ManifestId}` | `{surface.InventoryId}` | `{surface.RequestKind}` | `{surface.SegmentKind}` | {FormatRatio(surface.ShareP95)} | {FormatNumber(surface.TokensP95)} | `{surface.CompressionAllowed}` | `{surface.RecommendedCandidateStrategy}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Global Forbidden Transforms");
        builder.AppendLine();
        foreach (var item in result.GlobalForbiddenTransforms)
        {
            builder.AppendLine($"- {item}");
        }

        builder.AppendLine();
        builder.AppendLine("## Required Validator Outputs");
        builder.AppendLine();
        foreach (var item in result.RequiredValidatorOutputs)
        {
            builder.AppendLine($"- `{item}`");
        }

        foreach (var surface in result.SurfaceManifests)
        {
            builder.AppendLine();
            builder.AppendLine($"## Surface `{surface.InventoryId}`");
            builder.AppendLine();
            builder.AppendLine($"- Source component: `{surface.SourceComponentPath}`");
            builder.AppendLine($"- Source anchor: `{surface.SourceAnchor}`");
            builder.AppendLine($"- Policy critical: `{(surface.PolicyCritical ? "yes" : "no")}`");
            builder.AppendLine($"- Manual review required: `{(surface.ManualReviewRequired ? "yes" : "no")}`");
            builder.AppendLine($"- Semantic preservation required: `{(surface.SemanticPreservationRequired ? "yes" : "no")}`");
            builder.AppendLine($"- Salience preservation required: `{(surface.SaliencePreservationRequired ? "yes" : "no")}`");
            builder.AppendLine($"- Priority preservation required: `{(surface.PriorityPreservationRequired ? "yes" : "no")}`");
            builder.AppendLine($"- Compression allowed: `{surface.CompressionAllowed}`");
            builder.AppendLine();
            builder.AppendLine("### Surface Forbidden Transforms");
            builder.AppendLine();
            foreach (var item in surface.ForbiddenTransforms)
            {
                builder.AppendLine($"- {item}");
            }

            builder.AppendLine();
            builder.AppendLine("### Invariant Items");
            builder.AppendLine();
            builder.AppendLine("| Invariant Id | Class | Compression | Manual Review | Clause Summary |");
            builder.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var invariant in surface.Invariants)
            {
                builder.AppendLine($"| `{invariant.InvariantId}` | `{invariant.InvariantClass}` | `{invariant.CompressionAllowed}` | `{(invariant.ManualReviewRequired ? "yes" : "no")}` | {EscapePipe(invariant.SourceClauseSummary)} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Coverage Limitations");
        builder.AppendLine();
        if (result.CoverageLimitations.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var limitation in result.CoverageLimitations)
            {
                builder.AppendLine($"- `{limitation}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## What Must Not Happen Next");
        builder.AppendLine();
        foreach (var item in result.WhatMustNotHappenNext)
        {
            builder.AppendLine($"- {item}");
        }

        if (result.Notes.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Notes");
            builder.AppendLine();
            foreach (var note in result.Notes)
            {
                builder.AppendLine($"- {note}");
            }
        }

        return builder.ToString();
    }

    private static RuntimeTokenWrapperPolicyInvariantSurfaceManifest BuildSurfaceManifest(RuntimeTokenWrapperPolicySurfaceSummary surface)
    {
        var sourceComponentPath = string.Equals(surface.RequestKind, "worker", StringComparison.Ordinal)
            && string.Equals(surface.PayloadPath, "$.instructions", StringComparison.Ordinal)
            ? "src/CARVES.Runtime.Application/AI/WorkerAiRequestFactory.cs"
            : "unknown";
        var sourceAnchor = string.Equals(surface.RequestKind, "worker", StringComparison.Ordinal)
            && string.Equals(surface.PayloadPath, "$.instructions", StringComparison.Ordinal)
            ? "WorkerAiRequestFactory.BuildInstructions"
            : "manual_review_required";
        var forbiddenTransforms = BuildSurfaceForbiddenTransforms(surface);

        return new RuntimeTokenWrapperPolicyInvariantSurfaceManifest
        {
            ManifestId = $"manifest:{surface.InventoryId}",
            InventoryId = surface.InventoryId,
            RequestKind = surface.RequestKind,
            SegmentKind = surface.SegmentKind,
            PayloadPath = surface.PayloadPath,
            Role = surface.Role,
            SerializationKind = surface.SerializationKind,
            Producer = surface.Producer,
            SourceComponentPath = sourceComponentPath,
            SourceAnchor = sourceAnchor,
            ShareP95 = surface.ShareP95,
            TokensP95 = surface.TokensP95,
            PolicyCritical = surface.PolicyCritical,
            ManualReviewRequired = true,
            SemanticPreservationRequired = true,
            SaliencePreservationRequired = true,
            PriorityPreservationRequired = true,
            CompressionAllowed = "structural_only",
            RecommendedInventoryAction = surface.RecommendedInventoryAction,
            RecommendedCandidateStrategy = ResolveCandidateStrategy(surface),
            ForbiddenTransforms = forbiddenTransforms,
            RequiredValidatorChecks =
            [
                "wrapper_invariant_coverage",
                "semantic_preservation",
                "salience_preservation",
                "priority_preservation",
                "manual_review_resolution"
            ],
            Invariants = BuildInvariantItems(surface),
        };
    }

    private static IReadOnlyList<RuntimeTokenWrapperPolicyInvariantItem> BuildInvariantItems(RuntimeTokenWrapperPolicySurfaceSummary surface)
    {
        if (string.Equals(surface.RequestKind, "worker", StringComparison.Ordinal)
            && string.Equals(surface.PayloadPath, "$.instructions", StringComparison.Ordinal)
            && string.Equals(surface.SegmentKind, "system", StringComparison.Ordinal))
        {
            return
            [
                Invariant(
                    "WRAP-WORKER-SCOPE-001",
                    "scope_boundary",
                    "Governed worker scope boundary",
                    surface,
                    "Stay inside scope, respect sandbox and approval policy, edit only allowed files.",
                    [
                        "do not collapse scope, sandbox, and approval boundaries into a generic safety sentence",
                        "do not remove the allowed-files restriction from the wrapper candidate"
                    ]),
                Invariant(
                    "WRAP-WORKER-ONEPASS-002",
                    "interaction_contract",
                    "One-pass bounded completion contract",
                    surface,
                    "Do not ask for confirmation when the task is already bounded; choose a reasonable default and complete the task in one pass.",
                    [
                        "do not paraphrase this into an open-ended planning prompt",
                        "do not weaken the bounded completion rule into optional advice"
                    ]),
                Invariant(
                    "WRAP-WORKER-SHELL-003",
                    "execution_modality",
                    "PowerShell execution modality and edit method",
                    surface,
                    "The delegated shell is Windows PowerShell; do not use bash-only edit syntax and prefer PowerShell-native file-edit/search commands.",
                    [
                        "do not replace the PowerShell modality with shell-agnostic wording",
                        "do not delete the explicit ban on bash-only edit syntax"
                    ]),
                Invariant(
                    "WRAP-WORKER-VALIDATION-004",
                    "validation_boundary",
                    "Formal validation stays with CARVES",
                    surface,
                    "Formal build/test validation is executed by CARVES after the worker returns; do not run routine restore/build/test inside delegated execution.",
                    [
                        "do not shorten this into a vague 'avoid heavy validation' rule",
                        "do not drop the ownership boundary between worker edits and CARVES validation"
                    ]),
                Invariant(
                    "WRAP-WORKER-BUDGET-005",
                    "budget_boundary",
                    "Patch and command budget remain hard limits",
                    surface,
                    "Stay within the declared execution budget and narrow the slice rather than force an over-budget patch.",
                    [
                        "do not remove numeric budget references from the candidate structure",
                        "do not demote budget overflow handling into a non-binding suggestion"
                    ]),
                Invariant(
                    "WRAP-WORKER-STOP-006",
                    "stop_conditions",
                    "Stop conditions remain hard preflight boundaries",
                    surface,
                    "Treat stop conditions as hard preflight boundaries.",
                    [
                        "do not merge stop conditions into generic caution text",
                        "do not encode stop conditions as opaque shorthand"
                    ]),
                Invariant(
                    "WRAP-WORKER-SOURCE-007",
                    "source_grounding",
                    "Source identifiers must stay concrete when grounding is required",
                    surface,
                    "Use only task IDs, artifact paths, and evidence identifiers that are explicitly present in context; do not invent or substitute source identifiers.",
                    [
                        "do not drop the anti-fabrication clause",
                        "do not weaken missing-source handling into inferred substitution"
                    ])
            ];
        }

        return
        [
            Invariant(
                $"WRAP-{surface.RequestKind.ToUpperInvariant()}-{surface.SegmentKind.ToUpperInvariant()}-001",
                "policy_surface",
                "Policy surface remains structural-only",
                surface,
                "This wrapper surface is policy-critical and may only be compacted through structural projection, not lossy paraphrase.",
                [
                    "do not convert the wrapper surface into symbolic shortcodes",
                    "do not remove request-kind scoping or role semantics"
                ])
        ];
    }

    private static RuntimeTokenWrapperPolicyInvariantItem Invariant(
        string invariantId,
        string invariantClass,
        string title,
        RuntimeTokenWrapperPolicySurfaceSummary surface,
        string sourceClauseSummary,
        IReadOnlyList<string> forbiddenTransforms)
    {
        return new RuntimeTokenWrapperPolicyInvariantItem
        {
            InvariantId = invariantId,
            InvariantClass = invariantClass,
            Title = title,
            SourceSegmentKind = surface.SegmentKind,
            SourcePayloadPath = surface.PayloadPath,
            SourceClauseSummary = sourceClauseSummary,
            SemanticPreservationRequired = true,
            SaliencePreservationRequired = true,
            PriorityPreservationRequired = true,
            CompressionAllowed = "structural_only",
            ManualReviewRequired = true,
            ForbiddenTransforms = forbiddenTransforms,
            RequiredValidatorChecks =
            [
                "semantic_preservation",
                "salience_preservation",
                "priority_preservation",
                "manual_review_resolution"
            ],
            Notes =
            [
                "Policy-critical wrapper clauses may be reordered only if relative priority is preserved and explicitly validated.",
                "Structural-only means dedupe, request-kind slicing, or projection; not lossy paraphrase."
            ]
        };
    }

    private static IReadOnlyList<string> BuildSurfaceForbiddenTransforms(RuntimeTokenWrapperPolicySurfaceSummary surface)
    {
        var items = new List<string>
        {
            "do not introduce symbolic shortcodes for policy-critical wrapper clauses",
            "do not remove role, payload-path, or request-kind identity from the candidate",
            "do not lower manual-review-required surfaces to automatic pass-only validation"
        };

        if (string.Equals(surface.RequestKind, "worker", StringComparison.Ordinal)
            && string.Equals(surface.PayloadPath, "$.instructions", StringComparison.Ordinal))
        {
            items.Add("do not compress worker shell modality into a provider-neutral sentence");
            items.Add("do not delete budget or stop-condition salience from worker instructions");
        }

        return items;
    }

    private static string ResolveCandidateStrategy(RuntimeTokenWrapperPolicySurfaceSummary surface)
    {
        if (string.Equals(surface.RecommendedInventoryAction, "dedupe_and_request_kind_slice_review", StringComparison.Ordinal))
        {
            return "dedupe_then_request_kind_slice";
        }

        if (string.Equals(surface.RecommendedInventoryAction, "invariant_first", StringComparison.Ordinal))
        {
            return "invariant_first_then_structural_projection";
        }

        return "manual_review_first";
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenWrapperPolicyInventoryResult inventoryResult,
        IReadOnlyList<RuntimeTokenWrapperPolicyInvariantSurfaceManifest> surfaceManifests)
    {
        var notes = new List<string>
        {
            "This manifest is still offline-only. It does not approve runtime shadow, active canary, or main-renderer replacement.",
            "Phase 1.2 wrapper candidate remains blocked until the offline wrapper validator is ready."
        };

        if (inventoryResult.RequestKindsCovered.Count == 1
            && string.Equals(inventoryResult.RequestKindsCovered[0], "worker", StringComparison.Ordinal))
        {
            notes.Add("Current invariant coverage is worker-only because the trusted cohort does not yet cover planner/reviewer/repair/retry wrapper surfaces.");
        }

        var topSurface = surfaceManifests
            .OrderByDescending(item => item.ShareP95)
            .ThenBy(item => item.ManifestId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (topSurface is not null)
        {
            notes.Add($"Current primary wrapper target remains `{topSurface.InventoryId}` at share_p95={FormatRatio(topSurface.ShareP95)}.");
        }

        return notes;
    }

    private static void ValidateInputs(RuntimeTokenWrapperPolicyInventoryResult inventoryResult, DateOnly resultDate)
    {
        if (inventoryResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Wrapper policy invariant manifest requires the inventory result date to match the requested result date.");
        }

        if (!inventoryResult.Phase11WrapperInventoryMayReferenceThisLine
            || !string.Equals(inventoryResult.TrustLineClassification, "recomputed_trusted_for_phase_1_target_decision", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper policy invariant manifest requires a trusted wrapper inventory line.");
        }

        if (!string.Equals(inventoryResult.Phase10Decision, "reprioritize_to_wrapper", StringComparison.Ordinal)
            || !string.Equals(inventoryResult.Phase10NextTrack, "wrapper_policy_shadow_offline", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper policy invariant manifest requires Phase 1.0 to select wrapper_policy_shadow_offline.");
        }

        if (inventoryResult.TopWrapperSurfaces.Count == 0)
        {
            throw new InvalidOperationException("Wrapper policy invariant manifest requires at least one wrapper surface in the trusted inventory.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-1-wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-1",
            $"wrapper-policy-invariant-manifest-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }

    private static string FormatRatio(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string EscapePipe(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
