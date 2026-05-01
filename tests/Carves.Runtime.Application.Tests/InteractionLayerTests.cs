using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using Carves.Runtime.Domain.Planning;
using System.Text.Json;

namespace Carves.Runtime.Application.Tests;

public sealed class InteractionLayerTests
{
    [Fact]
    public void IntentDiscovery_GeneratesDraftWithoutRewritingAcceptedProjectTruth()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        var status = service.GenerateDraft();

        Assert.Equal(IntentDiscoveryState.Drafted, status.State);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, ".ai", "runtime", "intent_draft.json")));
        Assert.False(File.Exists(Path.Combine(workspace.RootPath, ".ai", "memory", "PROJECT.md")));
        Assert.NotNull(status.Draft);
        Assert.Contains("## Purpose", status.Draft!.SuggestedMarkdown, StringComparison.Ordinal);
        Assert.Equal(GuidedPlanningPosture.NeedsConfirmation, status.Draft.PlanningPosture);
        Assert.Equal(status.Draft.Purpose, status.Draft.ScopeFrame.Goal);
        Assert.Contains("Automatic official card writeback from free-form chat.", status.Draft.ScopeFrame.NotNow, StringComparer.Ordinal);
        Assert.Contains(status.Draft.PendingDecisions, item => item.DecisionId == "first_validation_artifact" && item.BlockingLevel == "blocking_for_grounded_card" && item.Status == GuidedPlanningDecisionStatus.Open);
        Assert.Contains(status.Draft.CandidateCards, item => item.CandidateCardId == "candidate-first-slice" && item.WritebackEligibility == "requires_grounding_then_host_writeback");
    }

    [Fact]
    public void IntentDiscovery_MutatesFocusDecisionAndCandidatePostureOnExistingDraftLane()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        var status = service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);

        Assert.Equal(IntentDiscoveryState.Drafted, status.State);
        Assert.NotNull(status.Draft);
        Assert.Equal("candidate-first-slice", status.Draft!.FocusCardId);
        Assert.Equal(GuidedPlanningPosture.ReadyToPlan, status.Draft.PlanningPosture);
        Assert.Contains(status.Draft.PendingDecisions, item => item.DecisionId == "first_validation_artifact" && item.Status == GuidedPlanningDecisionStatus.Resolved);
        Assert.Contains(status.Draft.CandidateCards, item => item.CandidateCardId == "candidate-first-slice" && item.PlanningPosture == GuidedPlanningPosture.ReadyToPlan);
        Assert.Contains("plan init", status.Draft.RecommendedNextAction, StringComparison.Ordinal);
    }

    [Fact]
    public void IntentDiscovery_RejectsReadyToPlanWhileBlockingDecisionRemainsOpen()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);

        var error = Assert.Throws<InvalidOperationException>(() => service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan));

        Assert.Contains("blocking guided-planning decisions remain open", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IntentDiscovery_InitializeFormalPlanning_IssuesActivePlanningCard()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);

        var status = service.InitializeFormalPlanning();

        Assert.NotNull(status.Draft);
        Assert.Equal(FormalPlanningState.Planning, status.Draft!.FormalPlanningState);
        Assert.NotNull(status.Draft.ActivePlanningCard);
        Assert.Equal("primary_formal_planning", status.Draft.ActivePlanningCard!.PlanningSlotId);
        Assert.Equal("candidate-first-slice", status.Draft.ActivePlanningCard.SourceCandidateCardId);
        Assert.Equal(status.Draft.ActivePlanningCard.LockedDoctrine.Digest, status.Draft.ActivePlanningCard.SystemDerived.LockedDoctrineDigest);
        Assert.Contains("plan export-card", status.Draft.RecommendedNextAction, StringComparison.Ordinal);
    }

    [Fact]
    public void IntentDiscovery_InitializeFormalPlanning_RejectsSecondActivePlanningCard()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        var initialized = service.InitializeFormalPlanning();

        var error = Assert.Throws<InvalidOperationException>(() => service.InitializeFormalPlanning("candidate-first-slice"));

        Assert.Contains("already occupied", error.Message, StringComparison.Ordinal);
        Assert.Contains("opening another active planning card is rejected", error.Message, StringComparison.Ordinal);
        Assert.Contains(initialized.Draft!.ActivePlanningCard!.PlanningCardId, error.Message, StringComparison.Ordinal);
        Assert.Contains("plan status", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void IntentDiscovery_GuidedPlanningMutation_InvalidatesActivePlanningCard()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        service.InitializeFormalPlanning();

        var status = service.SetFocusCard("candidate-intent-foundation");

        Assert.NotNull(status.Draft);
        Assert.Null(status.Draft!.ActivePlanningCard);
        Assert.Equal(FormalPlanningState.PlanInitRequired, status.Draft.FormalPlanningState);
    }

    [Fact]
    public void IntentDiscovery_ExportActivePlanningCardPayload_RejectsLockedDoctrineDrift()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var repository = new JsonIntentDraftRepository(paths);
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, repository, understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        var initialized = service.InitializeFormalPlanning();
        repository.Update(draft => new IntentDiscoveryDraft
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
            RecommendedNextAction = draft.RecommendedNextAction,
            PlanningPosture = draft.PlanningPosture,
            FormalPlanningState = draft.FormalPlanningState,
            FocusCardId = draft.FocusCardId,
            ScopeFrame = draft.ScopeFrame,
            PendingDecisions = draft.PendingDecisions,
            CandidateCards = draft.CandidateCards,
            ActivePlanningCard = new ActivePlanningCard
            {
                PlanningCardId = initialized.Draft!.ActivePlanningCard!.PlanningCardId,
                PlanningSlotId = initialized.Draft.ActivePlanningCard.PlanningSlotId,
                SourceIntentDraftId = initialized.Draft.ActivePlanningCard.SourceIntentDraftId,
                SourceCandidateCardId = initialized.Draft.ActivePlanningCard.SourceCandidateCardId,
                State = initialized.Draft.ActivePlanningCard.State,
                LockedDoctrine = new ActivePlanningCardLockedDoctrine
                {
                    LiteralLines = ["tampered doctrine line"],
                    CompareRule = initialized.Draft.ActivePlanningCard.LockedDoctrine.CompareRule,
                    Digest = initialized.Draft.ActivePlanningCard.LockedDoctrine.Digest,
                },
                OperatorIntent = initialized.Draft.ActivePlanningCard.OperatorIntent,
                AgentProposal = initialized.Draft.ActivePlanningCard.AgentProposal,
                SystemDerived = initialized.Draft.ActivePlanningCard.SystemDerived,
                IssuedAt = initialized.Draft.ActivePlanningCard.IssuedAt,
                UpdatedAt = initialized.Draft.ActivePlanningCard.UpdatedAt,
            },
            GeneratedAt = draft.GeneratedAt,
        });

        var error = Assert.Throws<InvalidOperationException>(() => service.ExportActivePlanningCardPayload(Path.Combine(workspace.RootPath, "drafts", "plan-card.json")));

        Assert.Contains("locked doctrine drifted", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("plan init candidate-first-slice", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanningCardInvariantService_EvaluatesCanonicalBlocksAndDrift()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        var initialized = service.InitializeFormalPlanning();
        var activePlanningCard = initialized.Draft!.ActivePlanningCard!;

        var valid = PlanningCardInvariantService.Evaluate(initialized.Draft, activePlanningCard);
        var driftedCard = new ActivePlanningCard
        {
            PlanningCardId = activePlanningCard.PlanningCardId,
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
            SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
            State = activePlanningCard.State,
            LockedDoctrine = new ActivePlanningCardLockedDoctrine
            {
                LiteralLines = activePlanningCard.LockedDoctrine.LiteralLines.Skip(1).ToArray(),
                CompareRule = activePlanningCard.LockedDoctrine.CompareRule,
                Digest = activePlanningCard.LockedDoctrine.Digest,
            },
            OperatorIntent = activePlanningCard.OperatorIntent,
            AgentProposal = activePlanningCard.AgentProposal,
            SystemDerived = activePlanningCard.SystemDerived,
            IssuedAt = activePlanningCard.IssuedAt,
            UpdatedAt = activePlanningCard.UpdatedAt,
        };
        var drifted = PlanningCardInvariantService.Evaluate(initialized.Draft, driftedCard);

        Assert.Equal(PlanningCardInvariantService.ValidState, valid.State);
        Assert.True(valid.CanExportGovernedTruth);
        Assert.Equal(5, valid.Blocks.Count);
        Assert.Empty(valid.Violations);
        Assert.Contains(valid.Blocks, block => block.BlockId == "runtime_issuer_and_edit_boundary");
        Assert.Contains(valid.Blocks, block => block.BlockId == "host_routed_truth_lineage");
        Assert.Equal(PlanningCardInvariantService.DriftedState, drifted.State);
        Assert.False(drifted.CanExportGovernedTruth);
        Assert.NotEmpty(drifted.Violations);
        Assert.Contains(drifted.Violations, violation => violation.ViolationKind == "altered_invariant_line");
        Assert.Contains("plan init candidate-first-slice", drifted.RemediationAction, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanningCardFillGuidanceService_ProjectsMissingFieldsAndBlocksOnInvariantDrift()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        var initialized = service.InitializeFormalPlanning();
        var activePlanningCard = initialized.Draft!.ActivePlanningCard!;
        var invariantReport = PlanningCardInvariantService.Evaluate(initialized.Draft, activePlanningCard);
        var missingAcceptanceCard = new ActivePlanningCard
        {
            PlanningCardId = activePlanningCard.PlanningCardId,
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
            SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
            State = activePlanningCard.State,
            LockedDoctrine = activePlanningCard.LockedDoctrine,
            OperatorIntent = new ActivePlanningCardOperatorIntent
            {
                Title = activePlanningCard.OperatorIntent.Title,
                Goal = activePlanningCard.OperatorIntent.Goal,
                ValidationArtifact = activePlanningCard.OperatorIntent.ValidationArtifact,
                AcceptanceOutline = [],
                Constraints = activePlanningCard.OperatorIntent.Constraints,
                NonGoals = activePlanningCard.OperatorIntent.NonGoals,
            },
            AgentProposal = activePlanningCard.AgentProposal,
            SystemDerived = activePlanningCard.SystemDerived,
            IssuedAt = activePlanningCard.IssuedAt,
            UpdatedAt = activePlanningCard.UpdatedAt,
        };
        var driftedCard = new ActivePlanningCard
        {
            PlanningCardId = activePlanningCard.PlanningCardId,
            PlanningSlotId = activePlanningCard.PlanningSlotId,
            SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
            SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
            State = activePlanningCard.State,
            LockedDoctrine = new ActivePlanningCardLockedDoctrine
            {
                LiteralLines = ["tampered doctrine line"],
                CompareRule = activePlanningCard.LockedDoctrine.CompareRule,
                Digest = activePlanningCard.LockedDoctrine.Digest,
            },
            OperatorIntent = activePlanningCard.OperatorIntent,
            AgentProposal = activePlanningCard.AgentProposal,
            SystemDerived = activePlanningCard.SystemDerived,
            IssuedAt = activePlanningCard.IssuedAt,
            UpdatedAt = activePlanningCard.UpdatedAt,
        };

        var ready = PlanningCardFillGuidanceService.Evaluate(activePlanningCard, invariantReport);
        var missing = PlanningCardFillGuidanceService.Evaluate(missingAcceptanceCard, invariantReport);
        var blocked = PlanningCardFillGuidanceService.Evaluate(driftedCard, PlanningCardInvariantService.Evaluate(initialized.Draft, driftedCard));

        Assert.Equal(PlanningCardFillGuidanceService.ReadyToExportState, ready.State);
        Assert.True(ready.ReadyForRecommendedExport);
        Assert.Equal(0, ready.MissingRequiredFieldCount);
        Assert.Contains("plan export-card", ready.RecommendedNextFillAction, StringComparison.Ordinal);
        Assert.Equal(PlanningCardFillGuidanceService.NeedsFillState, missing.State);
        Assert.False(missing.ReadyForRecommendedExport);
        Assert.Equal("operator_intent.acceptance_outline", missing.NextMissingFieldPath);
        Assert.Single(missing.MissingRequiredFields);
        Assert.Contains("acceptance criteria", missing.RecommendedNextFillAction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(PlanningCardFillGuidanceService.BlockedByInvariantDriftState, blocked.State);
        Assert.False(blocked.ReadyForRecommendedExport);
        Assert.Contains("plan init candidate-first-slice", blocked.RecommendedNextFillAction, StringComparison.Ordinal);
    }

    [Fact]
    public void IntentDiscovery_ExportActivePlanningCardPayload_AllowsEditableFieldChanges()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("README.md", "# Sample Repo\n\nA governed sample repo for intent discovery.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var repository = new JsonIntentDraftRepository(paths);
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var service = new IntentDiscoveryService(workspace.RootPath, paths, repository, understanding);

        service.GenerateDraft();
        service.SetFocusCard("candidate-first-slice");
        service.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        service.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        service.SetCandidateCardPosture("candidate-first-slice", GuidedPlanningPosture.ReadyToPlan);
        var initialized = service.InitializeFormalPlanning();
        var activePlanningCard = initialized.Draft!.ActivePlanningCard!;
        repository.Update(draft => new IntentDiscoveryDraft
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
            RecommendedNextAction = draft.RecommendedNextAction,
            PlanningPosture = draft.PlanningPosture,
            FormalPlanningState = draft.FormalPlanningState,
            FocusCardId = draft.FocusCardId,
            ScopeFrame = draft.ScopeFrame,
            PendingDecisions = draft.PendingDecisions,
            CandidateCards = draft.CandidateCards,
            ActivePlanningCard = new ActivePlanningCard
            {
                PlanningCardId = activePlanningCard.PlanningCardId,
                PlanningSlotId = activePlanningCard.PlanningSlotId,
                SourceIntentDraftId = activePlanningCard.SourceIntentDraftId,
                SourceCandidateCardId = activePlanningCard.SourceCandidateCardId,
                State = activePlanningCard.State,
                LockedDoctrine = activePlanningCard.LockedDoctrine,
                OperatorIntent = new ActivePlanningCardOperatorIntent
                {
                    Title = "Edited operator-owned planning title",
                    Goal = activePlanningCard.OperatorIntent.Goal,
                    ValidationArtifact = activePlanningCard.OperatorIntent.ValidationArtifact,
                    AcceptanceOutline = activePlanningCard.OperatorIntent.AcceptanceOutline,
                    Constraints = activePlanningCard.OperatorIntent.Constraints,
                    NonGoals = activePlanningCard.OperatorIntent.NonGoals,
                },
                AgentProposal = new ActivePlanningCardAgentProposal
                {
                    CandidateSummary = activePlanningCard.AgentProposal.CandidateSummary,
                    DecompositionCandidates = activePlanningCard.AgentProposal.DecompositionCandidates,
                    OpenQuestions = activePlanningCard.AgentProposal.OpenQuestions,
                    SuggestedNextAction = "Edited agent-assisted next action.",
                },
                SystemDerived = activePlanningCard.SystemDerived,
                IssuedAt = activePlanningCard.IssuedAt,
                UpdatedAt = activePlanningCard.UpdatedAt,
            },
            GeneratedAt = draft.GeneratedAt,
        });
        var exportPath = Path.Combine(workspace.RootPath, "drafts", "plan-card.json");

        var result = service.ExportActivePlanningCardPayload(exportPath);

        Assert.Equal(exportPath, result.OutputPath);
        using var exported = JsonDocument.Parse(File.ReadAllText(exportPath));
        Assert.Equal("Edited operator-owned planning title", exported.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public void ConversationProtocol_RejectsExecutionBeforeIntentOnEmptyGraph()
    {
        using var workspace = new TemporaryWorkspace();
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var understanding = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);
        var intentDiscovery = new IntentDiscoveryService(workspace.RootPath, paths, new JsonIntentDraftRepository(paths), understanding);
        var taskGraph = new TaskGraphService(new InMemoryTaskGraphRepository(new Carves.Runtime.Domain.Tasks.TaskGraph()), new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var protocol = new ConversationProtocolService(taskGraph, intentDiscovery);

        var validation = protocol.ValidateRequestedPhase(ConversationPhase.Execution, session: null);

        Assert.False(validation.Allowed);
        Assert.Equal(ConversationPhase.Intent, validation.CurrentPhase);
    }

    [Fact]
    public void PromptServices_ReturnCanonicalTemplatesAndKernel()
    {
        using var workspace = new TemporaryWorkspace();
        var templates = new PromptProtocolService(workspace.RootPath).GetTemplates();
        var kernel = new PromptKernelService(workspace.RootPath).GetKernel();

        Assert.Contains(templates, template => string.Equals(template.TemplateId, "intent-summary", StringComparison.Ordinal));
        Assert.Contains(templates, template => string.Equals(template.TemplateId, "review-explanation", StringComparison.Ordinal));
        Assert.Equal("carves-prompt-kernel", kernel.KernelId);
        Assert.Contains("Intent", kernel.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectUnderstandingProjection_ClassifiesMissingFreshAndStale()
    {
        using var workspace = new TemporaryWorkspace();
        var sourceFile = workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var service = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);

        var missing = service.Evaluate(hydrateIfNeeded: false);
        var refreshed = service.Evaluate(hydrateIfNeeded: true);
        File.WriteAllText(sourceFile, "namespace Sample; public sealed class SampleRegistry { }");
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow.AddSeconds(1));
        var stale = service.Evaluate(hydrateIfNeeded: false);

        Assert.Equal(ProjectUnderstandingState.Missing, missing.State);
        Assert.Equal(ProjectUnderstandingState.Fresh, refreshed.State);
        Assert.Equal("refreshed", refreshed.Action);
        Assert.Equal(ProjectUnderstandingState.Stale, stale.State);
    }

    [Fact]
    public void ProjectUnderstandingProjection_IgnoresGeneratedArtifactsWhenCheckingFreshness()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        var generatedArtifact = workspace.WriteFile("src/obj/project.assets.json", """{ "version": 3 }""");
        var paths = workspace.Paths;
        var systemConfig = TestSystemConfigFactory.Create();
        var builder = new FileCodeGraphBuilder(workspace.RootPath, paths, systemConfig);
        var query = new FileCodeGraphQueryService(paths, builder);
        var service = new ProjectUnderstandingProjectionService(workspace.RootPath, paths, systemConfig, builder, query);

        var refreshed = service.Evaluate(hydrateIfNeeded: true);
        File.WriteAllText(generatedArtifact, """{ "version": 4 }""");
        File.SetLastWriteTimeUtc(generatedArtifact, DateTime.UtcNow.AddSeconds(1));
        var afterGeneratedUpdate = service.Evaluate(hydrateIfNeeded: false);

        Assert.Equal(ProjectUnderstandingState.Fresh, refreshed.State);
        Assert.Equal(ProjectUnderstandingState.Fresh, afterGeneratedUpdate.State);
    }
}
