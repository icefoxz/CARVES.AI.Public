using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Carves.Runtime.Application.Artifacts;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Application.TaskGraph;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Tasks;
using Carves.Runtime.Infrastructure.CodeGraph;
using Carves.Runtime.Infrastructure.Persistence;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;
using DomainTaskStatus = Carves.Runtime.Domain.Tasks.TaskStatus;

namespace Carves.Runtime.Application.Tests;

public sealed partial class RuntimeGovernedAgentHandoffServicesTests
{
    [Fact]
    public void AgentProblemFollowUpPlanningIntake_IsReadyAndEmptyWhenNoAcceptedRecordsExist()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var surface = new RuntimeAgentProblemFollowUpPlanningIntakeService(
            workspace.RootPath,
            () => []).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-follow-up-planning-intake", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("agent_problem_follow_up_planning_intake_no_accepted_records", surface.OverallPosture);
        Assert.True(surface.DecisionRecordReady);
        Assert.True(surface.PlanningIntakeReady);
        Assert.Equal(0, surface.AcceptedDecisionRecordCount);
        Assert.Equal(0, surface.AcceptedPlanningItemCount);
        Assert.Equal("carves pilot follow-up-intake --json", surface.JsonCommandEntry);
        Assert.Equal("carves inspect runtime-agent-problem-follow-up-planning-intake", surface.InspectCommandEntry);
        Assert.Contains(surface.PlanningLaneCommands, command => command == "carves intent draft --persist");
        Assert.Contains(surface.PlanningLaneCommands, command => command == "carves plan init [candidate-card-id]");
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not create", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void AgentProblemFollowUpPlanningIntake_ProjectsAcceptedCommittedRecordsAsFormalPlanningInputs()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up planning intake truth");
        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);

        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-intake --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted follow-up planning input");

        var surface = new RuntimeAgentProblemFollowUpPlanningIntakeService(
            workspace.RootPath,
            () => [problem]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_planning_intake_ready", surface.OverallPosture);
        Assert.True(surface.DecisionRecordReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.True(surface.PlanningIntakeReady);
        Assert.Equal(1, surface.AcceptedDecisionRecordCount);
        Assert.Equal(1, surface.AcceptedPlanningItemCount);
        Assert.Equal(1, surface.ActionablePlanningItemCount);
        var item = Assert.Single(surface.PlanningItems);
        Assert.True(item.Actionable);
        Assert.Equal("ready_for_formal_planning", item.IntakeStatus);
        Assert.Contains(record.DecisionRecordId, item.DecisionRecordIds);
        Assert.Contains(record.RecordPath, item.DecisionRecordPaths);
        Assert.Contains(problem.ProblemId, item.RelatedProblemIds);
        Assert.Contains(problem.EvidenceId, item.RelatedEvidenceIds);
        Assert.Contains("carves intent draft --persist", item.SuggestedIntentDraftCommand, StringComparison.Ordinal);
        Assert.Contains("carves plan init", item.SuggestedPlanInitCommand, StringComparison.Ordinal);
        Assert.Contains("protected_truth_root", item.SuggestedTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("follow-up-intake", item.SuggestedReadbackCommand, StringComparison.Ordinal);
    }


    [Fact]
    public void AgentProblemFollowUpPlanningIntake_BlocksAcceptedRecordsUntilDecisionRecordCommitReadbackIsClean()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up planning intake truth");
        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);

        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-intake --json",
        });

        var surface = new RuntimeAgentProblemFollowUpPlanningIntakeService(
            workspace.RootPath,
            () => [problem]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_planning_intake_waiting_for_decision_record", surface.OverallPosture);
        Assert.False(surface.DecisionRecordReady);
        Assert.False(surface.DecisionRecordCommitReady);
        Assert.False(surface.PlanningIntakeReady);
        Assert.Equal(1, surface.AcceptedDecisionRecordCount);
        var item = Assert.Single(surface.PlanningItems);
        Assert.False(item.Actionable);
        Assert.Equal("blocked_by_decision_record_readback", item.IntakeStatus);
        Assert.Contains(record.DecisionRecordId, item.DecisionRecordIds);
        Assert.Contains(surface.Gaps, gap => gap == "agent_problem_follow_up_decision_record_not_ready");
        Assert.Contains(surface.Gaps, gap => gap.StartsWith("agent_problem_follow_up_decision_record:agent_problem_follow_up_decision_record_uncommitted:", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentProblemFollowUpPlanningIntake_TreatsCompletedCandidateTaskTruthAsConsumed()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up planning intake truth");
        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);

        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-intake --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted follow-up planning input");

        var candidateId = Assert.Single(record.CandidateIds);
        workspace.WriteFile(
            ".ai/tasks/nodes/T-FOLLOW-UP-CONSUMED-001.json",
            $$"""
            {
              "schema_version": "task_node.v1",
              "task_id": "T-FOLLOW-UP-CONSUMED-001",
              "title": "Consumed follow-up",
              "status": "completed",
              "metadata": {
                "source_candidate_card_id": "{{candidateId}}"
              }
            }
            """);
        RunGit(workspace.RootPath, "add", ".ai/tasks/nodes");
        RunGit(workspace.RootPath, "commit", "-m", "Complete follow-up planning input task");

        var surface = new RuntimeAgentProblemFollowUpPlanningIntakeService(
            workspace.RootPath,
            () => [problem]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_planning_intake_no_open_accepted_records", surface.OverallPosture);
        Assert.True(surface.DecisionRecordReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.True(surface.PlanningIntakeReady);
        Assert.Equal(1, surface.AcceptedDecisionRecordCount);
        Assert.Equal(0, surface.AcceptedPlanningItemCount);
        Assert.Equal(0, surface.ActionablePlanningItemCount);
        Assert.Equal(1, surface.ConsumedPlanningItemCount);
        Assert.Contains(candidateId, surface.ConsumedPlanningCandidateIds);
        Assert.Empty(surface.PlanningItems);
        Assert.Equal("agent_problem_follow_up_planning_intake_readback_clean", surface.RecommendedNextAction);
    }


    [Fact]
    public void AgentProblemFollowUpPlanningGate_IsReadyAndEmptyWhenNoAcceptedRecordsExist()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var surface = new RuntimeAgentProblemFollowUpPlanningGateService(
            workspace.RootPath,
            () => new RuntimeAgentProblemFollowUpPlanningIntakeService(workspace.RootPath, () => []).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "discussion_only",
                FormalPlanningState = "discuss",
                ActivePlanningSlotState = "no_intent_draft",
                ActivePlanningSlotCanInitialize = false,
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-follow-up-planning-gate", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", surface.PlanningIntakeDocumentPath);
        Assert.Equal("agent_problem_follow_up_planning_gate_no_accepted_records", surface.OverallPosture);
        Assert.True(surface.PlanningIntakeReady);
        Assert.True(surface.PlanningGateReady);
        Assert.Equal(0, surface.AcceptedPlanningItemCount);
        Assert.Equal(0, surface.ReadyForPlanInitCount);
        Assert.Equal("carves pilot follow-up-gate --json", surface.JsonCommandEntry);
        Assert.Equal("carves inspect runtime-agent-problem-follow-up-planning-gate", surface.InspectCommandEntry);
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not create", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void AgentProblemFollowUpPlanningGate_WaitsForIntentDraftBeforePlanInit()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up planning gate truth");
        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);

        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-gate --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted follow-up planning gate input");

        var surface = new RuntimeAgentProblemFollowUpPlanningGateService(
            workspace.RootPath,
            () => new RuntimeAgentProblemFollowUpPlanningIntakeService(workspace.RootPath, () => [problem]).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "discussion_only",
                FormalPlanningState = "discuss",
                FormalPlanningEntryCommand = "plan init [candidate-card-id]",
                ActivePlanningSlotState = "no_intent_draft",
                ActivePlanningSlotCanInitialize = false,
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_planning_gate_waiting_for_intent_draft", surface.OverallPosture);
        Assert.True(surface.PlanningGateReady);
        Assert.Equal("carves intent draft --persist", surface.NextGovernedCommand);
        Assert.Equal(1, surface.AcceptedPlanningItemCount);
        Assert.Equal(0, surface.ReadyForPlanInitCount);
        var item = Assert.Single(surface.PlanningGateItems);
        Assert.False(item.Actionable);
        Assert.Equal("waiting_for_intent_draft", item.PlanningGateStatus);
        Assert.Equal("carves intent draft --persist", item.NextGovernedCommand);
        Assert.Contains(record.DecisionRecordId, item.DecisionRecordIds);
    }


    [Fact]
    public void AgentProblemFollowUpPlanningGate_AllowsPlanInitWhenSlotCanInitialize()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up planning gate truth");
        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);

        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-gate --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted follow-up planning gate input");

        var planningIntake = new RuntimeAgentProblemFollowUpPlanningIntakeService(workspace.RootPath, () => [problem]).Build();
        var intakeItem = Assert.Single(planningIntake.PlanningItems);
        workspace.WriteFile(
            ".ai/runtime/intent_draft.json",
            $$"""
            {
              "candidate_cards": [
                {
                  "candidate_card_id": "{{intakeItem.CandidateId}}",
                  "planning_posture": "ready_to_plan"
                }
              ]
            }
            """);

        var surface = new RuntimeAgentProblemFollowUpPlanningGateService(
            workspace.RootPath,
            () => planningIntake,
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "plan_init_required",
                FormalPlanningState = "plan_init_required",
                FormalPlanningEntryCommand = "plan init [candidate-card-id]",
                ActivePlanningSlotState = "empty_ready_to_initialize",
                ActivePlanningSlotCanInitialize = true,
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_planning_gate_ready_to_plan_init", surface.OverallPosture);
        Assert.True(surface.PlanningGateReady);
        Assert.Equal(1, surface.ReadyForPlanInitCount);
        Assert.Contains("carves plan init", surface.NextGovernedCommand, StringComparison.Ordinal);
        var item = Assert.Single(surface.PlanningGateItems);
        Assert.True(item.Actionable);
        Assert.Equal("ready_for_plan_init", item.PlanningGateStatus);
        Assert.Contains("carves plan init", item.NextGovernedCommand, StringComparison.Ordinal);
        Assert.Contains(record.DecisionRecordId, item.DecisionRecordIds);
    }


    [Fact]
    public void AgentProblemFollowUpPlanningGate_WaitsForIntentDraftProjectionBeforePlanInit()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up planning gate truth");
        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);

        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-gate --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted follow-up planning gate input");

        var planningIntake = new RuntimeAgentProblemFollowUpPlanningIntakeService(workspace.RootPath, () => [problem]).Build();
        var surface = new RuntimeAgentProblemFollowUpPlanningGateService(
            workspace.RootPath,
            () => planningIntake,
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "plan_init_required",
                FormalPlanningState = "plan_init_required",
                FormalPlanningEntryCommand = "plan init [candidate-card-id]",
                ActivePlanningSlotState = "empty_ready_to_initialize",
                ActivePlanningSlotCanInitialize = true,
                IsValid = true,
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_planning_gate_waiting_for_intent_draft_candidate_projection", surface.OverallPosture);
        Assert.True(surface.PlanningGateReady);
        Assert.Equal("carves intent draft --persist", surface.NextGovernedCommand);
        Assert.Equal(0, surface.ReadyForPlanInitCount);
        var item = Assert.Single(surface.PlanningGateItems);
        Assert.False(item.Actionable);
        Assert.Equal("waiting_for_intent_draft_candidate_projection", item.PlanningGateStatus);
        Assert.Equal("carves intent draft --persist", item.NextGovernedCommand);
        Assert.Contains(record.DecisionRecordId, item.DecisionRecordIds);
        Assert.Contains(surface.Gaps, gap => gap.StartsWith("follow_up_candidate_not_in_intent_draft:", StringComparison.Ordinal));
    }


    [Fact]
    public void IntentDraft_ProjectsAcceptedFollowUpPlanningInputsAsPlanInitCandidates()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("README.md", "# Follow-up Target\n\nA target repo for follow-up planning.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up intent truth");

        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        workspace.WriteFile(
            $".ai/runtime/pilot-problems/{problem.ProblemId}.json",
            JsonSerializer.Serialize(problem, SnakeCaseJsonOptions));
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);
        recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-gate --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/pilot-problems", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted follow-up planning input");

        var planningIntake = new RuntimeAgentProblemFollowUpPlanningIntakeService(
            workspace.RootPath,
            () => [problem]).Build();
        var followUpItem = Assert.Single(planningIntake.PlanningItems);
        var intentDiscovery = CreateIntentDiscoveryService(workspace);
        var draftStatus = intentDiscovery.GenerateDraft();

        Assert.NotNull(draftStatus.Draft);
        Assert.Contains(draftStatus.Draft!.CandidateCards, candidate =>
            candidate.CandidateCardId == followUpItem.CandidateId
            && candidate.PlanningPosture == GuidedPlanningPosture.ReadyToPlan
            && candidate.Summary.Contains("Accepted follow-up evidence", StringComparison.Ordinal));

        intentDiscovery.SetFocusCard(followUpItem.CandidateId);
        intentDiscovery.SetPendingDecisionStatus("first_validation_artifact", GuidedPlanningDecisionStatus.Resolved);
        intentDiscovery.SetPendingDecisionStatus("first_slice_boundary", GuidedPlanningDecisionStatus.Resolved);
        var initialized = intentDiscovery.InitializeFormalPlanning(followUpItem.CandidateId);

        Assert.NotNull(initialized.Draft?.ActivePlanningCard);
        Assert.Equal(followUpItem.CandidateId, initialized.Draft!.ActivePlanningCard!.SourceCandidateCardId);
        Assert.Contains("plan export-card", initialized.Draft.RecommendedNextAction, StringComparison.Ordinal);
    }


    [Fact]
    public void IntentDraft_DoesNotProjectConsumedFollowUpPlanningInput()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("README.md", "# Follow-up Target\n\nA target repo for follow-up planning.");
        workspace.WriteFile("src/Sample.cs", "namespace Sample; public sealed class SampleService { }");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline consumed follow-up intent truth");

        var problem = CreateBlockingPilotProblem(workspace.RootPath);
        workspace.WriteFile(
            $".ai/runtime/pilot-problems/{problem.ProblemId}.json",
            JsonSerializer.Serialize(problem, SnakeCaseJsonOptions));
        var recordService = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [problem]);
        var record = recordService.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-gate --json",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/pilot-problems", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record accepted consumed follow-up planning input");

        var candidateId = Assert.Single(record.CandidateIds);
        workspace.WriteFile(
            ".ai/tasks/nodes/T-FOLLOW-UP-CONSUMED-001.json",
            $$"""
            {
              "schema_version": "task_node.v1",
              "task_id": "T-FOLLOW-UP-CONSUMED-001",
              "title": "Consumed follow-up",
              "status": "completed",
              "metadata": {
                "source_candidate_card_id": "{{candidateId}}"
              }
            }
            """);
        RunGit(workspace.RootPath, "add", ".ai/tasks/nodes");
        RunGit(workspace.RootPath, "commit", "-m", "Complete consumed follow-up task");

        var planningIntake = new RuntimeAgentProblemFollowUpPlanningIntakeService(
            workspace.RootPath,
            () => [problem]).Build();
        Assert.Empty(planningIntake.PlanningItems);

        var intentDiscovery = CreateIntentDiscoveryService(workspace);
        var draftStatus = intentDiscovery.GenerateDraft();

        Assert.NotNull(draftStatus.Draft);
        Assert.DoesNotContain(draftStatus.Draft!.CandidateCards, candidate => candidate.CandidateCardId == candidateId);
    }


    [Fact]
    public void CliActivationPlan_ProjectsOperatorOwnedActivationLanes()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("CARVES.Runtime.sln", string.Empty);

        var surface = new RuntimeCliActivationPlanService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-cli-activation-plan", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("cli_activation_plan_ready", surface.OverallPosture);
        Assert.True(surface.ActivationPlanComplete);
        Assert.Equal("absolute_wrapper", surface.RecommendedActivationLane);
        Assert.Equal("source_tree", surface.RuntimeRootKind);
        Assert.True(surface.RuntimeRootHasPowerShellWrapper);
        Assert.True(surface.RuntimeRootHasCmdWrapper);
        Assert.Contains(surface.ActivationLanes, lane => lane.LaneId == "absolute_wrapper");
        Assert.Contains(surface.ActivationLanes, lane => lane.LaneId == "session_alias");
        Assert.Contains(surface.ActivationLanes, lane => lane.LaneId == "path_entry");
        Assert.Contains(surface.ActivationLanes, lane => lane.LaneId == "cmd_shim");
        Assert.Contains(surface.ActivationLanes, lane => lane.LaneId == "dotnet_tool");
        Assert.Contains(surface.RequiredSmokeCommands, command => command == "carves pilot activation --json");
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("shell profile", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not install", StringComparison.Ordinal));
    }


    [Fact]
    public void CliInvocationContract_ProjectsInvocationLanes()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("docs/runtime/runtime-governed-agent-handoff-proof.md", "# proof");
        workspace.WriteFile("carves.ps1", "# wrapper");
        workspace.WriteFile("carves.cmd", "@echo off");
        workspace.WriteFile("CARVES.Runtime.sln", string.Empty);

        var surface = new RuntimeCliInvocationContractService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-cli-invocation-contract", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("cli_invocation_contract_ready", surface.OverallPosture);
        Assert.True(surface.InvocationContractComplete);
        Assert.Equal("source_tree_wrapper", surface.RecommendedInvocationMode);
        Assert.Equal("source_tree", surface.RuntimeRootKind);
        Assert.Contains(surface.InvocationLanes, lane => lane.LaneId == "source_tree_wrapper");
        Assert.Contains(surface.InvocationLanes, lane => lane.LaneId == "local_dist_wrapper");
        Assert.Contains(surface.InvocationLanes, lane => lane.LaneId == "global_alias");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot invocation --json");
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("global", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void ProductClosurePilotStatus_ProjectsInitGapAndWorkspaceSubmitNextCommand()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var missingRuntimeSurface = new RuntimeProductClosurePilotStatusService(
            workspace.RootPath,
            CreateTaskGraphService(),
            () => new RuntimeProductClosurePilotGuideService(workspace.RootPath).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "formal_planning_ready",
                FormalPlanningState = "discuss",
            },
            () => new RuntimeManagedWorkspaceSurface
            {
                OverallPosture = "no_managed_workspace",
            }).Build();

        Assert.True(missingRuntimeSurface.IsValid);
        Assert.Equal("runtime-product-closure-pilot-status", missingRuntimeSurface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", missingRuntimeSurface.ProductClosurePhase);
        Assert.Equal("pilot_status_blocked_by_runtime_init", missingRuntimeSurface.OverallPosture);
        Assert.Equal("attach_target", missingRuntimeSurface.CurrentStageId);
        Assert.Equal("blocked", missingRuntimeSurface.CurrentStageStatus);
        Assert.Equal("carves init [target-path] --json", missingRuntimeSurface.NextCommand);
        Assert.Contains(missingRuntimeSurface.Gaps, gap => gap == "runtime_not_initialized");

        workspace.WriteFile(".ai/runtime.json", "{}");
        var missingBootstrapSurface = new RuntimeProductClosurePilotStatusService(
            workspace.RootPath,
            CreateTaskGraphService(),
            () => new RuntimeProductClosurePilotGuideService(workspace.RootPath).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "formal_planning_ready",
                FormalPlanningState = "discuss",
            },
            () => new RuntimeManagedWorkspaceSurface
            {
                OverallPosture = "no_managed_workspace",
            }).Build();

        Assert.True(missingBootstrapSurface.IsValid);
        Assert.Equal("pilot_status_target_agent_bootstrap_required", missingBootstrapSurface.OverallPosture);
        Assert.Equal("target_agent_bootstrap", missingBootstrapSurface.CurrentStageId);
        Assert.Equal("carves agent bootstrap --write", missingBootstrapSurface.NextCommand);
        Assert.Contains(missingBootstrapSurface.Gaps, gap => gap == "target_agent_bootstrap_missing");

        workspace.WriteFile(".ai/AGENT_BOOTSTRAP.md", "# bootstrap");
        workspace.WriteFile("AGENTS.md", "# agents");
        WriteProjectLocalAgentEntry(workspace.RootPath);
        const string taskId = "T-PILOT-STATUS-001";
        var activeTask = new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-PILOT-STATUS",
            Title = "Pilot status active task",
            Description = "Exercise active workspace stage projection.",
            Status = DomainTaskStatus.Running,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["docs/runtime/"],
            Acceptance = ["pilot status selects workspace submit"],
        };

        var activeWorkspaceSurface = new RuntimeProductClosurePilotStatusService(
            workspace.RootPath,
            CreateTaskGraphService(activeTask),
            () => new RuntimeProductClosurePilotGuideService(workspace.RootPath).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "formal_planning_ready",
                FormalPlanningState = "plan_bound",
            },
            () => new RuntimeManagedWorkspaceSurface
            {
                OverallPosture = "task_bound_workspace_active",
                BoundTaskIds = [taskId],
                ActiveLeases =
                [
                    new RuntimeManagedWorkspaceLeaseSurface
                    {
                        LeaseId = "lease-pilot-status-001",
                        TaskId = taskId,
                        Status = "active",
                        WorkspacePath = Path.Combine(workspace.RootPath, "..", "pilot-status-workspace"),
                    },
                ],
            }).Build();

        Assert.True(activeWorkspaceSurface.IsValid);
        Assert.Equal("pilot_status_workspace_output_required", activeWorkspaceSurface.OverallPosture);
        Assert.Equal("workspace_submit", activeWorkspaceSurface.CurrentStageId);
        Assert.Equal(1, activeWorkspaceSurface.ActiveLeaseCount);
        Assert.Equal($"carves plan submit-workspace {taskId} \"submitted managed workspace result\"", activeWorkspaceSurface.NextCommand);
        Assert.Contains(activeWorkspaceSurface.StageStatuses, stage =>
            stage.StageId == "workspace_submit"
            && stage.State == "ready");
    }


    [Fact]
    public void ProductClosurePilotStatus_ProjectsRecoverableCleanupWhenManagedWorkspaceResidueRemains()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile(".ai/AGENT_BOOTSTRAP.md", "# bootstrap");
        workspace.WriteFile("AGENTS.md", "# agents");
        WriteProjectLocalAgentEntry(workspace.RootPath);

        var surface = new RuntimeProductClosurePilotStatusService(
            workspace.RootPath,
            CreateTaskGraphService(),
            () => new RuntimeProductClosurePilotGuideService(workspace.RootPath).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "formal_planning_ready",
                FormalPlanningState = "discuss",
            },
            () => new RuntimeManagedWorkspaceSurface
            {
                OverallPosture = "planning_lineage_closed_no_active_workspace",
                RecoverableResiduePosture = "recoverable_runtime_residue_present",
                RecoverableResidueCount = 1,
                RecoverableResidueSummary = "Active managed workspace lease is still recorded for a completed task.",
                RecoverableResidueRecommendedNextAction = "Run `carves cleanup` and re-read `inspect runtime-managed-workspace`.",
                RecoverableResidueBlocksAutoRun = true,
                HighestRecoverableResidueSeverity = "warning",
                RecoverableCleanupActionId = "cleanup_runtime_residue",
                RecoverableCleanupActionMode = "dry_run_first",
                AvailableActions =
                [
                    new RuntimeInteractionActionSurface
                    {
                        ActionId = "cleanup_runtime_residue",
                        Kind = "cleanup",
                        ActionMode = "dry_run_first",
                        Command = "carves cleanup --dry-run",
                    },
                ],
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("pilot_status_intent_capture_required", surface.OverallPosture);
        Assert.True(surface.RecoverableCleanupRequired);
        Assert.Equal(1, surface.RecoverableResidueCount);
        Assert.Equal("healthy_with_recoverable_residue", surface.OperationalState);
        Assert.False(surface.SafeToStartNewExecution);
        Assert.True(surface.SafeToDiscuss);
        Assert.True(surface.SafeToCleanup);
        Assert.Equal("warning", surface.HighestRecoverableResidueSeverity);
        Assert.True(surface.RecoverableResidueBlocksAutoRun);
        Assert.Equal("cleanup_runtime_residue", surface.RecoverableCleanupActionId);
        Assert.Equal("dry_run_first", surface.RecoverableCleanupActionMode);
        Assert.Contains("Active managed workspace lease", surface.RecoverableCleanupSummary, StringComparison.Ordinal);
        Assert.Contains("carves cleanup", surface.RecoverableCleanupRecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("Recoverable runtime residue remains", surface.Summary, StringComparison.Ordinal);
        Assert.Contains(surface.Gaps, gap => gap == "recoverable_runtime_residue_present");
        Assert.Contains(surface.AvailableActions, action => action.ActionId == "cleanup_runtime_residue" && action.Kind == "cleanup" && action.ActionMode == "dry_run_first");
    }


    [Fact]
    public void ProductClosurePilotStatus_ProjectsNewIntentAfterProductPilotProofIsComplete()
    {
        using var workspace = new TemporaryWorkspace();

        var sourceRoot = Path.Combine(workspace.RootPath, "source");
        Directory.CreateDirectory(sourceRoot);
        WriteDistFile(sourceRoot, "README.md", "# Runtime source\n");
        RunGit(sourceRoot, "init");
        RunGit(sourceRoot, "config", "user.email", "carves-tests@example.invalid");
        RunGit(sourceRoot, "config", "user.name", "CARVES Tests");
        RunGit(sourceRoot, "add", ".");
        RunGit(sourceRoot, "commit", "-m", "source baseline");
        var sourceHead = RunGitCapture(sourceRoot, "rev-parse", "HEAD");

        var distRoot = Path.Combine(workspace.RootPath, ".dist", "CARVES.Runtime-0.2.0-beta.1");
        WriteFrozenDistProofRuntimeResources(distRoot);
        WriteDistFile(
            distRoot,
            "MANIFEST.json",
            JsonSerializer.Serialize(
                new
                {
                    schema_version = "carves-runtime-dist.v1",
                    version = "0.2.0-beta.1",
                    source_commit = sourceHead,
                    source_repo_root = sourceRoot,
                    output_path = distRoot,
                    published_cli_entry = RuntimeCliWrapperPaths.PublishedCliManifestEntry,
                },
                JsonOptions));

        var targetRoot = Path.Combine(workspace.RootPath, "target");
        Directory.CreateDirectory(targetRoot);
        WriteDistFile(targetRoot, "PROJECT.md", "# target\n");
        WriteDistFile(
            targetRoot,
            ".ai/runtime.json",
            JsonSerializer.Serialize(new { runtime_root = distRoot }, JsonOptions));
        WriteDistFile(
            targetRoot,
            ".ai/runtime/attach-handshake.json",
            JsonSerializer.Serialize(
                new
                {
                    request = new
                    {
                        runtime_root = distRoot,
                    },
                    acknowledgement = new
                    {
                        status = "attached",
                    },
                },
                JsonOptions));
        WriteDistFile(targetRoot, ".ai/AGENT_BOOTSTRAP.md", "# bootstrap\n");
        WriteDistFile(targetRoot, "AGENTS.md", "# agents\n");
        WriteProjectLocalAgentEntry(targetRoot, distRoot);
        RunGit(targetRoot, "init");
        RunGit(targetRoot, "config", "user.email", "carves-tests@example.invalid");
        RunGit(targetRoot, "config", "user.name", "CARVES Tests");
        RunGit(targetRoot, "add", ".");
        RunGit(targetRoot, "commit", "-m", "target baseline");

        var completedTask = new TaskNode
        {
            TaskId = "T-PILOT-STATUS-PROOF-001",
            CardId = "CARD-PILOT-STATUS-PROOF",
            Title = "Completed pilot proof task",
            Description = "Exercise post-proof pilot status projection.",
            Status = DomainTaskStatus.Completed,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["PROJECT.md"],
            Acceptance = ["pilot proof is complete"],
        };

        var surface = new RuntimeProductClosurePilotStatusService(
            targetRoot,
            CreateTaskGraphService(completedTask),
            () => new RuntimeProductClosurePilotGuideService(targetRoot).Build(),
            () => new RuntimeFormalPlanningPostureSurface
            {
                OverallPosture = "formal_planning_ready",
                FormalPlanningState = "closed",
            },
            () => new RuntimeManagedWorkspaceSurface
            {
                OverallPosture = "planning_lineage_closed_no_active_workspace",
            }).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("pilot_status_product_pilot_proof_complete", surface.OverallPosture);
        Assert.Equal("ready_for_new_intent", surface.CurrentStageId);
        Assert.Equal(26, surface.CurrentStageOrder);
        Assert.Equal("ready", surface.CurrentStageStatus);
        Assert.Equal("carves discuss context", surface.NextCommand);
        Assert.Empty(surface.Gaps);
        Assert.True(surface.TargetCommitClosureComplete);
        Assert.True(surface.TargetResiduePolicyReady);
        Assert.True(surface.TargetIgnoreDecisionPlanReady);
        Assert.True(surface.TargetIgnoreDecisionRecordReady);
        Assert.True(surface.LocalDistFreshnessSmokeReady);
        Assert.True(surface.StableExternalConsumptionReady);
        Assert.True(surface.FrozenDistTargetReadbackProofComplete);
        Assert.Contains(surface.StageStatuses, stage =>
            stage.StageId == "product_pilot_proof"
            && stage.State == "satisfied_or_not_required");
        Assert.Contains(surface.StageStatuses, stage =>
            stage.StageId == "ready_for_new_intent"
            && stage.State == "ready");
    }

}
