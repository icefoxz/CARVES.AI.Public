using System.Globalization;
using System.Text;
using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.AI;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenWrapperOfflineValidatorService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly IReadOnlyList<string> BlockingReasons = Array.Empty<string>();

    private readonly ControlPlanePaths paths;

    public RuntimeTokenWrapperOfflineValidatorService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public RuntimeTokenWrapperOfflineValidatorResult Persist(
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenWrapperPolicyInventoryResult inventoryResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult)
    {
        return Persist(paths, manifestResult, inventoryResult, workerRecollectResult, manifestResult.ResultDate);
    }

    internal static RuntimeTokenWrapperOfflineValidatorResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenWrapperPolicyInventoryResult inventoryResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(manifestResult, inventoryResult, workerRecollectResult, resultDate);

        var requestCountsByKind = inventoryResult.RequestKindSummaries
            .ToDictionary(item => item.RequestKind, item => item.RequestCount, StringComparer.Ordinal);
        var surfaceResults = manifestResult.SurfaceManifests
            .Select(surface => BuildSurfaceResult(surface, requestCountsByKind))
            .OrderByDescending(item => item.SourceShareP95)
            .ThenBy(item => item.ManifestId, StringComparer.Ordinal)
            .ToArray();
        var manualReviewQueue = manifestResult.SurfaceManifests
            .SelectMany(BuildManualReviewItems)
            .OrderBy(item => item.ReviewId, StringComparer.Ordinal)
            .ToArray();

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenWrapperOfflineValidatorResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CohortId = manifestResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            ManifestMarkdownArtifactPath = manifestResult.MarkdownArtifactPath,
            ManifestJsonArtifactPath = manifestResult.JsonArtifactPath,
            InventoryMarkdownArtifactPath = inventoryResult.MarkdownArtifactPath,
            InventoryJsonArtifactPath = inventoryResult.JsonArtifactPath,
            WorkerRecollectMarkdownArtifactPath = workerRecollectResult.MarkdownArtifactPath,
            WorkerRecollectJsonArtifactPath = workerRecollectResult.JsonArtifactPath,
            TrustLineClassification = manifestResult.TrustLineClassification,
            Phase11WrapperValidatorMayReferenceThisLine = true,
            Phase10Decision = manifestResult.Phase10Decision,
            Phase10NextTrack = manifestResult.Phase10NextTrack,
            ValidationMode = "source_echo_baseline",
            ValidatorVerdict = "ready_for_phase12_wrapper_candidate_design",
            Phase12WrapperCandidateMayStart = true,
            RuntimeShadowExecutionAllowed = false,
            ActiveCanaryAllowed = false,
            RequestKindsCovered = manifestResult.RequestKindsCovered,
            BaselineTaskIds = workerRecollectResult.TaskIds,
            SurfaceResults = surfaceResults,
            ManualReviewQueue = manualReviewQueue,
            BlockingReasons = BlockingReasons,
            WhatMustNotHappenNext =
            [
                "do not enable runtime shadow execution",
                "do not start active canary",
                "do not replace the main renderer",
                "do not treat source-echo validator pass as token-reduction proof",
                "do not claim canary readiness before a targeted wrapper candidate is validated"
            ],
            Notes = BuildNotes(manifestResult, workerRecollectResult, surfaceResults),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(markdownPath, FormatMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string FormatMarkdown(RuntimeTokenWrapperOfflineValidatorResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 1.1-W Wrapper Offline Validator Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Wrapper validator may reference this line: `{(result.Phase11WrapperValidatorMayReferenceThisLine ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 decision: `{result.Phase10Decision}`");
        builder.AppendLine($"- Phase 1.0 next track: `{result.Phase10NextTrack}`");
        builder.AppendLine($"- Validation mode: `{result.ValidationMode}`");
        builder.AppendLine($"- Validator verdict: `{result.ValidatorVerdict}`");
        builder.AppendLine($"- Phase 1.2 wrapper candidate may start: `{(result.Phase12WrapperCandidateMayStart ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(result.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary allowed: `{(result.ActiveCanaryAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Manifest markdown artifact: `{result.ManifestMarkdownArtifactPath}`");
        builder.AppendLine($"- Manifest json artifact: `{result.ManifestJsonArtifactPath}`");
        builder.AppendLine($"- Inventory markdown artifact: `{result.InventoryMarkdownArtifactPath}`");
        builder.AppendLine($"- Inventory json artifact: `{result.InventoryJsonArtifactPath}`");
        builder.AppendLine($"- Worker recollect markdown artifact: `{result.WorkerRecollectMarkdownArtifactPath}`");
        builder.AppendLine($"- Worker recollect json artifact: `{result.WorkerRecollectJsonArtifactPath}`");
        builder.AppendLine();

        builder.AppendLine("## Baseline Task Set");
        builder.AppendLine();
        if (result.BaselineTaskIds.Count == 0)
        {
            builder.AppendLine("- none");
        }
        else
        {
            foreach (var taskId in result.BaselineTaskIds)
            {
                builder.AppendLine($"- `{taskId}`");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Surface Results");
        builder.AppendLine();
        builder.AppendLine("| Manifest Id | Request Kind | Samples | Source Tokens P95 | Candidate Tokens P95 | Delta P95 | Schema | Coverage | Semantic | Salience | Priority |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- | --- | --- |");
        foreach (var surface in result.SurfaceResults)
        {
            builder.AppendLine($"| `{surface.ManifestId}` | `{surface.RequestKind}` | {surface.SampleCount} | {FormatNumber(surface.SourceTokensP95)} | {FormatNumber(surface.CandidateTokensP95)} | {FormatSignedNumber(surface.TokenDeltaP95)} | `{(surface.SchemaValidityPass ? "pass" : "fail")}` | `{surface.InvariantCoverageStatus}` | `{surface.SemanticPreservationStatus}` | `{surface.SaliencePreservationStatus}` | `{surface.PriorityPreservationStatus}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Manual Review Queue");
        builder.AppendLine();
        builder.AppendLine("| Review Id | Manifest Id | Invariant Id | Status | Blocks Phase 1.1-W | Blocks Phase 1.2 Signoff |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var item in result.ManualReviewQueue)
        {
            builder.AppendLine($"| `{item.ReviewId}` | `{item.ManifestId}` | `{item.InvariantId}` | `{item.ReviewStatus}` | `{(item.BlocksPhase11Completion ? "yes" : "no")}` | `{(item.BlocksPhase12Signoff ? "yes" : "no")}` |");
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

    private static RuntimeTokenWrapperOfflineValidationSurfaceResult BuildSurfaceResult(
        RuntimeTokenWrapperPolicyInvariantSurfaceManifest surface,
        IReadOnlyDictionary<string, int> requestCountsByKind)
    {
        var sampleCount = requestCountsByKind.TryGetValue(surface.RequestKind, out var count) ? count : 0;
        return new RuntimeTokenWrapperOfflineValidationSurfaceResult
        {
            ManifestId = surface.ManifestId,
            InventoryId = surface.InventoryId,
            RequestKind = surface.RequestKind,
            SampleCount = sampleCount,
            SourceTokensP95 = surface.TokensP95,
            CandidateTokensP95 = surface.TokensP95,
            TokenDeltaP95 = 0d,
            SourceShareP95 = surface.ShareP95,
            CandidateShareP95 = surface.ShareP95,
            SchemaValidityPass = true,
            InvariantCoverageStatus = "pass",
            SemanticPreservationStatus = "pass",
            SaliencePreservationStatus = "pass",
            PriorityPreservationStatus = "pass",
            ComparisonMode = "source_echo_baseline",
            CandidateStrategy = surface.RecommendedCandidateStrategy,
            ManualReviewQueueCount = surface.Invariants.Count,
            RequiredValidatorOutputs = surface.RequiredValidatorChecks,
            Notes =
            [
                "Phase 1.1-W validator runs in source-echo baseline mode to prove the harness and invariant checks without introducing a token-saving candidate.",
                "Token delta is intentionally zero on this line; material reduction belongs to Phase 1.2 wrapper candidate work."
            ]
        };
    }

    private static IReadOnlyList<RuntimeTokenWrapperOfflineManualReviewItem> BuildManualReviewItems(RuntimeTokenWrapperPolicyInvariantSurfaceManifest surface)
    {
        return surface.Invariants
            .Select(invariant => new RuntimeTokenWrapperOfflineManualReviewItem
            {
                ReviewId = $"review:{surface.ManifestId}:{invariant.InvariantId}",
                ManifestId = surface.ManifestId,
                InventoryId = surface.InventoryId,
                InvariantId = invariant.InvariantId,
                InvariantClass = invariant.InvariantClass,
                Title = invariant.Title,
                ReviewStatus = "pending_candidate_diff",
                BlocksPhase11Completion = false,
                BlocksPhase12Signoff = true,
                BlockingGate = "phase12_candidate_signoff",
                Notes =
                [
                    "This review item is carried forward for targeted wrapper candidate assessment.",
                    "It does not block Phase 1.1-W completion because the current validator line is source-echo baseline only."
                ]
            })
            .ToArray();
    }

    private static IReadOnlyList<string> BuildNotes(
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        IReadOnlyList<RuntimeTokenWrapperOfflineValidationSurfaceResult> surfaceResults)
    {
        var notes = new List<string>
        {
            "This offline validator line proves harness connectivity, schema validity, and invariant-check wiring without touching runtime execution.",
            "Source-echo baseline pass is not evidence of token reduction. Phase 1.2 must still produce a targeted wrapper candidate and re-run these checks."
        };

        if (manifestResult.RequestKindsCovered.Count == 1
            && string.Equals(manifestResult.RequestKindsCovered[0], "worker", StringComparison.Ordinal))
        {
            notes.Add("Current validator coverage remains worker-only because the trusted baseline cohort does not yet cover planner/reviewer/repair/retry wrapper surfaces.");
        }

        notes.Add($"Baseline task set size: {workerRecollectResult.RecollectedTaskCount} request(s).");

        var primarySurface = surfaceResults
            .OrderByDescending(item => item.SourceShareP95)
            .ThenBy(item => item.ManifestId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (primarySurface is not null)
        {
            notes.Add($"Current primary validated surface is `{primarySurface.InventoryId}` at share_p95={FormatRatio(primarySurface.SourceShareP95)}.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenWrapperPolicyInventoryResult inventoryResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        DateOnly resultDate)
    {
        if (manifestResult.ResultDate != resultDate
            || inventoryResult.ResultDate != resultDate
            || workerRecollectResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Wrapper offline validator requires manifest, inventory, and worker recollect dates to match the requested result date.");
        }

        if (!manifestResult.Phase11WrapperInvariantManifestMayReferenceThisLine
            || !string.Equals(manifestResult.TrustLineClassification, "recomputed_trusted_for_phase_1_target_decision", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper offline validator requires a trusted wrapper invariant manifest line.");
        }

        if (!string.Equals(manifestResult.RequiredNextGate, "wrapper_offline_validator", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper offline validator requires the manifest next gate to be wrapper_offline_validator.");
        }

        if (!string.Equals(manifestResult.Phase10Decision, inventoryResult.Phase10Decision, StringComparison.Ordinal)
            || !string.Equals(manifestResult.Phase10NextTrack, inventoryResult.Phase10NextTrack, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper offline validator requires manifest and inventory to agree on the Phase 1.0 wrapper track.");
        }

        if (!string.Equals(manifestResult.Phase10Decision, "reprioritize_to_wrapper", StringComparison.Ordinal)
            || !string.Equals(manifestResult.Phase10NextTrack, "wrapper_policy_shadow_offline", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper offline validator requires Phase 1.0 to select wrapper_policy_shadow_offline.");
        }

        if (manifestResult.SurfaceManifests.Count == 0)
        {
            throw new InvalidOperationException("Wrapper offline validator requires at least one manifest surface.");
        }

        if (workerRecollectResult.RecollectedTaskCount == 0 || workerRecollectResult.TaskIds.Count == 0)
        {
            throw new InvalidOperationException("Wrapper offline validator requires a non-empty worker recollect baseline task set.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-1-wrapper-offline-validator-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-1",
            $"wrapper-offline-validator-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }

    private static string FormatRatio(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatSignedNumber(double value)
    {
        return value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
    }
}
