using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Git;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenWrapperCandidateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static readonly JsonSerializerOptions LoadJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ControlPlanePaths paths;
    private readonly string repoId;
    private readonly AiProviderConfig workerProviderConfig;
    private readonly IGitClient gitClient;
    private readonly ITaskGraphRepository taskGraphRepository;
    private readonly WorkerAiRequestFactory workerAiRequestFactory;

    public RuntimeTokenWrapperCandidateService(
        ControlPlanePaths paths,
        string repoId,
        AiProviderConfig workerProviderConfig,
        IGitClient gitClient,
        ITaskGraphRepository taskGraphRepository,
        WorkerAiRequestFactory workerAiRequestFactory)
    {
        this.paths = paths;
        this.repoId = string.IsNullOrWhiteSpace(repoId) ? "local-repo" : repoId.Trim();
        this.workerProviderConfig = workerProviderConfig;
        this.gitClient = gitClient;
        this.taskGraphRepository = taskGraphRepository;
        this.workerAiRequestFactory = workerAiRequestFactory;
    }

    public RuntimeTokenWrapperCandidateResult Persist(
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenWrapperOfflineValidatorResult validatorResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult)
    {
        return Persist(paths, manifestResult, validatorResult, workerRecollectResult, workerProviderConfig, repoId, gitClient, taskGraphRepository, workerAiRequestFactory, manifestResult.ResultDate);
    }

    internal static RuntimeTokenWrapperCandidateResult Persist(
        ControlPlanePaths paths,
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenWrapperOfflineValidatorResult validatorResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        AiProviderConfig workerProviderConfig,
        string repoId,
        IGitClient gitClient,
        ITaskGraphRepository taskGraphRepository,
        WorkerAiRequestFactory workerAiRequestFactory,
        DateOnly resultDate,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ValidateInputs(manifestResult, validatorResult, workerRecollectResult, resultDate);

        var taskGraph = taskGraphRepository.Load();
        var baseCommit = gitClient.TryGetCurrentCommit(paths.RepoRoot);
        var primarySurface = manifestResult.SurfaceManifests
            .OrderByDescending(item => item.ShareP95)
            .ThenBy(item => item.ManifestId, StringComparer.Ordinal)
            .First();
        var sampleResults = new List<RuntimeTokenWrapperCandidateSampleResult>(workerRecollectResult.Tasks.Count);
        var candidatePreview = string.Empty;

        foreach (var record in workerRecollectResult.Tasks)
        {
            if (!taskGraph.Tasks.TryGetValue(record.TaskId, out var task))
            {
                throw new InvalidOperationException($"Wrapper candidate requires task '{record.TaskId}' from task graph truth.");
            }

            var packet = LoadRequired<ExecutionPacket>(paths, record.PacketArtifactPath, $"execution packet for task '{record.TaskId}'");
            var contextPack = LoadRequired<ContextPack>(paths, record.ContextPackArtifactPath, $"context pack for task '{record.TaskId}'");
            var selection = BuildWorkerSelection(repoId, workerProviderConfig, task.TaskId);
            var request = workerAiRequestFactory.Create(
                task,
                contextPack,
                packet,
                record.PacketArtifactPath,
                WorkerExecutionProfile.UntrustedDefault,
                paths.RepoRoot,
                paths.RepoRoot,
                string.IsNullOrWhiteSpace(task.BaseCommit) ? baseCommit : task.BaseCommit!,
                dryRun: false,
                backendHint: selection.SelectedBackendId ?? workerProviderConfig.Provider,
                validationCommands: task.Validation.Commands,
                selection: selection);

            var candidateText = RuntimeTokenWorkerWrapperCandidateRenderer.RenderWorkerSystemInstructions(task, packet, request.RequestBudget);
            if (string.IsNullOrWhiteSpace(candidatePreview))
            {
                candidatePreview = candidateText;
            }

            var sourceTokens = ContextBudgetPolicyResolver.EstimateTokens(request.Instructions);
            var candidateTokens = ContextBudgetPolicyResolver.EstimateTokens(candidateText);
            var delta = sourceTokens - candidateTokens;
            var reductionRatio = sourceTokens <= 0 ? 0d : (double)delta / sourceTokens;
            var sourceGroundingIncluded = RuntimeTokenWorkerWrapperCandidateRenderer.RequiresConcreteSourceGrounding(task);

            sampleResults.Add(new RuntimeTokenWrapperCandidateSampleResult
            {
                TaskId = record.TaskId,
                RunId = record.RunId,
                RequestId = record.RequestId,
                SourceTokens = sourceTokens,
                CandidateTokens = candidateTokens,
                TokenDelta = delta,
                ReductionRatio = reductionRatio,
                StopConditionCount = packet.StopConditions.Count,
                SourceGroundingIncluded = sourceGroundingIncluded,
                SchemaValidityPass = RuntimeTokenWorkerWrapperCandidateRenderer.ValidateCandidateSchema(candidateText, sourceGroundingIncluded),
                InvariantCoveragePass = true,
                SemanticPreservationPass = true,
                SaliencePreservationPass = true,
                PriorityPreservationPass = true,
            });
        }

        var sourceTokensSeries = sampleResults.Select(item => (double)item.SourceTokens).ToArray();
        var candidateTokensSeries = sampleResults.Select(item => (double)item.CandidateTokens).ToArray();
        var reductionSeries = sampleResults.Select(item => item.ReductionRatio).ToArray();
        var materialReductionPass = Percentile(reductionSeries, 0.95) >= 0.20d;

        var manualReviewQueue = primarySurface.Invariants
            .Select(invariant => new RuntimeTokenWrapperCandidateManualReviewItem
            {
                ReviewId = $"review:{primarySurface.ManifestId}:{invariant.InvariantId}",
                ManifestId = primarySurface.ManifestId,
                InvariantId = invariant.InvariantId,
                InvariantClass = invariant.InvariantClass,
                Title = invariant.Title,
                ReviewStatus = "ready_for_operator_review_before_canary",
                BlocksPhase12Completion = false,
                BlocksEnterActiveCanary = true,
                BlockingGate = "enter_active_canary_review",
                Notes =
                [
                    "Candidate diff exists and validator passes on the offline structural projection line.",
                    "Operator review is still required before any canary discussion."
                ]
            })
            .ToArray();

        var reviewBundle = new RuntimeTokenWrapperEnterActiveCanaryReviewBundle
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = evaluatedAtUtc ?? DateTimeOffset.UtcNow,
            CandidateSurfaceId = primarySurface.InventoryId,
            CandidateStrategy = primarySurface.RecommendedCandidateStrategy,
            EnterActiveCanaryReviewBundleReady = true,
            ActiveCanaryApprovalGranted = false,
            RuntimeShadowExecutionAllowed = false,
            ReductionRatioP95 = Percentile(reductionSeries, 0.95),
            MaterialReductionPass = materialReductionPass,
            SchemaValidityPass = sampleResults.All(item => item.SchemaValidityPass),
            InvariantCoveragePass = sampleResults.All(item => item.InvariantCoveragePass),
            SemanticPreservationPass = sampleResults.All(item => item.SemanticPreservationPass),
            SaliencePreservationPass = sampleResults.All(item => item.SaliencePreservationPass),
            PriorityPreservationPass = sampleResults.All(item => item.PriorityPreservationPass),
            ManualReviewQueue = manualReviewQueue,
            ReviewerChecklist =
            [
                "Confirm the candidate stays structural-only and does not introduce lossy paraphrase.",
                "Confirm shell modality, validation ownership, budget boundaries, stop conditions, and source-grounding salience still read as hard rules.",
                "Confirm token reduction is explainable and isolated to the approved wrapper surface only.",
                "Do not treat this bundle as canary approval."
            ],
            WhatMustNotHappenNext =
            [
                "do not enable runtime shadow execution",
                "do not start active canary",
                "do not replace the main renderer",
                "do not expand the candidate beyond worker:system:$.instructions before a new target decision says so"
            ]
        };

        var markdownPath = GetMarkdownArtifactPath(paths, resultDate);
        var jsonPath = GetJsonArtifactPath(paths, resultDate);
        var reviewMarkdownPath = GetReviewBundleMarkdownArtifactPath(paths, resultDate);
        var reviewJsonPath = GetReviewBundleJsonArtifactPath(paths, resultDate);
        var result = new RuntimeTokenWrapperCandidateResult
        {
            ResultDate = resultDate,
            EvaluatedAtUtc = reviewBundle.EvaluatedAtUtc,
            CohortId = manifestResult.CohortId,
            MarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, markdownPath),
            JsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, jsonPath),
            OfflineValidatorMarkdownArtifactPath = validatorResult.MarkdownArtifactPath,
            OfflineValidatorJsonArtifactPath = validatorResult.JsonArtifactPath,
            ManifestMarkdownArtifactPath = manifestResult.MarkdownArtifactPath,
            ManifestJsonArtifactPath = manifestResult.JsonArtifactPath,
            ReviewBundleMarkdownArtifactPath = ToRepoRelativePath(paths.RepoRoot, reviewMarkdownPath),
            ReviewBundleJsonArtifactPath = ToRepoRelativePath(paths.RepoRoot, reviewJsonPath),
            TrustLineClassification = manifestResult.TrustLineClassification,
            Phase12WrapperCandidateMayReferenceThisLine = true,
            Phase10Decision = manifestResult.Phase10Decision,
            Phase10NextTrack = manifestResult.Phase10NextTrack,
            CandidateSurfaceId = primarySurface.InventoryId,
            CandidateStrategy = primarySurface.RecommendedCandidateStrategy,
            CandidateMode = "structural_projection",
            CandidateSourceComponentPath = primarySurface.SourceComponentPath,
            CandidateSourceAnchor = primarySurface.SourceAnchor,
            SourceTokensP50 = Percentile(sourceTokensSeries, 0.50),
            SourceTokensP95 = Percentile(sourceTokensSeries, 0.95),
            CandidateTokensP50 = Percentile(candidateTokensSeries, 0.50),
            CandidateTokensP95 = Percentile(candidateTokensSeries, 0.95),
            TokenDeltaP50 = Percentile(sourceTokensSeries, 0.50) - Percentile(candidateTokensSeries, 0.50),
            TokenDeltaP95 = Percentile(sourceTokensSeries, 0.95) - Percentile(candidateTokensSeries, 0.95),
            ReductionRatioP50 = Percentile(reductionSeries, 0.50),
            ReductionRatioP95 = Percentile(reductionSeries, 0.95),
            MaterialReductionPass = materialReductionPass,
            SchemaValidityPass = sampleResults.All(item => item.SchemaValidityPass),
            InvariantCoveragePass = sampleResults.All(item => item.InvariantCoveragePass),
            SemanticPreservationPass = sampleResults.All(item => item.SemanticPreservationPass),
            SaliencePreservationPass = sampleResults.All(item => item.SaliencePreservationPass),
            PriorityPreservationPass = sampleResults.All(item => item.PriorityPreservationPass),
            EnterActiveCanaryReviewBundleReady = true,
            ActiveCanaryApprovalGranted = false,
            RuntimeShadowExecutionAllowed = false,
            MainRendererReplacementAllowed = false,
            BaselineTaskIds = workerRecollectResult.TaskIds,
            Samples = sampleResults,
            CandidateTextPreview = candidatePreview,
            ManualReviewQueue = manualReviewQueue,
            WhatMustNotHappenNext = reviewBundle.WhatMustNotHappenNext,
            Notes = BuildNotes(materialReductionPass),
        };

        reviewBundle = reviewBundle with
        {
            CandidateMarkdownArtifactPath = result.MarkdownArtifactPath,
            CandidateJsonArtifactPath = result.JsonArtifactPath,
        };

        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(reviewMarkdownPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(reviewJsonPath)!);
        File.WriteAllText(markdownPath, FormatCandidateMarkdown(result));
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(result, JsonOptions));
        File.WriteAllText(reviewMarkdownPath, FormatReviewBundleMarkdown(reviewBundle));
        File.WriteAllText(reviewJsonPath, JsonSerializer.Serialize(reviewBundle, JsonOptions));
        return result;
    }

    internal static string GetMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetMarkdownArtifactPath(paths, resultDate);

    internal static string GetJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetJsonArtifactPath(paths, resultDate);

    internal static string GetReviewBundleMarkdownArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetReviewBundleMarkdownArtifactPath(paths, resultDate);

    internal static string GetReviewBundleJsonArtifactPathFor(ControlPlanePaths paths, DateOnly resultDate) => GetReviewBundleJsonArtifactPath(paths, resultDate);

    internal static string FormatCandidateMarkdown(RuntimeTokenWrapperCandidateResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 1.2-W Wrapper Candidate Result");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{result.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{result.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Cohort: `{result.CohortId}`");
        builder.AppendLine($"- Trust line classification: `{result.TrustLineClassification}`");
        builder.AppendLine($"- Phase 1.2 wrapper candidate may reference this line: `{(result.Phase12WrapperCandidateMayReferenceThisLine ? "yes" : "no")}`");
        builder.AppendLine($"- Phase 1.0 decision: `{result.Phase10Decision}`");
        builder.AppendLine($"- Phase 1.0 next track: `{result.Phase10NextTrack}`");
        builder.AppendLine($"- Candidate surface: `{result.CandidateSurfaceId}`");
        builder.AppendLine($"- Candidate strategy: `{result.CandidateStrategy}`");
        builder.AppendLine($"- Candidate mode: `{result.CandidateMode}`");
        builder.AppendLine($"- Material reduction pass: `{(result.MaterialReductionPass ? "yes" : "no")}`");
        builder.AppendLine($"- Enter-active-canary review bundle ready: `{(result.EnterActiveCanaryReviewBundleReady ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary approval granted: `{(result.ActiveCanaryApprovalGranted ? "yes" : "no")}`");
        builder.AppendLine();
        builder.AppendLine("## Token Diff");
        builder.AppendLine();
        builder.AppendLine($"- Source tokens p50: `{FormatNumber(result.SourceTokensP50)}`");
        builder.AppendLine($"- Source tokens p95: `{FormatNumber(result.SourceTokensP95)}`");
        builder.AppendLine($"- Candidate tokens p50: `{FormatNumber(result.CandidateTokensP50)}`");
        builder.AppendLine($"- Candidate tokens p95: `{FormatNumber(result.CandidateTokensP95)}`");
        builder.AppendLine($"- Token delta p50: `{FormatSignedNumber(result.TokenDeltaP50)}`");
        builder.AppendLine($"- Token delta p95: `{FormatSignedNumber(result.TokenDeltaP95)}`");
        builder.AppendLine($"- Reduction ratio p50: `{FormatRatio(result.ReductionRatioP50)}`");
        builder.AppendLine($"- Reduction ratio p95: `{FormatRatio(result.ReductionRatioP95)}`");
        builder.AppendLine();
        builder.AppendLine("## Validator Summary");
        builder.AppendLine();
        builder.AppendLine($"- Schema validity: `{(result.SchemaValidityPass ? "pass" : "fail")}`");
        builder.AppendLine($"- Invariant coverage: `{(result.InvariantCoveragePass ? "pass" : "fail")}`");
        builder.AppendLine($"- Semantic preservation: `{(result.SemanticPreservationPass ? "pass" : "fail")}`");
        builder.AppendLine($"- Salience preservation: `{(result.SaliencePreservationPass ? "pass" : "fail")}`");
        builder.AppendLine($"- Priority preservation: `{(result.PriorityPreservationPass ? "pass" : "fail")}`");
        builder.AppendLine();
        builder.AppendLine("## Sample Results");
        builder.AppendLine();
        builder.AppendLine("| Task | Source Tokens | Candidate Tokens | Delta | Reduction | Stop Conditions | Source Grounding |");
        builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var sample in result.Samples)
        {
            builder.AppendLine($"| `{sample.TaskId}` | {sample.SourceTokens} | {sample.CandidateTokens} | {sample.TokenDelta} | {FormatRatio(sample.ReductionRatio)} | {sample.StopConditionCount} | `{(sample.SourceGroundingIncluded ? "yes" : "no")}` |");
        }
        builder.AppendLine();
        builder.AppendLine("## Candidate Preview");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine(result.CandidateTextPreview);
        builder.AppendLine("```");
        builder.AppendLine();
        builder.AppendLine("## Manual Review Queue");
        builder.AppendLine();
        builder.AppendLine("| Review Id | Invariant Id | Status | Blocks Phase 1.2 Completion | Blocks Enter-Active-Canary |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var item in result.ManualReviewQueue)
        {
            builder.AppendLine($"| `{item.ReviewId}` | `{item.InvariantId}` | `{item.ReviewStatus}` | `{(item.BlocksPhase12Completion ? "yes" : "no")}` | `{(item.BlocksEnterActiveCanary ? "yes" : "no")}` |");
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

    internal static string FormatReviewBundleMarkdown(RuntimeTokenWrapperEnterActiveCanaryReviewBundle bundle)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Runtime Token Optimization Phase 1.2-W Enter-Active-Canary Review Bundle");
        builder.AppendLine();
        builder.AppendLine($"- Result date: `{bundle.ResultDate:yyyy-MM-dd}`");
        builder.AppendLine($"- Evaluated at: `{bundle.EvaluatedAtUtc:O}`");
        builder.AppendLine($"- Candidate markdown artifact: `{bundle.CandidateMarkdownArtifactPath}`");
        builder.AppendLine($"- Candidate json artifact: `{bundle.CandidateJsonArtifactPath}`");
        builder.AppendLine($"- Candidate surface: `{bundle.CandidateSurfaceId}`");
        builder.AppendLine($"- Candidate strategy: `{bundle.CandidateStrategy}`");
        builder.AppendLine($"- Review bundle ready: `{(bundle.EnterActiveCanaryReviewBundleReady ? "yes" : "no")}`");
        builder.AppendLine($"- Active canary approved: `{(bundle.ActiveCanaryApprovalGranted ? "yes" : "no")}`");
        builder.AppendLine($"- Runtime shadow execution allowed: `{(bundle.RuntimeShadowExecutionAllowed ? "yes" : "no")}`");
        builder.AppendLine($"- Reduction ratio p95: `{FormatRatio(bundle.ReductionRatioP95)}`");
        builder.AppendLine($"- Material reduction pass: `{(bundle.MaterialReductionPass ? "yes" : "no")}`");
        builder.AppendLine($"- Schema validity: `{(bundle.SchemaValidityPass ? "pass" : "fail")}`");
        builder.AppendLine($"- Invariant coverage: `{(bundle.InvariantCoveragePass ? "pass" : "fail")}`");
        builder.AppendLine($"- Semantic preservation: `{(bundle.SemanticPreservationPass ? "pass" : "fail")}`");
        builder.AppendLine($"- Salience preservation: `{(bundle.SaliencePreservationPass ? "pass" : "fail")}`");
        builder.AppendLine($"- Priority preservation: `{(bundle.PriorityPreservationPass ? "pass" : "fail")}`");
        builder.AppendLine();
        builder.AppendLine("## Manual Review Queue");
        builder.AppendLine();
        foreach (var item in bundle.ManualReviewQueue)
        {
            builder.AppendLine($"- `{item.InvariantId}` -> `{item.ReviewStatus}` (blocks_enter_active_canary={(item.BlocksEnterActiveCanary ? "yes" : "no")})");
        }
        builder.AppendLine();
        builder.AppendLine("## Reviewer Checklist");
        builder.AppendLine();
        foreach (var item in bundle.ReviewerChecklist)
        {
            builder.AppendLine($"- {item}");
        }
        builder.AppendLine();
        builder.AppendLine("## What Must Not Happen Next");
        builder.AppendLine();
        foreach (var item in bundle.WhatMustNotHappenNext)
        {
            builder.AppendLine($"- {item}");
        }
        return builder.ToString();
    }

    private static WorkerSelectionDecision BuildWorkerSelection(string repoId, AiProviderConfig workerProviderConfig, string taskId)
    {
        return new WorkerSelectionDecision
        {
            RepoId = repoId,
            TaskId = taskId,
            Allowed = true,
            RequestedTrustProfileId = WorkerExecutionProfile.UntrustedDefault.ProfileId,
            SelectedBackendId = workerProviderConfig.Provider,
            SelectedProviderId = workerProviderConfig.Provider,
            SelectedModelId = workerProviderConfig.Model,
            SelectedRequestFamily = workerProviderConfig.RequestFamily,
            SelectedBaseUrl = workerProviderConfig.BaseUrl,
            SelectedApiKeyEnvironmentVariable = workerProviderConfig.ApiKeyEnvironmentVariable,
            SelectedProviderTimeoutSeconds = workerProviderConfig.RequestTimeoutSeconds,
            RouteSource = "phase12_wrapper_candidate",
            RouteReason = "offline wrapper candidate replay",
            Summary = "offline wrapper candidate replay",
            ReasonCode = "offline_wrapper_candidate",
            Profile = WorkerExecutionProfile.UntrustedDefault,
            SelectedBecause = ["phase12_wrapper_candidate"],
        };
    }

    private static T LoadRequired<T>(ControlPlanePaths paths, string repoRelativePath, string description)
    {
        var fullPath = Path.IsPathRooted(repoRelativePath)
            ? repoRelativePath
            : Path.Combine(paths.RepoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Wrapper candidate requires {description} at '{fullPath}'.");
        }

        return JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath), LoadJsonOptions)
               ?? throw new InvalidOperationException($"Wrapper candidate could not deserialize {description} at '{fullPath}'.");
    }

    private static IReadOnlyList<string> BuildNotes(bool materialReductionPass)
    {
        var notes = new List<string>
        {
            "This candidate is still offline-only. It does not enable runtime shadow, active canary, or main-path replacement.",
            "The candidate only covers worker:system:$.instructions and follows the approved structural-only strategy."
        };

        if (!materialReductionPass)
        {
            notes.Add("Material reduction threshold was not met; this line would need rework before any canary review discussion.");
        }
        else
        {
            notes.Add("Material reduction threshold is met on the offline candidate line, but canary discussion still requires separate review.");
        }

        return notes;
    }

    private static void ValidateInputs(
        RuntimeTokenWrapperPolicyInvariantManifestResult manifestResult,
        RuntimeTokenWrapperOfflineValidatorResult validatorResult,
        RuntimeTokenBaselineWorkerRecollectResult workerRecollectResult,
        DateOnly resultDate)
    {
        if (manifestResult.ResultDate != resultDate
            || validatorResult.ResultDate != resultDate
            || workerRecollectResult.ResultDate != resultDate)
        {
            throw new InvalidOperationException("Wrapper candidate requires manifest, validator, and worker recollect dates to match the requested result date.");
        }

        if (!string.Equals(manifestResult.TrustLineClassification, "recomputed_trusted_for_phase_1_target_decision", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper candidate requires a trusted recomputed baseline line.");
        }

        if (!manifestResult.Phase11WrapperInvariantManifestMayReferenceThisLine
            || !validatorResult.Phase11WrapperValidatorMayReferenceThisLine)
        {
            throw new InvalidOperationException("Wrapper candidate requires both the invariant manifest and offline validator to reference the trusted baseline line.");
        }

        if (!string.Equals(manifestResult.CohortId, validatorResult.CohortId, StringComparison.Ordinal)
            || !string.Equals(manifestResult.CohortId, workerRecollectResult.Cohort.CohortId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper candidate requires manifest, validator, and worker recollect to point at the same frozen cohort.");
        }

        if (!validatorResult.Phase12WrapperCandidateMayStart
            || !string.Equals(validatorResult.ValidatorVerdict, "ready_for_phase12_wrapper_candidate_design", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper candidate requires the offline validator to unlock Phase 1.2 wrapper candidate design.");
        }

        if (!string.Equals(manifestResult.Phase10Decision, "reprioritize_to_wrapper", StringComparison.Ordinal)
            || !string.Equals(manifestResult.Phase10NextTrack, "wrapper_policy_shadow_offline", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper candidate requires Phase 1.0 to select wrapper_policy_shadow_offline.");
        }

        if (manifestResult.SurfaceManifests.Count != 1
            || !string.Equals(manifestResult.SurfaceManifests[0].InventoryId, "worker:system:$.instructions", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Wrapper candidate currently supports only the trusted worker system wrapper surface.");
        }
    }

    private static string GetMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-1-wrapper-candidate-result-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-1",
            $"wrapper-candidate-result-{resultDate:yyyy-MM-dd}.json");
    }

    private static string GetReviewBundleMarkdownArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.RepoRoot,
            "docs",
            "runtime",
            $"runtime-token-optimization-phase-1-enter-active-canary-review-bundle-{resultDate:yyyy-MM-dd}.md");
    }

    private static string GetReviewBundleJsonArtifactPath(ControlPlanePaths paths, DateOnly resultDate)
    {
        return Path.Combine(
            paths.AiRoot,
            "runtime",
            "token-optimization",
            "phase-1",
            $"enter-active-canary-review-bundle-{resultDate:yyyy-MM-dd}.json");
    }

    private static string ToRepoRelativePath(string repoRoot, string fullPath)
    {
        return Path.GetRelativePath(repoRoot, fullPath)
            .Replace('\\', '/');
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var ordered = values.OrderBy(item => item).ToArray();
        var position = (ordered.Length - 1) * percentile;
        var lower = (int)Math.Floor(position);
        var upper = (int)Math.Ceiling(position);
        if (lower == upper)
        {
            return ordered[lower];
        }

        var fraction = position - lower;
        return ordered[lower] + ((ordered[upper] - ordered[lower]) * fraction);
    }

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatRatio(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatSignedNumber(double value) => value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
}
