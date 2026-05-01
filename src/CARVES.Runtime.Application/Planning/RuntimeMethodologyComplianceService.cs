using System.Text.Json;
using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Domain.Cards;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class RuntimeMethodologyComplianceService
{
    private const string MethodologyPath = ".ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md";
    private const string MethodologyConstraint = "Must consult `.ai/memory/architecture/05_EXECUTION_OS_METHODOLOGY.md` before implementation.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    private static readonly string[] RuntimeKeywords =
    [
        "runtime",
        "host",
        "session",
        "attach",
        "dashboard",
        "worker",
        "execution",
        "boundary",
        "replan",
        "provider",
        "repo runtime",
        "thin client",
    ];

    private readonly ControlPlanePaths paths;

    public RuntimeMethodologyComplianceService(ControlPlanePaths paths)
    {
        this.paths = paths;
    }

    public string AsyncResumeGatePath => Path.Combine(paths.RuntimeRoot, "planning", "async-multi-worker-resume-gate.json");

    public RuntimeMethodologyAssessment AssessCard(CardDefinition card, string? markdownPath = null)
    {
        var text = BuildText(
            card.Title,
            card.Goal,
            string.Join(Environment.NewLine, card.Scope),
            string.Join(Environment.NewLine, card.Acceptance),
            string.Join(Environment.NewLine, card.Constraints),
            markdownPath is not null && File.Exists(markdownPath) ? File.ReadAllText(markdownPath) : null);
        return Assess(text);
    }

    public RuntimeMethodologyAssessment AssessDraft(CardDraftRecord draft)
    {
        if (draft.MethodologyRequired || draft.MethodologyAcknowledged || !string.IsNullOrWhiteSpace(draft.MethodologyCoverageStatus))
        {
            return new RuntimeMethodologyAssessment
            {
                Applies = draft.MethodologyRequired,
                Acknowledged = draft.MethodologyAcknowledged,
                ReferencePath = draft.MethodologyReferencePath ?? MethodologyPath,
                CoverageStatus = ParseCoverageStatus(draft.MethodologyCoverageStatus),
                RelatedCardIds = draft.MethodologyRelatedCards,
                Summary = draft.MethodologySummary ?? string.Empty,
                RecommendedAction = draft.MethodologyRecommendedAction ?? string.Empty,
            };
        }

        return Assess(BuildText(
            draft.Title,
            draft.Goal,
            string.Join(Environment.NewLine, draft.Scope),
            string.Join(Environment.NewLine, draft.Acceptance),
            string.Join(Environment.NewLine, draft.Constraints),
            string.Join(Environment.NewLine, draft.Notes)));
    }

    public RuntimeMethodologyAssessment AssessTaskGraphDraft(TaskGraphDraftRecord draft, CardDraftRecord? cardDraft = null)
    {
        if (draft.MethodologyRequired || draft.MethodologyAcknowledged || !string.IsNullOrWhiteSpace(draft.MethodologyCoverageStatus))
        {
            return new RuntimeMethodologyAssessment
            {
                Applies = draft.MethodologyRequired,
                Acknowledged = draft.MethodologyAcknowledged,
                ReferencePath = draft.MethodologyReferencePath ?? MethodologyPath,
                CoverageStatus = ParseCoverageStatus(draft.MethodologyCoverageStatus),
                RelatedCardIds = draft.MethodologyRelatedCards,
                Summary = draft.MethodologySummary ?? string.Empty,
                RecommendedAction = draft.MethodologyRecommendedAction ?? string.Empty,
            };
        }

        var cardAssessment = cardDraft is null ? new RuntimeMethodologyAssessment() : AssessDraft(cardDraft);
        if (cardAssessment.Applies)
        {
            return cardAssessment;
        }

        var text = BuildText(
            draft.CardId,
            string.Join(Environment.NewLine, draft.Tasks.Select(task => task.Title)),
            string.Join(Environment.NewLine, draft.Tasks.Select(task => task.Description)),
            string.Join(Environment.NewLine, draft.Tasks.SelectMany(task => task.Scope)),
            string.Join(Environment.NewLine, draft.Tasks.SelectMany(task => task.Constraints)),
            null);
        return Assess(text);
    }

    public CardDraftRecord NormalizeDraft(CardDraftRecord draft)
    {
        var assessment = AssessDraftContent(draft);
        var constraints = draft.Constraints.ToList();
        if (assessment.Applies && !constraints.Contains(MethodologyConstraint, StringComparer.Ordinal))
        {
            constraints.Add(MethodologyConstraint);
        }

        var normalizedAssessment = Assess(BuildText(
            draft.Title,
            draft.Goal,
            string.Join(Environment.NewLine, draft.Scope),
            string.Join(Environment.NewLine, draft.Acceptance),
            string.Join(Environment.NewLine, constraints),
            string.Join(Environment.NewLine, draft.Notes)));

        return new CardDraftRecord
        {
            DraftId = draft.DraftId,
            CardId = draft.CardId,
            Title = draft.Title,
            Goal = draft.Goal,
            Acceptance = draft.Acceptance,
            Scope = draft.Scope,
            Constraints = constraints,
            Notes = draft.Notes,
            AcceptanceContract = draft.AcceptanceContract,
            RealityModel = draft.RealityModel,
            PlanningLineage = draft.PlanningLineage,
            Status = draft.Status,
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = draft.UpdatedAtUtc,
            ReviewedAtUtc = draft.ReviewedAtUtc,
            ApprovedAtUtc = draft.ApprovedAtUtc,
            RejectedAtUtc = draft.RejectedAtUtc,
            ArchivedAtUtc = draft.ArchivedAtUtc,
            LifecycleReason = draft.LifecycleReason,
            MethodologyRequired = normalizedAssessment.Applies,
            MethodologyAcknowledged = normalizedAssessment.Acknowledged,
            MethodologyReferencePath = normalizedAssessment.Applies ? normalizedAssessment.ReferencePath : null,
            MethodologyCoverageStatus = DescribeCoverage(normalizedAssessment.CoverageStatus),
            MethodologyRelatedCards = normalizedAssessment.RelatedCardIds,
            MethodologySummary = normalizedAssessment.Summary,
            MethodologyRecommendedAction = normalizedAssessment.RecommendedAction,
            MarkdownPath = draft.MarkdownPath,
        };
    }

    public TaskGraphDraftRecord NormalizeTaskGraphDraft(TaskGraphDraftRecord draft, CardDraftRecord? cardDraft = null)
    {
        var assessment = AssessTaskGraphDraftContent(draft, cardDraft);
        return new TaskGraphDraftRecord
        {
            DraftId = draft.DraftId,
            CardId = draft.CardId,
            Status = draft.Status,
            CreatedAtUtc = draft.CreatedAtUtc,
            UpdatedAtUtc = draft.UpdatedAtUtc,
            ApprovedAtUtc = draft.ApprovedAtUtc,
            ApprovalReason = draft.ApprovalReason,
            MethodologyRequired = assessment.Applies,
            MethodologyAcknowledged = assessment.Acknowledged,
            MethodologyReferencePath = assessment.Applies ? assessment.ReferencePath : null,
            MethodologyCoverageStatus = DescribeCoverage(assessment.CoverageStatus),
            MethodologyRelatedCards = assessment.RelatedCardIds,
            MethodologySummary = assessment.Summary,
            MethodologyRecommendedAction = assessment.RecommendedAction,
            PlanningLineage = draft.PlanningLineage,
            Tasks = draft.Tasks,
        };
    }

    public void EnsureSatisfied(RuntimeMethodologyAssessment assessment, string subject)
    {
        if (!assessment.Applies || assessment.Acknowledged)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{subject} requires methodology acknowledgment. Add `{MethodologyPath}` to the card constraints or use the host-routed draft normalization path.");
    }

    public DeferredLineageResumeGate EnsureAsyncResumeGate()
    {
        var gate = BuildAsyncResumeGate();
        Directory.CreateDirectory(Path.GetDirectoryName(AsyncResumeGatePath)!);
        File.WriteAllText(AsyncResumeGatePath, JsonSerializer.Serialize(gate, JsonOptions));
        return gate;
    }

    public DeferredLineageResumeGate BuildAsyncResumeGate()
    {
        return new DeferredLineageResumeGate
        {
            Preconditions =
            [
                "Execution protocol, boundary enforcement, attach/session truth, and pilot proof cards are completed.",
                "Runtime consistency verification reports zero findings before resuming async lineage work.",
                "New cards touching async orchestration or delegation proof must route to CARD-136..154 instead of creating duplicate lineage.",
            ],
            ResumeOrder =
            [
                new DeferredLineageResumeStep
                {
                    Order = 1,
                    Summary = "Serialize control-plane mutations before resuming concurrent orchestration.",
                    CardIds = ["CARD-152"],
                    ResumeReason = "File and task truth contention must be removed before multi-worker orchestration resumes.",
                },
                new DeferredLineageResumeStep
                {
                    Order = 2,
                    Summary = "Resume planner/worker async orchestration foundation.",
                    CardIds = ["CARD-136", "CARD-137", "CARD-138", "CARD-139", "CARD-140"],
                    ResumeReason = "Wake bridge, independent planner loop, reentry, session semantics, and proof must land before broader delegation work.",
                },
                new DeferredLineageResumeStep
                {
                    Order = 3,
                    Summary = "Resume delegation proof and host-mediated control-plane lineage.",
                    CardIds = ["CARD-141", "CARD-142", "CARD-143", "CARD-144", "CARD-145", "CARD-146", "CARD-147", "CARD-148", "CARD-149"],
                    ResumeReason = "Async execution must keep host-mediated mutation proof and cold-path restriction intact.",
                },
                new DeferredLineageResumeStep
                {
                    Order = 4,
                    Summary = "Finish local CLI approval handling and projection hardening.",
                    CardIds = ["CARD-150", "CARD-154"],
                    ResumeReason = "Approval waits and multi-worker projection should be resumed only after orchestration and control-plane truth are stable.",
                },
            ],
            AntiDuplicationRules =
            [
                "Do not create new cards for async worker wake, reentry, mutation proof, or multi-worker projection while CARD-136..154 remain deferred.",
                "Map new resident-host, multi-worker, approval-wait, or delegation-proof requests back to the deferred lineage before planning.",
                "Use CARD-152 as the resume gate head whenever concurrent mutation risk is part of the request.",
            ],
        };
    }

    public static string DescribeCoverage(RuntimeMethodologyCoverageStatus status)
    {
        return status switch
        {
            RuntimeMethodologyCoverageStatus.NewDelta => "new_delta",
            RuntimeMethodologyCoverageStatus.CoveredCompletedLineage => "covered_completed_lineage",
            RuntimeMethodologyCoverageStatus.DeferredLineageResume => "deferred_lineage_resume",
            RuntimeMethodologyCoverageStatus.MissingAcknowledgment => "missing_acknowledgment",
            _ => "not_applicable",
        };
    }

    private static RuntimeMethodologyAssessment Assess(string text)
    {
        var normalized = text.ToLowerInvariant();
        var applies = RuntimeKeywords.Any(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
        if (!applies)
        {
            return new RuntimeMethodologyAssessment
            {
                Applies = false,
                Acknowledged = true,
                ReferencePath = MethodologyPath,
                CoverageStatus = RuntimeMethodologyCoverageStatus.NotApplicable,
                Summary = "Methodology gate does not apply to this card or draft.",
                RecommendedAction = "no methodology acknowledgment required",
            };
        }

        var acknowledged = normalized.Contains(MethodologyPath.ToLowerInvariant(), StringComparison.Ordinal)
            || normalized.Contains("05_execution_os_methodology.md", StringComparison.Ordinal);
        var (status, relatedCards, summary, recommendedAction) = ClassifyCoverage(normalized, acknowledged);
        return new RuntimeMethodologyAssessment
        {
            Applies = true,
            Acknowledged = acknowledged,
            ReferencePath = MethodologyPath,
            CoverageStatus = status,
            RelatedCardIds = relatedCards,
            Summary = summary,
            RecommendedAction = recommendedAction,
        };
    }

    private static (RuntimeMethodologyCoverageStatus Status, IReadOnlyList<string> RelatedCards, string Summary, string RecommendedAction) ClassifyCoverage(string normalizedText, bool acknowledged)
    {
        if (ContainsAny(normalizedText, "multi-worker", "async", "delegation proof", "mutation serialization", "approval event"))
        {
            return (
                acknowledged ? RuntimeMethodologyCoverageStatus.DeferredLineageResume : RuntimeMethodologyCoverageStatus.MissingAcknowledgment,
                ["CARD-136", "CARD-137", "CARD-138", "CARD-139", "CARD-140", "CARD-141", "CARD-142", "CARD-143", "CARD-144", "CARD-145", "CARD-146", "CARD-147", "CARD-148", "CARD-149", "CARD-150", "CARD-152", "CARD-154"],
                acknowledged
                    ? "This request overlaps the deferred async and multi-worker lineage."
                    : "This request overlaps deferred async lineage but does not acknowledge the methodology gate.",
                acknowledged
                    ? "resume the deferred lineage in the published order instead of creating duplicate cards"
                    : $"add `{MethodologyPath}` and map the request to CARD-136..154");
        }

        if (ContainsAny(normalizedText, "result envelope", "failure intelligence", "boundary", "replan", "execution run", "remote api worker", "provider protocol", "thin client", "attach handshake", "runtime manifest"))
        {
            return (
                acknowledged ? RuntimeMethodologyCoverageStatus.CoveredCompletedLineage : RuntimeMethodologyCoverageStatus.MissingAcknowledgment,
                ResolveCoveredCards(normalizedText),
                acknowledged
                    ? "This request overlaps execution OS work already covered by completed lineage."
                    : "This request overlaps completed execution OS lineage but does not acknowledge the methodology gate.",
                acknowledged
                    ? "extend the existing truth or surface instead of creating a parallel subsystem"
                    : $"add `{MethodologyPath}` and state which completed lineage is being extended");
        }

        return (
            acknowledged ? RuntimeMethodologyCoverageStatus.NewDelta : RuntimeMethodologyCoverageStatus.MissingAcknowledgment,
            Array.Empty<string>(),
            acknowledged
                ? "This request is a new execution OS delta and correctly acknowledges the methodology gate."
                : "This runtime-oriented request needs an explicit methodology acknowledgment before planning or persistence.",
            acknowledged
                ? "proceed through the existing host-routed planning path"
                : $"add `{MethodologyPath}` to the card or draft constraints before planning");
    }

    private static IReadOnlyList<string> ResolveCoveredCards(string normalizedText)
    {
        if (ContainsAny(normalizedText, "result envelope", "task envelope"))
        {
            return ["CARD-167", "CARD-168"];
        }

        if (ContainsAny(normalizedText, "failure intelligence", "failure summary", "failure policy"))
        {
            return ["CARD-156", "CARD-157", "CARD-158", "CARD-159", "CARD-160", "CARD-161"];
        }

        if (ContainsAny(normalizedText, "boundary", "budget", "risk", "enforcement", "replan"))
        {
            return ["CARD-170", "CARD-171", "CARD-172", "CARD-173", "CARD-174", "CARD-175", "CARD-176", "CARD-177", "CARD-178"];
        }

        if (ContainsAny(normalizedText, "execution run", "session truth", "step"))
        {
            return ["CARD-179", "CARD-180", "CARD-181", "CARD-182", "CARD-183"];
        }

        if (ContainsAny(normalizedText, "remote api", "provider protocol", "gemini", "http transport"))
        {
            return ["CARD-192", "CARD-193", "CARD-194", "CARD-195", "CARD-196", "CARD-197"];
        }

        if (ContainsAny(normalizedText, "attach", "runtime manifest", "dashboard truth", "card lifecycle"))
        {
            return ["CARD-219", "CARD-220", "CARD-221", "CARD-222", "CARD-223", "CARD-224", "CARD-225", "CARD-226", "CARD-227", "CARD-228", "CARD-229", "CARD-230", "CARD-231", "CARD-232", "CARD-233", "CARD-234", "CARD-235", "CARD-236", "CARD-237", "CARD-238"];
        }

        return Array.Empty<string>();
    }

    private static string BuildText(params string?[] segments)
    {
        return string.Join(Environment.NewLine, segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static RuntimeMethodologyAssessment AssessDraftContent(CardDraftRecord draft)
    {
        return Assess(BuildText(
            draft.Title,
            draft.Goal,
            string.Join(Environment.NewLine, draft.Scope),
            string.Join(Environment.NewLine, draft.Acceptance),
            string.Join(Environment.NewLine, draft.Constraints),
            string.Join(Environment.NewLine, draft.Notes)));
    }

    private static RuntimeMethodologyAssessment AssessTaskGraphDraftContent(TaskGraphDraftRecord draft, CardDraftRecord? cardDraft)
    {
        var cardAssessment = cardDraft is null ? new RuntimeMethodologyAssessment() : AssessDraftContent(cardDraft);
        if (cardAssessment.Applies)
        {
            return cardAssessment;
        }

        return Assess(BuildText(
            draft.CardId,
            string.Join(Environment.NewLine, draft.Tasks.Select(task => task.Title)),
            string.Join(Environment.NewLine, draft.Tasks.Select(task => task.Description)),
            string.Join(Environment.NewLine, draft.Tasks.SelectMany(task => task.Scope)),
            string.Join(Environment.NewLine, draft.Tasks.SelectMany(task => task.Constraints)),
            null));
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(value => text.Contains(value, StringComparison.Ordinal));
    }

    private static RuntimeMethodologyCoverageStatus ParseCoverageStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RuntimeMethodologyCoverageStatus.NotApplicable;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "new_delta" => RuntimeMethodologyCoverageStatus.NewDelta,
            "covered_completed_lineage" => RuntimeMethodologyCoverageStatus.CoveredCompletedLineage,
            "deferred_lineage_resume" => RuntimeMethodologyCoverageStatus.DeferredLineageResume,
            "missing_acknowledgment" => RuntimeMethodologyCoverageStatus.MissingAcknowledgment,
            _ => RuntimeMethodologyCoverageStatus.NotApplicable,
        };
    }
}
