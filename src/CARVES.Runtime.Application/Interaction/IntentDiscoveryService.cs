using Carves.Runtime.Application.ControlPlane;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Planning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Carves.Runtime.Application.Interaction;

public sealed class IntentDiscoveryService
{
    private const string PrimaryPlanningSlotId = ActivePlanningSlotProjectionResolver.PrimaryFormalPlanningSlotId;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private readonly string repoRoot;
    private readonly ControlPlanePaths paths;
    private readonly IIntentDraftRepository draftRepository;
    private readonly ProjectUnderstandingProjectionService projectUnderstandingProjectionService;

    public IntentDiscoveryService(
        string repoRoot,
        ControlPlanePaths paths,
        IIntentDraftRepository draftRepository,
        ProjectUnderstandingProjectionService projectUnderstandingProjectionService)
    {
        this.repoRoot = repoRoot;
        this.paths = paths;
        this.draftRepository = draftRepository;
        this.projectUnderstandingProjectionService = projectUnderstandingProjectionService;
    }

    public IntentDiscoveryStatus GetStatus()
    {
        var acceptedPath = GetAcceptedIntentPath();
        var acceptedExists = File.Exists(acceptedPath);
        var acceptedPreview = acceptedExists ? LoadAcceptedPreview(acceptedPath) : "(missing)";
        var draft = draftRepository.Load();
        if (draft is not null)
        {
            draft = RebuildDraft(draft);
        }

        var understanding = projectUnderstandingProjectionService.Evaluate(false);
        var state = ResolveState(acceptedPath, acceptedExists, draft, understanding);

        return new IntentDiscoveryStatus(
            state,
            acceptedPath,
            acceptedExists,
            acceptedPreview,
            draft,
            draft is not null,
            BuildRecommendedNextAction(state),
            state switch
            {
                IntentDiscoveryState.Missing => "no accepted project intent exists yet",
                IntentDiscoveryState.Drafted => "a draft intent exists and awaits human acceptance",
                IntentDiscoveryState.Accepted => "accepted project intent truth exists",
                IntentDiscoveryState.Stale => "accepted project intent exists but appears stale or incomplete",
                _ => "intent status is unknown",
            });
    }

    public IntentDiscoveryStatus GenerateDraft()
    {
        draftRepository.Save(BuildDraft());
        return GetStatus();
    }

    public IntentDiscoveryStatus PreviewDraft()
    {
        var acceptedPath = GetAcceptedIntentPath();
        var acceptedExists = File.Exists(acceptedPath);
        var acceptedPreview = acceptedExists ? LoadAcceptedPreview(acceptedPath) : "(missing)";
        var draft = BuildDraft();
        var understanding = projectUnderstandingProjectionService.Evaluate(false);
        var state = ResolveState(acceptedPath, acceptedExists, draft, understanding);

        return new IntentDiscoveryStatus(
            state,
            acceptedPath,
            acceptedExists,
            acceptedPreview,
            draft,
            false,
            "Review this preview and run `intent draft --persist` only when Runtime should create guided-planning draft truth.",
            "preview_only_intent_draft");
    }

    public IntentDiscoveryStatus InitializeFormalPlanning(string? candidateCardId = null)
    {
        var draft = draftRepository.Load() ?? throw new InvalidOperationException("No intent draft exists. Run `intent draft --persist` first.");
        draft = RebuildDraft(draft);
        EnsureNoActivePlanningSlotConflict(draft);
        return InitializeFormalPlanningCore(draft, candidateCardId);
    }

    public IntentDiscoveryStatus InitializeFormalPlanningAfterClosedSlot(string closedPlanningCardId, string? candidateCardId = null)
    {
        if (string.IsNullOrWhiteSpace(closedPlanningCardId))
        {
            throw new InvalidOperationException("Closed planning card id is required before replacing a closed planning slot.");
        }

        var draft = draftRepository.Load() ?? throw new InvalidOperationException("No intent draft exists. Run `intent draft --persist` first.");
        draft = RebuildDraft(draft);
        var activePlanningCard = draft.ActivePlanningCard
            ?? throw new InvalidOperationException("No closed active planning card exists to replace. Run `plan init` first.");
        if (!string.Equals(activePlanningCard.PlanningCardId, closedPlanningCardId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Closed planning slot replacement expected active planning card '{closedPlanningCardId}', but current active planning card is '{activePlanningCard.PlanningCardId}'.");
        }

        return InitializeFormalPlanningCore(draft, candidateCardId);
    }

    private IntentDiscoveryStatus InitializeFormalPlanningCore(IntentDiscoveryDraft draft, string? candidateCardId)
    {
        var candidate = SelectCandidateForFormalPlanning(draft, NormalizeOptionalValue(candidateCardId));
        EnsureCandidateEligibleForFormalPlanning(draft, candidate);
        var activePlanningCard = BuildActivePlanningCard(draft, candidate);
        draftRepository.Save(RebuildDraft(
            draft,
            focusCardId: candidate.CandidateCardId,
            preserveExistingFocus: false,
            activePlanningCard: activePlanningCard,
            preserveActivePlanningCard: false,
            formalPlanningStateOverride: FormalPlanningState.Planning));
        return GetStatus();
    }

    private static void EnsureNoActivePlanningSlotConflict(IntentDiscoveryDraft draft)
    {
        if (draft.ActivePlanningCard is null)
        {
            return;
        }

        throw new InvalidOperationException(ActivePlanningSlotProjectionResolver.BuildConflictExceptionMessage(draft.ActivePlanningCard));
    }

    public ActivePlanningCardExportResult ExportActivePlanningCardPayload(string outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new InvalidOperationException("Usage: plan export-card <json-path>");
        }

        var draft = draftRepository.Load() ?? throw new InvalidOperationException("No intent draft exists. Run `intent draft --persist` first.");
        draft = RebuildDraft(draft);
        var activePlanningCard = draft.ActivePlanningCard
            ?? throw new InvalidOperationException("No active planning card exists. Run `plan init` first.");
        ValidateLockedDoctrine(draft, activePlanningCard);
        var exportPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);

        var payload = new
        {
            title = activePlanningCard.OperatorIntent.Title,
            goal = activePlanningCard.OperatorIntent.Goal,
            acceptance = activePlanningCard.OperatorIntent.AcceptanceOutline,
            constraints = activePlanningCard.OperatorIntent.Constraints,
            notes = BuildExportNotes(activePlanningCard),
            planning_lineage = new PlanningLineage
            {
                PlanningSlotId = activePlanningCard.PlanningSlotId,
                ActivePlanningCardId = activePlanningCard.PlanningCardId,
                SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
                SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
                FormalPlanningState = FormalPlanningState.PlanBound,
            },
        };
        File.WriteAllText(exportPath, JsonSerializer.Serialize(payload, JsonOptions));

        var updatedPlanningCard = new ActivePlanningCard
        {
            PlanningCardId = activePlanningCard.PlanningCardId,
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
            SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
            State = activePlanningCard.State,
            LockedDoctrine = activePlanningCard.LockedDoctrine,
            OperatorIntent = activePlanningCard.OperatorIntent,
            AgentProposal = activePlanningCard.AgentProposal,
            SystemDerived = new ActivePlanningCardSystemDerived
            {
                FieldClasses = activePlanningCard.SystemDerived.FieldClasses,
                ComparisonPolicySummary = activePlanningCard.SystemDerived.ComparisonPolicySummary,
                LockedDoctrineDigest = activePlanningCard.SystemDerived.LockedDoctrineDigest,
                LastExportedAt = DateTimeOffset.UtcNow,
                LastExportedCardPayloadPath = exportPath,
            },
            IssuedAt = activePlanningCard.IssuedAt,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        draftRepository.Save(RebuildDraft(
            draft,
            activePlanningCard: updatedPlanningCard,
            preserveActivePlanningCard: false,
            formalPlanningStateOverride: FormalPlanningState.Planning));
        return new ActivePlanningCardExportResult(
            exportPath,
            updatedPlanningCard.PlanningCardId,
            updatedPlanningCard.PlanningSlotId,
            updatedPlanningCard.LockedDoctrine.Digest);
    }

    public IntentDiscoveryStatus AcceptDraft()
    {
        var draft = draftRepository.Load() ?? throw new InvalidOperationException("No intent draft exists. Run `intent draft --persist` first.");
        var acceptedPath = GetAcceptedIntentPath();
        Directory.CreateDirectory(Path.GetDirectoryName(acceptedPath)!);
        File.WriteAllText(acceptedPath, draft.SuggestedMarkdown);
        draftRepository.Delete();
        return GetStatus();
    }

    public IntentDiscoveryStatus DiscardDraft()
    {
        draftRepository.Delete();
        return GetStatus();
    }

    public IntentDiscoveryStatus SetFocusCard(string? candidateCardId)
    {
        var normalizedCandidateCardId = NormalizeOptionalValue(candidateCardId);
        draftRepository.Update(draft =>
        {
            if (normalizedCandidateCardId is not null
                && draft.CandidateCards.All(item => !string.Equals(item.CandidateCardId, normalizedCandidateCardId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Unknown candidate card '{normalizedCandidateCardId}'.");
            }

            return RebuildDraft(
                draft,
                focusCardId: normalizedCandidateCardId,
                preserveExistingFocus: false,
                preserveActivePlanningCard: false);
        });

        return GetStatus();
    }

    public IntentDiscoveryStatus SetPendingDecisionStatus(string decisionId, GuidedPlanningDecisionStatus status)
    {
        if (string.IsNullOrWhiteSpace(decisionId))
        {
            throw new InvalidOperationException("Usage: intent decision <decision-id> <open|resolved|paused|forbidden>");
        }

        draftRepository.Update(draft =>
        {
            if (draft.PendingDecisions.All(item => !string.Equals(item.DecisionId, decisionId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Unknown pending decision '{decisionId}'.");
            }

            var updatedDecisions = draft.PendingDecisions
                .Select(item => string.Equals(item.DecisionId, decisionId, StringComparison.Ordinal)
                    ? new GuidedPlanningPendingDecision
                    {
                        DecisionId = item.DecisionId,
                        Title = item.Title,
                        WhyItMatters = item.WhyItMatters,
                        Options = item.Options,
                        CurrentRecommendation = item.CurrentRecommendation,
                        BlockingLevel = item.BlockingLevel,
                        Status = status,
                    }
                    : item)
                .ToArray();

            return RebuildDraft(
                draft,
                pendingDecisions: updatedDecisions,
                preserveActivePlanningCard: false);
        });

        return GetStatus();
    }

    public IntentDiscoveryStatus SetCandidateCardPosture(string candidateCardId, GuidedPlanningPosture posture)
    {
        if (string.IsNullOrWhiteSpace(candidateCardId))
        {
            throw new InvalidOperationException("Usage: intent candidate <candidate-card-id> <emerging|needs_confirmation|wobbling|grounded|paused|forbidden|ready_to_plan>");
        }

        draftRepository.Update(draft =>
        {
            if (draft.CandidateCards.All(item => !string.Equals(item.CandidateCardId, candidateCardId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException($"Unknown candidate card '{candidateCardId}'.");
            }

            var blockingDecisions = GetBlockingOpenDecisions(draft.PendingDecisions);
            if (posture == GuidedPlanningPosture.ReadyToPlan && blockingDecisions.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Candidate cards cannot move to ready_to_plan while blocking guided-planning decisions remain open: {string.Join(", ", blockingDecisions.Select(item => item.DecisionId))}.");
            }

            var updatedCandidates = draft.CandidateCards
                .Select(item => string.Equals(item.CandidateCardId, candidateCardId, StringComparison.Ordinal)
                    ? new GuidedPlanningCandidateCard
                    {
                        CandidateCardId = item.CandidateCardId,
                        Title = item.Title,
                        Summary = item.Summary,
                        PlanningPosture = posture,
                        WritebackEligibility = item.WritebackEligibility,
                        FocusQuestions = item.FocusQuestions,
                        AllowedUserActions = item.AllowedUserActions,
                    }
                    : item)
                .ToArray();

            return RebuildDraft(
                draft,
                candidateCards: updatedCandidates,
                preserveActivePlanningCard: false);
        });

        return GetStatus();
    }

    private string GetAcceptedIntentPath()
    {
        return Path.Combine(paths.AiRoot, "memory", "PROJECT.md");
    }

    private static IntentDiscoveryState ResolveState(
        string acceptedPath,
        bool acceptedExists,
        IntentDiscoveryDraft? draft,
        ProjectUnderstandingProjection understanding)
    {
        if (acceptedExists)
        {
            if (IsAcceptedIntentStale(acceptedPath, understanding))
            {
                return IntentDiscoveryState.Stale;
            }

            return IntentDiscoveryState.Accepted;
        }

        return draft is null ? IntentDiscoveryState.Missing : IntentDiscoveryState.Drafted;
    }

    private static bool IsAcceptedIntentStale(string acceptedPath, ProjectUnderstandingProjection understanding)
    {
        var content = File.ReadAllText(acceptedPath);
        var requiredSections = new[] { "## Purpose", "## Users", "## Core Capabilities", "## Technology Scope" };
        if (requiredSections.Any(section => !content.Contains(section, StringComparison.Ordinal)))
        {
            return true;
        }

        if (understanding.GeneratedAt is null)
        {
            return false;
        }

        return File.GetLastWriteTimeUtc(acceptedPath) < understanding.GeneratedAt.Value.UtcDateTime;
    }

    private static string BuildRecommendedNextAction(IntentDiscoveryState state)
    {
        return state switch
        {
            IntentDiscoveryState.Missing => "Run `intent draft --persist` to create a governed project-intent candidate.",
            IntentDiscoveryState.Drafted => "Review the existing intent draft and explicitly accept or discard it.",
            IntentDiscoveryState.Accepted => "Continue through cards, tasks, execution, and review with the accepted intent as reference.",
            IntentDiscoveryState.Stale => "Refresh intent understanding and then explicitly rewrite PROJECT.md only if the draft is accepted.",
            _ => "Inspect the current intent state.",
        };
    }

    private IntentDiscoveryDraft BuildDraft()
    {
        var projectName = Path.GetFileName(repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var understanding = projectUnderstandingProjectionService.Evaluate(false);
        var purpose = LoadReadmeSummary();
        if (string.IsNullOrWhiteSpace(purpose))
        {
            purpose = $"Clarify the purpose and scope of {projectName} before expanding governed execution work.";
        }

        var users = new[]
        {
            $"Developers operating and evolving {projectName}.",
            "AI planners and workers collaborating through CARVES-governed tasks.",
        };
        var coreCapabilities = understanding.ModuleSummaries.Count > 0
            ? understanding.ModuleSummaries.Take(3).ToArray()
            : new[]
            {
                "Capture project intent before planning new work.",
                "Translate intent into cards, tasks, execution, and review.",
                "Keep runtime truth, planner truth, and worker truth explainable.",
            };
        var technologyScope = ResolveTechnologyScope(projectName, understanding);
        var scopeFrame = BuildScopeFrame(purpose, users, coreCapabilities, technologyScope);
        var pendingDecisions = BuildPendingDecisions(projectName);
        var candidateCards = MergeCandidateCards(
            BuildCandidateCards(projectName, coreCapabilities),
            BuildAcceptedFollowUpCandidateCards());

        return RebuildDraft(new IntentDiscoveryDraft
        {
            RepoRoot = repoRoot,
            ProjectName = projectName,
            Purpose = purpose,
            Users = users,
            CoreCapabilities = coreCapabilities,
            TechnologyScope = technologyScope,
            SourceSummary = $"readme={(string.IsNullOrWhiteSpace(LoadReadmeSummary()) ? "missing" : "present")}; codegraph={understanding.State.ToString().ToLowerInvariant()}; stage={RuntimeStageInfo.CurrentStage}",
            SuggestedMarkdown = BuildMarkdown(projectName, purpose, users, coreCapabilities, technologyScope),
            PlanningPosture = GuidedPlanningPosture.NeedsConfirmation,
            FocusCardId = null,
            ScopeFrame = scopeFrame,
            PendingDecisions = pendingDecisions,
            CandidateCards = candidateCards,
        });
    }

    private string LoadReadmeSummary()
    {
        var readmePath = Path.Combine(repoRoot, "README.md");
        if (!File.Exists(readmePath))
        {
            return string.Empty;
        }

        foreach (var line in File.ReadLines(readmePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            return trimmed;
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveTechnologyScope(string projectName, ProjectUnderstandingProjection understanding)
    {
        var items = new List<string>();
        if (understanding.ModuleCount > 0)
        {
            items.Add($"{understanding.ModuleCount} modules indexed through the CARVES codegraph.");
        }

        if (understanding.FileCount > 0)
        {
            items.Add($"{understanding.FileCount} files tracked by runtime code understanding.");
        }

        items.Add($"Repository focus: {projectName}.");
        return items;
    }

    private static GuidedPlanningScopeFrame BuildScopeFrame(
        string purpose,
        IReadOnlyList<string> users,
        IReadOnlyList<string> coreCapabilities,
        IReadOnlyList<string> technologyScope)
    {
        return new GuidedPlanningScopeFrame
        {
            Goal = purpose,
            FirstUsers = users,
            ValidationArtifact = "An explicitly accepted PROJECT.md plus the first grounded card with reviewable acceptance criteria.",
            MustHave = coreCapabilities,
            NiceToHave =
            [
                "Candidate-card graph projection in a downstream Operator shell.",
                "Focus-based clarification continuity through focus_card_id.",
            ],
            NotNow =
            [
                "Automatic official card writeback from free-form chat.",
                "Unity-based guided planning shell in v1.",
            ],
            Constraints =
            [
                "Runtime remains the only official card and task truth ingress.",
                "Candidate cards stay draft-local until explicitly grounded.",
                "Mermaid may project planning views but does not become canonical truth.",
                .. technologyScope,
            ],
            OpenQuestions =
            [
                "What is the first bounded slice that should become a grounded card?",
                "What validation artifact would make the first slice feel complete?",
            ],
        };
    }

    private static IReadOnlyList<GuidedPlanningPendingDecision> BuildPendingDecisions(string projectName)
    {
        return
        [
            new GuidedPlanningPendingDecision
            {
                DecisionId = "first_validation_artifact",
                Title = "Choose the first validation artifact",
                WhyItMatters = "Guided planning needs one explicit proof target before a candidate card can be grounded.",
                Options =
                [
                    "User-visible workflow proof",
                    "Host and runtime truth proof",
                    "Packaging and onboarding proof",
                ],
                CurrentRecommendation = $"Start with the smallest validation artifact that proves {projectName} can move from intent into one governed slice.",
                BlockingLevel = "blocking_for_grounded_card",
                Status = GuidedPlanningDecisionStatus.Open,
            },
            new GuidedPlanningPendingDecision
            {
                DecisionId = "first_slice_boundary",
                Title = "Choose the first bounded slice",
                WhyItMatters = "Candidate cards should stay narrow enough to ground into one official card without scope drift.",
                Options =
                [
                    "Clarify the initialization slice",
                    "Clarify the first user-visible outcome",
                    "Clarify the first runtime boundary to freeze",
                ],
                CurrentRecommendation = "Ground the smallest slice that can be validated without widening product-shell scope.",
                BlockingLevel = "blocking_for_writeback",
                Status = GuidedPlanningDecisionStatus.Open,
            },
        ];
    }

    private static IReadOnlyList<GuidedPlanningCandidateCard> BuildCandidateCards(string projectName, IReadOnlyList<string> coreCapabilities)
    {
        var topCapability = coreCapabilities.FirstOrDefault() ?? $"Clarify the first bounded slice for {projectName}.";
        return
        [
            new GuidedPlanningCandidateCard
            {
                CandidateCardId = "candidate-intent-foundation",
                Title = $"Ground {projectName} purpose and first proof target",
                Summary = "Stabilize purpose, first users, validation artifact, and constraints before official card writeback.",
                PlanningPosture = GuidedPlanningPosture.NeedsConfirmation,
                WritebackEligibility = "requires_grounding_then_host_writeback",
                FocusQuestions =
                [
                    "Who is the first real user for this project?",
                    "What proof would make the first slice feel complete?",
                ],
                AllowedUserActions =
                [
                    "confirm",
                    "pause",
                    "forbid",
                    "split",
                ],
            },
            new GuidedPlanningCandidateCard
            {
                CandidateCardId = "candidate-first-slice",
                Title = $"Freeze the first bounded slice for {projectName}",
                Summary = $"Take the current strongest capability signal and convert it into one reviewable grounded slice: {topCapability}",
                PlanningPosture = GuidedPlanningPosture.NeedsConfirmation,
                WritebackEligibility = "requires_grounding_then_host_writeback",
                FocusQuestions =
                [
                    "Which part of the first slice is strictly must-have?",
                    "What should explicitly stay out of scope for the first card?",
                ],
                AllowedUserActions =
                [
                    "confirm",
                    "pause",
                    "forbid",
                    "split",
                ],
            },
        ];
    }

    private IReadOnlyList<GuidedPlanningCandidateCard> BuildAcceptedFollowUpCandidateCards()
    {
        var problemRecords = ListPilotProblemIntakeRecords();
        if (problemRecords.Count == 0)
        {
            return Array.Empty<GuidedPlanningCandidateCard>();
        }

        RuntimeAgentProblemFollowUpPlanningIntakeSurface planningIntake;
        try
        {
            planningIntake = new RuntimeAgentProblemFollowUpPlanningIntakeService(repoRoot, () => problemRecords).Build();
        }
        catch
        {
            return Array.Empty<GuidedPlanningCandidateCard>();
        }

        if (!planningIntake.PlanningIntakeReady)
        {
            return Array.Empty<GuidedPlanningCandidateCard>();
        }

        return planningIntake.PlanningItems
            .Where(static item => item.Actionable && !string.IsNullOrWhiteSpace(item.CandidateId))
            .Select(BuildAcceptedFollowUpCandidateCard)
            .ToArray();
    }

    private IReadOnlyList<PilotProblemIntakeRecord> ListPilotProblemIntakeRecords()
    {
        var problemRoot = Path.Combine(paths.RuntimeRoot, "pilot-problems");
        if (!Directory.Exists(problemRoot))
        {
            return Array.Empty<PilotProblemIntakeRecord>();
        }

        return Directory.EnumerateFiles(problemRoot, "*.json", SearchOption.TopDirectoryOnly)
            .Select(TryReadPilotProblemIntakeRecord)
            .OfType<PilotProblemIntakeRecord>()
            .OrderByDescending(static record => record.RecordedAtUtc)
            .ThenByDescending(static record => record.ProblemId, StringComparer.Ordinal)
            .ToArray();
    }

    private static PilotProblemIntakeRecord? TryReadPilotProblemIntakeRecord(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<PilotProblemIntakeRecord>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static GuidedPlanningCandidateCard BuildAcceptedFollowUpCandidateCard(
        RuntimeAgentProblemFollowUpPlanningIntakeItemSurface item)
    {
        return new GuidedPlanningCandidateCard
        {
            CandidateCardId = item.CandidateId,
            Title = item.SuggestedTitle,
            Summary = BuildAcceptedFollowUpCandidateSummary(item),
            PlanningPosture = GuidedPlanningPosture.ReadyToPlan,
            WritebackEligibility = "requires_grounding_then_host_writeback",
            FocusQuestions = BuildAcceptedFollowUpFocusQuestions(item),
            AllowedUserActions =
            [
                "confirm",
                "pause",
                "forbid",
                "split",
            ],
        };
    }

    private static string BuildAcceptedFollowUpCandidateSummary(RuntimeAgentProblemFollowUpPlanningIntakeItemSurface item)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(item.SuggestedIntent))
        {
            parts.Add(item.SuggestedIntent.Trim());
        }

        if (!string.IsNullOrWhiteSpace(item.SuggestedAcceptanceEvidence))
        {
            parts.Add($"Accepted follow-up evidence: {item.SuggestedAcceptanceEvidence.Trim()}");
        }

        if (item.DecisionRecordIds.Count > 0)
        {
            parts.Add($"Decision records: {string.Join(", ", item.DecisionRecordIds)}.");
        }

        return parts.Count == 0
            ? $"Accepted follow-up planning input {item.CandidateId}."
            : string.Join(" ", parts);
    }

    private static IReadOnlyList<string> BuildAcceptedFollowUpFocusQuestions(
        RuntimeAgentProblemFollowUpPlanningIntakeItemSurface item)
    {
        var questions = new List<string>
        {
            "Which accepted follow-up evidence must become acceptance criteria?",
            "What should stay out of scope for this follow-up planning card?",
        };

        questions.AddRange(item.PlanningRequirements
            .Where(static requirement => !string.IsNullOrWhiteSpace(requirement))
            .Select(static requirement => requirement.Trim()));

        return questions
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<GuidedPlanningCandidateCard> MergeCandidateCards(
        IReadOnlyList<GuidedPlanningCandidateCard> baseCandidates,
        IReadOnlyList<GuidedPlanningCandidateCard> followUpCandidates)
    {
        return baseCandidates
            .Concat(followUpCandidates)
            .GroupBy(static candidate => candidate.CandidateCardId, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    private static string BuildMarkdown(
        string projectName,
        string purpose,
        IReadOnlyList<string> users,
        IReadOnlyList<string> coreCapabilities,
        IReadOnlyList<string> technologyScope)
    {
        var lines = new List<string>
        {
            $"# {projectName} Intent",
            string.Empty,
            "## Purpose",
            purpose,
            string.Empty,
            "## Users",
        };
        lines.AddRange(users.Select(user => $"- {user}"));
        lines.Add(string.Empty);
        lines.Add("## Core Capabilities");
        lines.AddRange(coreCapabilities.Select(capability => $"- {capability}"));
        lines.Add(string.Empty);
        lines.Add("## Technology Scope");
        lines.AddRange(technologyScope.Select(scope => $"- {scope}"));
        lines.Add(string.Empty);
        lines.Add("> Generated as a CARVES intent draft. Accept explicitly before treating it as durable truth.");
        return string.Join(Environment.NewLine, lines);
    }

    private static string LoadAcceptedPreview(string acceptedPath)
    {
        return string.Join(
            Environment.NewLine,
            File.ReadLines(acceptedPath).Take(12).Select(line => line.TrimEnd()));
    }

    private static IntentDiscoveryDraft RebuildDraft(
        IntentDiscoveryDraft draft,
        IReadOnlyList<GuidedPlanningPendingDecision>? pendingDecisions = null,
        IReadOnlyList<GuidedPlanningCandidateCard>? candidateCards = null,
        string? focusCardId = null,
        bool preserveExistingFocus = true,
        ActivePlanningCard? activePlanningCard = null,
        bool preserveActivePlanningCard = true,
        FormalPlanningState? formalPlanningStateOverride = null)
    {
        var nextDecisions = pendingDecisions?.ToArray() ?? draft.PendingDecisions.ToArray();
        var nextCandidates = candidateCards?.ToArray() ?? draft.CandidateCards.ToArray();
        var nextFocusCardId = preserveExistingFocus ? draft.FocusCardId : focusCardId;
        if (!string.IsNullOrWhiteSpace(nextFocusCardId)
            && nextCandidates.All(item => !string.Equals(item.CandidateCardId, nextFocusCardId, StringComparison.Ordinal)))
        {
            nextFocusCardId = null;
        }

        var nextPlanningPosture = ResolveDraftPlanningPosture(nextCandidates, nextDecisions);
        var nextActivePlanningCard = preserveActivePlanningCard ? draft.ActivePlanningCard : activePlanningCard;
        var nextFormalPlanningState = formalPlanningStateOverride
            ?? ResolveFormalPlanningState(nextPlanningPosture, nextActivePlanningCard);
        return new IntentDiscoveryDraft
        {
            SchemaVersion = draft.SchemaVersion,
            DraftId = draft.DraftId,
            RepoRoot = draft.RepoRoot,
            ProjectName = draft.ProjectName,
            Purpose = draft.Purpose,
            Users = draft.Users,
            CoreCapabilities = draft.CoreCapabilities,
            TechnologyScope = draft.TechnologyScope,
            SourceSummary = draft.SourceSummary,
            SuggestedMarkdown = draft.SuggestedMarkdown,
            RecommendedNextAction = BuildDraftRecommendedNextAction(nextPlanningPosture, nextFocusCardId, nextCandidates, nextDecisions, nextActivePlanningCard),
            PlanningPosture = nextPlanningPosture,
            FormalPlanningState = nextFormalPlanningState,
            FocusCardId = nextFocusCardId,
            ScopeFrame = draft.ScopeFrame,
            PendingDecisions = nextDecisions,
            CandidateCards = nextCandidates,
            ActivePlanningCard = nextActivePlanningCard,
            GeneratedAt = draft.GeneratedAt,
        };
    }

    private static GuidedPlanningPosture ResolveDraftPlanningPosture(
        IReadOnlyList<GuidedPlanningCandidateCard> candidateCards,
        IReadOnlyList<GuidedPlanningPendingDecision> pendingDecisions)
    {
        if (candidateCards.Count == 0)
        {
            return GuidedPlanningPosture.Emerging;
        }

        if (candidateCards.Any(item => item.PlanningPosture == GuidedPlanningPosture.ReadyToPlan))
        {
            return GuidedPlanningPosture.ReadyToPlan;
        }

        if (candidateCards.Any(item => item.PlanningPosture == GuidedPlanningPosture.Wobbling))
        {
            return GuidedPlanningPosture.Wobbling;
        }

        if (candidateCards.All(item => item.PlanningPosture == GuidedPlanningPosture.Forbidden))
        {
            return GuidedPlanningPosture.Forbidden;
        }

        if (candidateCards.All(item => item.PlanningPosture is GuidedPlanningPosture.Paused or GuidedPlanningPosture.Forbidden)
            && candidateCards.Any(item => item.PlanningPosture == GuidedPlanningPosture.Paused))
        {
            return GuidedPlanningPosture.Paused;
        }

        if (candidateCards.Any(item => item.PlanningPosture == GuidedPlanningPosture.Grounded)
            && GetBlockingOpenDecisions(pendingDecisions).Count == 0)
        {
            return GuidedPlanningPosture.Grounded;
        }

        if (candidateCards.Any(item => item.PlanningPosture == GuidedPlanningPosture.Emerging))
        {
            return GuidedPlanningPosture.Emerging;
        }

        return GuidedPlanningPosture.NeedsConfirmation;
    }

    private static FormalPlanningState ResolveFormalPlanningState(
        GuidedPlanningPosture planningPosture,
        ActivePlanningCard? activePlanningCard)
    {
        if (activePlanningCard is not null)
        {
            return activePlanningCard.State;
        }

        return planningPosture is GuidedPlanningPosture.ReadyToPlan or GuidedPlanningPosture.Grounded
            ? FormalPlanningState.PlanInitRequired
            : FormalPlanningState.Discuss;
    }

    private static GuidedPlanningCandidateCard SelectCandidateForFormalPlanning(IntentDiscoveryDraft draft, string? candidateCardId)
    {
        if (!string.IsNullOrWhiteSpace(candidateCardId))
        {
            return draft.CandidateCards.FirstOrDefault(item => string.Equals(item.CandidateCardId, candidateCardId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Unknown candidate card '{candidateCardId}'.");
        }

        var focusedCandidate = !string.IsNullOrWhiteSpace(draft.FocusCardId)
            ? draft.CandidateCards.FirstOrDefault(item => string.Equals(item.CandidateCardId, draft.FocusCardId, StringComparison.Ordinal))
            : null;
        if (focusedCandidate is not null)
        {
            return focusedCandidate;
        }

        return draft.CandidateCards.FirstOrDefault(item => item.PlanningPosture == GuidedPlanningPosture.ReadyToPlan)
            ?? draft.CandidateCards.FirstOrDefault(item => item.PlanningPosture == GuidedPlanningPosture.Grounded)
            ?? throw new InvalidOperationException("No guided-planning candidate is ready for formal planning. Resolve the blocking decisions and move one candidate to `grounded` or `ready_to_plan` first.");
    }

    private static void EnsureCandidateEligibleForFormalPlanning(IntentDiscoveryDraft draft, GuidedPlanningCandidateCard candidate)
    {
        if (candidate.PlanningPosture is GuidedPlanningPosture.Forbidden or GuidedPlanningPosture.Paused)
        {
            throw new InvalidOperationException($"Candidate '{candidate.CandidateCardId}' is in posture '{JsonNamingPolicy.SnakeCaseLower.ConvertName(candidate.PlanningPosture.ToString())}' and cannot enter formal planning.");
        }

        var blockingDecisions = GetBlockingOpenDecisions(draft.PendingDecisions);
        if (blockingDecisions.Count > 0)
        {
            throw new InvalidOperationException(
                $"Formal planning cannot start while blocking guided-planning decisions remain open: {string.Join(", ", blockingDecisions.Select(item => item.DecisionId))}.");
        }

        if (candidate.PlanningPosture is not GuidedPlanningPosture.ReadyToPlan and not GuidedPlanningPosture.Grounded)
        {
            throw new InvalidOperationException(
                $"Candidate '{candidate.CandidateCardId}' must be `grounded` or `ready_to_plan` before `plan init` can issue an active planning card.");
        }
    }

    private static ActivePlanningCard BuildActivePlanningCard(IntentDiscoveryDraft draft, GuidedPlanningCandidateCard candidate)
    {
        var planningCardId = $"PLANCARD-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var lockedDoctrineLines = PlanningCardInvariantService.BuildCanonicalLiteralLines(candidate.CandidateCardId);
        var lockedDoctrineDigest = PlanningCardInvariantService.ComputeDigest(lockedDoctrineLines);
        return new ActivePlanningCard
        {
            PlanningCardId = planningCardId,
            PlanningSlotId = PrimaryPlanningSlotId,
            SourceIntentDraftId = draft.DraftId,
            SourceCandidateCardId = candidate.CandidateCardId,
            State = FormalPlanningState.Planning,
            LockedDoctrine = new ActivePlanningCardLockedDoctrine
            {
                LiteralLines = lockedDoctrineLines,
                CompareRule = PlanningCardInvariantService.CompareRule,
                Digest = lockedDoctrineDigest,
            },
            OperatorIntent = new ActivePlanningCardOperatorIntent
            {
                Title = candidate.Title,
                Goal = draft.ScopeFrame.Goal,
                ValidationArtifact = draft.ScopeFrame.ValidationArtifact,
                AcceptanceOutline =
                [
                    $"Freeze one bounded slice around '{candidate.Title}'.",
                    $"Preserve `{draft.ScopeFrame.ValidationArtifact}` as the explicit proof target.",
                    "Keep official card truth host-routed and single-lane.",
                ],
                Constraints = draft.ScopeFrame.Constraints,
                NonGoals = draft.ScopeFrame.NotNow,
            },
            AgentProposal = new ActivePlanningCardAgentProposal
            {
                CandidateSummary = candidate.Summary,
                DecompositionCandidates = candidate.FocusQuestions,
                OpenQuestions = draft.ScopeFrame.OpenQuestions,
                SuggestedNextAction = $"Fill the editable fields for '{candidate.Title}' and export a card payload through `plan export-card`.",
            },
            SystemDerived = new ActivePlanningCardSystemDerived
            {
                FieldClasses = BuildPlanningFieldClasses(),
                ComparisonPolicySummary = "locked doctrine uses canonical invariant blocks with literal and digest compare; operator intent and agent proposal are schema-edited; system-derived fields are host-owned",
                LockedDoctrineDigest = lockedDoctrineDigest,
            },
        };
    }

    private static IReadOnlyList<PlanningFieldClassRule> BuildPlanningFieldClasses()
    {
        return
        [
            new PlanningFieldClassRule
            {
                FieldPath = "locked_doctrine.literal_lines",
                Ownership = "system",
                EditPolicy = "locked",
                CompareRule = "literal_compare",
            },
            new PlanningFieldClassRule
            {
                FieldPath = "locked_doctrine.digest",
                Ownership = "system",
                EditPolicy = "locked",
                CompareRule = "digest_compare",
            },
            new PlanningFieldClassRule
            {
                FieldPath = "operator_intent.*",
                Ownership = "operator_or_agent_assist",
                EditPolicy = "schema_editable",
                CompareRule = "schema_compare",
            },
            new PlanningFieldClassRule
            {
                FieldPath = "agent_proposal.*",
                Ownership = "agent_assist",
                EditPolicy = "schema_editable",
                CompareRule = "schema_compare",
            },
            new PlanningFieldClassRule
            {
                FieldPath = "system_derived.*",
                Ownership = "system",
                EditPolicy = "host_only",
                CompareRule = "derived_compare",
            },
        ];
    }

    private static IReadOnlyList<string> BuildExportNotes(ActivePlanningCard activePlanningCard)
    {
        return
        [
            "Generated from `plan export-card`.",
            $"active_planning_card_id={activePlanningCard.PlanningCardId}",
            $"planning_slot_id={activePlanningCard.PlanningSlotId}",
            $"source_intent_draft_id={activePlanningCard.SourceIntentDraftId}",
            $"source_candidate_card_id={activePlanningCard.SourceCandidateCardId ?? "(none)"}",
            "Editable fields before `create-card-draft`: title, goal, acceptance, constraints, notes.",
        ];
    }

    private static void ValidateLockedDoctrine(IntentDiscoveryDraft draft, ActivePlanningCard activePlanningCard)
    {
        PlanningCardInvariantService.ValidateOrThrow(draft, activePlanningCard);
    }

    private static string BuildDraftRecommendedNextAction(
        GuidedPlanningPosture planningPosture,
        string? focusCardId,
        IReadOnlyList<GuidedPlanningCandidateCard> candidateCards,
        IReadOnlyList<GuidedPlanningPendingDecision> pendingDecisions,
        ActivePlanningCard? activePlanningCard)
    {
        var blockingDecisions = GetBlockingOpenDecisions(pendingDecisions);
        var focusedCandidate = string.IsNullOrWhiteSpace(focusCardId)
            ? null
            : candidateCards.FirstOrDefault(item => string.Equals(item.CandidateCardId, focusCardId, StringComparison.Ordinal));

        if (activePlanningCard is not null)
        {
            return "Use `plan export-card <json-path>` to issue a host-routed card payload from the active planning card before creating official card truth.";
        }

        if (planningPosture == GuidedPlanningPosture.ReadyToPlan)
        {
            return "Use `plan init [candidate-card-id]` to issue one active planning card before crossing into official card truth. PROJECT.md still remains an explicit `intent accept` path.";
        }

        if (focusedCandidate is not null && blockingDecisions.Count > 0)
        {
            return $"Keep clarifying '{focusedCandidate.Title}' and resolve blocking guided-planning decisions before moving it to `ready_to_plan`.";
        }

        if (focusedCandidate is not null)
        {
            return $"Keep the conversation scoped to '{focusedCandidate.Title}' until the slice feels grounded enough for Host-routed writeback.";
        }

        if (blockingDecisions.Count > 0)
        {
            return "Review the intent draft, pick a candidate card to focus, and resolve or pause the remaining blocking guided-planning decisions before official writeback.";
        }

        if (candidateCards.Any(item => item.PlanningPosture == GuidedPlanningPosture.Grounded))
        {
            return "Select the grounded candidate and move it through Host-routed card writeback when the slice is ready.";
        }

        return "Pick a candidate card to focus, keep the plan draft-local, and use `intent accept` only when PROJECT.md should become durable project intent.";
    }

    private static List<GuidedPlanningPendingDecision> GetBlockingOpenDecisions(IReadOnlyList<GuidedPlanningPendingDecision> pendingDecisions)
    {
        return pendingDecisions
            .Where(item =>
                item.Status == GuidedPlanningDecisionStatus.Open
                && item.BlockingLevel.StartsWith("blocking_", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Equals("none", StringComparison.OrdinalIgnoreCase)
               || value.Equals("clear", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }
}

public sealed record ActivePlanningCardExportResult(
    string OutputPath,
    string PlanningCardId,
    string PlanningSlotId,
    string LockedDoctrineDigest);
