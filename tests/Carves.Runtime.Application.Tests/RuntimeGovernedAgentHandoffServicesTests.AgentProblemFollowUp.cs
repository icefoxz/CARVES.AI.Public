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
    public void AgentProblemIntake_ProjectsSchemaCommandsAndRecentProblems()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        var recordedAt = DateTimeOffset.Parse("2026-04-12T01:02:03Z");

        var surface = new RuntimeAgentProblemIntakeService(
            workspace.RootPath,
            () => new RuntimeExternalTargetPilotStartSurface
            {
                OverallPosture = "external_target_pilot_start_bundle_ready",
                PilotStartBundleReady = true,
                IsValid = true,
            },
            () => new RuntimeExternalTargetPilotNextSurface
            {
                OverallPosture = "external_target_pilot_next_ready",
                ReadyToRunNextCommand = true,
                CurrentStageId = "intent_capture",
                CurrentStageOrder = 8,
                CurrentStageStatus = "ready",
                NextGovernedCommand = "carves discuss context",
                IsValid = true,
            },
            () =>
            [
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-010203-abc",
                    EvidenceId = "EVIDENCE-20260412-010203-def",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "intent_capture",
                    ProblemKind = "blocked_posture",
                    Severity = "blocking",
                    Summary = "Agent reached a CARVES blocked posture.",
                    BlockedCommand = "carves pilot next --json",
                    Status = "recorded",
                    RecordedAtUtc = recordedAt,
                },
            ]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-intake", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("agent_problem_intake_ready", surface.OverallPosture);
        Assert.True(surface.ProblemIntakeReady);
        Assert.True(surface.PilotStartBundleReady);
        Assert.True(surface.ReadyToRunNextCommand);
        Assert.Equal("intent_capture", surface.CurrentStageId);
        Assert.Equal("carves discuss context", surface.NextGovernedCommand);
        Assert.Equal("docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md", surface.ProblemIntakeGuideDocumentPath);
        Assert.Contains(surface.AcceptedProblemKinds, kind => kind == "blocked_posture");
        Assert.Contains(surface.AcceptedProblemKinds, kind => kind == "protected_truth_root_requested");
        Assert.Contains(surface.RequiredPayloadFields, field => field == "summary");
        Assert.Contains(surface.RequiredPayloadFields, field => field == "problem_kind");
        Assert.Contains(surface.CommandExamples, command => command == "carves pilot problem-intake --json");
        Assert.Contains(surface.CommandExamples, command => command == "carves pilot report-problem .carves-agent/problem-intake.json --json");
        Assert.Contains(surface.CommandExamples, command => command == "carves pilot follow-up --json");
        Assert.Contains(surface.StopAndReportTriggers, trigger => trigger.Contains("protected truth root", StringComparison.Ordinal));
        Assert.Equal(1, surface.RecentProblemCount);
        Assert.Contains(surface.RecentProblems, problem =>
            problem.ProblemId == "PROBLEM-20260412-010203-abc"
            && problem.EvidenceId == "EVIDENCE-20260412-010203-def"
            && problem.ProblemKind == "blocked_posture");
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not create cards", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentProblemTriageLedger_ProjectsGroupedProblemQueue()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        var older = DateTimeOffset.Parse("2026-04-12T01:02:03Z");
        var newer = DateTimeOffset.Parse("2026-04-12T02:03:04Z");

        var surface = new RuntimeAgentProblemTriageLedgerService(
            workspace.RootPath,
            () =>
            [
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-010203-abc",
                    EvidenceId = "EVIDENCE-20260412-010203-def",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "intent_capture",
                    ProblemKind = "blocked_posture",
                    Severity = "blocking",
                    Summary = "Agent reached a CARVES blocked posture.",
                    BlockedCommand = "carves pilot next --json",
                    Status = "recorded",
                    RecordedAtUtc = older,
                },
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-020304-ghi",
                    EvidenceId = "EVIDENCE-20260412-020304-jkl",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "workspace_submit",
                    ProblemKind = "protected_truth_root_requested",
                    Severity = "high",
                    Summary = "Agent was asked to edit protected truth directly.",
                    BlockedCommand = "manual edit .ai/tasks/graph.json",
                    RecommendedFollowUp = "Open governed Runtime follow-up work.",
                    Status = "recorded",
                    RecordedAtUtc = newer,
                },
            ]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-triage-ledger", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("agent_problem_triage_review_required", surface.OverallPosture);
        Assert.True(surface.TriageLedgerReady);
        Assert.Equal(2, surface.RecordedProblemCount);
        Assert.Equal(2, surface.BlockingProblemCount);
        Assert.Equal(1, surface.RepoCount);
        Assert.Equal(2, surface.DistinctProblemKindCount);
        Assert.Equal(2, surface.ReviewQueueCount);
        Assert.Equal("carves pilot triage --json", surface.JsonCommandEntry);
        Assert.Equal("carves inspect runtime-agent-problem-triage-ledger", surface.InspectCommandEntry);
        Assert.Contains(surface.ProblemKindLedger, item =>
            item.ProblemKind == "protected_truth_root_requested"
            && item.RecommendedTriageLane == "protected_truth_root_policy_review");
        Assert.Contains(surface.ProblemKindLedger, item =>
            item.ProblemKind == "blocked_posture"
            && item.RecommendedTriageLane == "command_contract_or_runtime_surface_review");
        Assert.Contains(surface.StageLedger, item => item.CurrentStageId == "workspace_submit" && item.Count == 1);
        Assert.Contains(surface.SeverityLedger, item => item.Severity == "blocking" && item.Count == 1);
        Assert.Equal("PROBLEM-20260412-020304-ghi", surface.ReviewQueue[0].ProblemId);
        Assert.Equal("protected_truth_root_policy_review", surface.ReviewQueue[0].RecommendedTriageLane);
        Assert.Contains(surface.TriageRules, rule => rule.Contains("read-only ledger", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not automatically close", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentProblemFollowUpCandidates_ProjectsOperatorReviewCandidates()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        var older = DateTimeOffset.Parse("2026-04-12T01:02:03Z");
        var newer = DateTimeOffset.Parse("2026-04-12T02:03:04Z");

        var surface = new RuntimeAgentProblemFollowUpCandidatesService(
            workspace.RootPath,
            () =>
            [
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-010203-abc",
                    EvidenceId = "EVIDENCE-20260412-010203-def",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "intent_capture",
                    ProblemKind = "next_command_ambiguous",
                    Severity = "low",
                    Summary = "Agent could not classify the next command.",
                    Status = "recorded",
                    RecordedAtUtc = older,
                },
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-020304-ghi",
                    EvidenceId = "EVIDENCE-20260412-020304-jkl",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "workspace_submit",
                    ProblemKind = "protected_truth_root_requested",
                    Severity = "blocking",
                    Summary = "Agent was asked to edit protected truth directly.",
                    BlockedCommand = "manual edit .ai/tasks/graph.json",
                    RecommendedFollowUp = "Open governed Runtime follow-up work.",
                    Status = "recorded",
                    RecordedAtUtc = newer,
                },
            ]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-follow-up-candidates", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("agent_problem_follow_up_candidates_operator_review_required", surface.OverallPosture);
        Assert.True(surface.FollowUpCandidatesReady);
        Assert.Equal(2, surface.RecordedProblemCount);
        Assert.Equal(2, surface.CandidateCount);
        Assert.Equal(1, surface.GovernedCandidateCount);
        Assert.Equal(1, surface.WatchlistCandidateCount);
        Assert.Equal(0, surface.RepeatedPatternCount);
        Assert.Equal(1, surface.BlockingCandidateCount);
        Assert.Equal("carves pilot follow-up --json", surface.JsonCommandEntry);
        Assert.Equal("carves inspect runtime-agent-problem-follow-up-candidates", surface.InspectCommandEntry);
        Assert.Contains(surface.Candidates, candidate =>
            candidate.ProblemKind == "protected_truth_root_requested"
            && candidate.CandidateStatus == "governed_follow_up_candidate"
            && candidate.RecommendedTriageLane == "protected_truth_root_policy_review"
            && candidate.BlockingCount == 1);
        Assert.Contains(surface.Candidates, candidate =>
            candidate.ProblemKind == "next_command_ambiguous"
            && candidate.CandidateStatus == "watchlist_only"
            && candidate.RecommendedTriageLane == "pilot_next_or_stage_status_review");
        Assert.Contains(surface.CandidateRules, rule => rule.Contains("operator review prompts", StringComparison.Ordinal));
        Assert.Contains(surface.OperatorReviewQuestions, question => question.Contains("acceptance evidence", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not create cards", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentProblemFollowUpDecisionPlan_ProjectsAcceptRejectWaitChoices()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        var older = DateTimeOffset.Parse("2026-04-12T01:02:03Z");
        var newer = DateTimeOffset.Parse("2026-04-12T02:03:04Z");

        var surface = new RuntimeAgentProblemFollowUpDecisionPlanService(
            workspace.RootPath,
            () =>
            [
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-010203-abc",
                    EvidenceId = "EVIDENCE-20260412-010203-def",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "intent_capture",
                    ProblemKind = "next_command_ambiguous",
                    Severity = "low",
                    Summary = "Agent could not classify the next command.",
                    Status = "recorded",
                    RecordedAtUtc = older,
                },
                new PilotProblemIntakeRecord
                {
                    ProblemId = "PROBLEM-20260412-020304-ghi",
                    EvidenceId = "EVIDENCE-20260412-020304-jkl",
                    RepoRoot = workspace.RootPath,
                    RepoId = "target-repo",
                    CurrentStageId = "workspace_submit",
                    ProblemKind = "protected_truth_root_requested",
                    Severity = "blocking",
                    Summary = "Agent was asked to edit protected truth directly.",
                    BlockedCommand = "manual edit .ai/tasks/graph.json",
                    RecommendedFollowUp = "Open governed Runtime follow-up work.",
                    Status = "recorded",
                    RecordedAtUtc = newer,
                },
            ]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-follow-up-decision-plan", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("agent_problem_follow_up_decision_plan_ready_for_operator_review", surface.OverallPosture);
        Assert.True(surface.DecisionPlanReady);
        Assert.True(surface.DecisionRequired);
        Assert.Equal(2, surface.CandidateCount);
        Assert.Equal(1, surface.OperatorReviewItemCount);
        Assert.Equal(1, surface.WatchlistItemCount);
        Assert.Equal("carves pilot follow-up-plan --json", surface.JsonCommandEntry);
        Assert.Equal("carves inspect runtime-agent-problem-follow-up-decision-plan", surface.InspectCommandEntry);
        Assert.Contains(surface.DecisionItems, item =>
            item.ProblemKind == "protected_truth_root_requested"
            && item.RecommendedDecision == "operator_review_required"
            && item.DecisionOptions.Contains("accept_as_governed_planning_input"));
        Assert.Contains(surface.DecisionItems, item =>
            item.ProblemKind == "next_command_ambiguous"
            && item.RecommendedDecision == "wait_for_more_evidence"
            && item.DecisionOptions.Contains("accept_as_governed_planning_input_after_operator_override"));
        Assert.Contains(surface.OperatorDecisionChecklist, item => item.Contains("intent draft", StringComparison.Ordinal));
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("read-only", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not record durable operator decisions", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentProblemFollowUpDecisionRecord_ProjectsNoDecisionRequiredReadback()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var surface = new RuntimeAgentProblemFollowUpDecisionRecordService(workspace.RootPath, () => []).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-agent-problem-follow-up-decision-record", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", surface.DecisionPlanDocumentPath);
        Assert.Equal("agent_problem_follow_up_decision_record_no_decision_required", surface.OverallPosture);
        Assert.True(surface.DecisionPlanReady);
        Assert.False(surface.DecisionRequired);
        Assert.True(surface.DecisionRecordReady);
        Assert.True(surface.RecordAuditReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.Equal(0, surface.RequiredDecisionCandidateCount);
        Assert.Equal("carves pilot follow-up-record --json", surface.JsonCommandEntry);
        Assert.Equal("carves inspect runtime-agent-problem-follow-up-decision-record", surface.InspectCommandEntry);
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("does not create cards", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not authorize", StringComparison.Ordinal));
    }


    [Fact]
    public void AgentProblemFollowUpDecisionRecord_RequiresOperatorRecordForGovernedCandidate()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var surface = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [CreateBlockingPilotProblem(workspace.RootPath)]).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("agent_problem_follow_up_decision_record_waiting_for_operator_decision", surface.OverallPosture);
        Assert.True(surface.DecisionPlanReady);
        Assert.True(surface.DecisionRequired);
        Assert.False(surface.DecisionRecordReady);
        Assert.True(surface.RecordAuditReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.Equal(1, surface.RequiredDecisionCandidateCount);
        Assert.Equal(1, surface.MissingDecisionCandidateCount);
        Assert.Contains(surface.RequiredDecisionCandidateIds, candidateId => candidateId.Contains("PROTECTED-TRUTH-ROOT-REQUESTED", StringComparison.Ordinal));
        Assert.Contains(surface.Gaps, gap => gap.StartsWith("agent_problem_follow_up_decision_record_missing:", StringComparison.Ordinal));
        Assert.Contains("record-follow-up-decision", surface.RecommendedNextAction, StringComparison.Ordinal);
    }


    [Fact]
    public void AgentProblemFollowUpDecisionRecord_AcceptDecisionRequiresEvidenceAndReadback()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        var service = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [CreateBlockingPilotProblem(workspace.RootPath)]);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
        }));

        Assert.Contains("acceptance evidence is required", exception.Message, StringComparison.Ordinal);
        Assert.Contains("readback command is required", exception.Message, StringComparison.Ordinal);
    }


    [Fact]
    public void AgentProblemFollowUpDecisionRecord_RecordsAcceptedDecisionAndRequiresCommit()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline follow-up record truth");
        var service = new RuntimeAgentProblemFollowUpDecisionRecordService(
            workspace.RootPath,
            () => [CreateBlockingPilotProblem(workspace.RootPath)]);

        var record = service.Record(new AgentProblemFollowUpDecisionRecordRequest
        {
            Decision = "accept",
            AllCandidates = true,
            Reason = "operator accepts this as governed planning input",
            Operator = "test-operator",
            AcceptanceEvidence = "Problem PROBLEM-20260412-020304-ghi proves protected truth root confusion.",
            ReadbackCommand = "carves pilot follow-up-record --json",
        });
        var beforeCommit = service.Build();
        RunGit(workspace.RootPath, "add", ".ai/runtime/agent-problem-follow-up-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record agent problem follow-up decision");
        var afterCommit = service.Build();

        Assert.Equal("accept_as_governed_planning_input", record.Decision);
        Assert.Equal("runtime-agent-problem-follow-up-decision-plan", record.SourceSurfaceId);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", record.PhaseDocumentPath);
        Assert.StartsWith(".ai/runtime/agent-problem-follow-up-decisions/", record.RecordPath, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, record.RecordPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(beforeCommit.DecisionRecordReady);
        Assert.True(beforeCommit.RecordAuditReady);
        Assert.False(beforeCommit.DecisionRecordCommitReady);
        Assert.Equal(0, beforeCommit.MissingDecisionCandidateCount);
        Assert.Equal(1, beforeCommit.UntrackedDecisionRecordCount);
        Assert.Contains(beforeCommit.Gaps, gap => gap.StartsWith("agent_problem_follow_up_decision_record_uncommitted:", StringComparison.Ordinal));
        Assert.True(afterCommit.DecisionRecordReady);
        Assert.True(afterCommit.RecordAuditReady);
        Assert.True(afterCommit.DecisionRecordCommitReady);
        Assert.Equal(1, afterCommit.RecordCount);
        Assert.Equal(1, afterCommit.CurrentPlanRecordCount);
        Assert.Equal(1, afterCommit.ValidCurrentPlanRecordCount);
        Assert.Equal(0, afterCommit.MissingDecisionCandidateCount);
        Assert.Equal(0, afterCommit.UncommittedDecisionRecordCount);
        Assert.Contains(afterCommit.DecisionRecordIds, id => id == record.DecisionRecordId);
    }

}
