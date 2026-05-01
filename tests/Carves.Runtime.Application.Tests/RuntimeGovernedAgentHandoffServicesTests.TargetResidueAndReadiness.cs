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
    public void TargetResiduePolicy_ProjectsLocalResidueAndIgnoreSuggestions()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var surface = new RuntimeTargetResiduePolicyService(workspace.RootPath).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-target-residue-policy", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("target_residue_policy_ready_with_ignore_suggestions", surface.OverallPosture);
        Assert.True(surface.CommitClosureComplete);
        Assert.True(surface.ResiduePolicyReady);
        Assert.True(surface.ProductProofCanRemainComplete);
        Assert.False(surface.TargetGitWorktreeClean);
        Assert.Equal(2, surface.ResiduePathCount);
        Assert.Equal(0, surface.StagePathCount);
        Assert.Equal(0, surface.OperatorReviewRequiredPathCount);
        Assert.Empty(surface.Gaps);
        Assert.Contains(surface.ResiduePaths, path => path == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.ResiduePaths, path => path == ".carves-platform/live-state/session.json");
        Assert.Contains(surface.SuggestedIgnoreEntries, entry => entry == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.SuggestedIgnoreEntries, entry => entry == ".carves-platform/");
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("does not write .gitignore", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not mutate .gitignore", StringComparison.Ordinal));
    }


    [Fact]
    public void TargetIgnoreDecisionPlan_ProjectsOperatorReviewedIgnoreCandidates()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var surface = new RuntimeTargetIgnoreDecisionPlanService(workspace.RootPath).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-target-ignore-decision-plan", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("target_ignore_decision_plan_ready_for_operator_review", surface.OverallPosture);
        Assert.True(surface.IgnoreDecisionPlanReady);
        Assert.True(surface.IgnoreDecisionRequired);
        Assert.True(surface.ProductProofCanRemainComplete);
        Assert.True(surface.CanKeepResidueLocal);
        Assert.True(surface.CanApplyIgnoreAfterReview);
        Assert.Equal(2, surface.DecisionCandidateCount);
        Assert.Equal(2, surface.MissingIgnoreEntryCount);
        Assert.Contains(surface.MissingIgnoreEntries, entry => entry == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.MissingIgnoreEntries, entry => entry == ".carves-platform/");
        Assert.Contains(surface.GitIgnorePatchPreview, line => line == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.DecisionCandidates, candidate =>
            candidate.Entry == ".carves-platform/"
            && candidate.OperatorApprovalRequired
            && candidate.DecisionOptions.Contains("add_to_gitignore_after_review"));
        Assert.Empty(surface.Gaps);
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not write", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void TargetIgnoreDecisionPlan_DoesNotRequireDecisionWhenResidueAlreadyIgnored()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile(".gitignore", ".ai/runtime/attach.lock.json\n.carves-platform/\n");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var surface = new RuntimeTargetIgnoreDecisionPlanService(workspace.RootPath).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("target_ignore_decision_plan_no_residue", surface.OverallPosture);
        Assert.True(surface.IgnoreDecisionPlanReady);
        Assert.False(surface.IgnoreDecisionRequired);
        Assert.False(surface.CanApplyIgnoreAfterReview);
        Assert.Equal(0, surface.DecisionCandidateCount);
        Assert.Equal(0, surface.MissingIgnoreEntryCount);
        Assert.Empty(surface.MissingIgnoreEntries);
        Assert.Empty(surface.GitIgnorePatchPreview);
    }


    [Fact]
    public void TargetIgnoreDecisionRecord_RequiresOperatorDecisionForMissingIgnoreEntries()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var surface = new RuntimeTargetIgnoreDecisionRecordService(workspace.RootPath).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-target-ignore-decision-record", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("target_ignore_decision_record_waiting_for_operator_decision", surface.OverallPosture);
        Assert.True(surface.IgnoreDecisionPlanReady);
        Assert.True(surface.IgnoreDecisionRequired);
        Assert.False(surface.DecisionRecordReady);
        Assert.True(surface.RecordAuditReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.Equal(0, surface.UncommittedDecisionRecordCount);
        Assert.False(surface.ProductProofCanRemainComplete);
        Assert.Equal(2, surface.RequiredDecisionEntryCount);
        Assert.Equal(2, surface.MissingDecisionEntryCount);
        Assert.Contains(surface.MissingDecisionEntries, entry => entry == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.MissingDecisionEntries, entry => entry == ".carves-platform/");
        Assert.Contains(surface.Gaps, gap => gap == "target_ignore_decision_record_missing:.ai/runtime/attach.lock.json");
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("does not automatically mutate .gitignore", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not write", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void TargetIgnoreDecisionRecord_RecordsKeepLocalDecisionForCurrentPlan()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var service = new RuntimeTargetIgnoreDecisionRecordService(workspace.RootPath);
        var record = service.Record(new TargetIgnoreDecisionRecordRequest
        {
            Decision = "keep_local",
            AllEntries = true,
            Reason = "operator accepted local CARVES residue",
            Operator = "test-operator",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/target-ignore-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record target ignore decision");

        var surface = service.Build();

        Assert.Equal("keep_local", record.Decision);
        Assert.Equal("runtime-target-ignore-decision-plan", record.SourceSurfaceId);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", record.PhaseDocumentPath);
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, record.RecordPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("target_ignore_decision_record_ready", surface.OverallPosture);
        Assert.True(surface.DecisionRecordReady);
        Assert.True(surface.RecordAuditReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.Equal(0, surface.DirtyDecisionRecordCount);
        Assert.Equal(0, surface.UntrackedDecisionRecordCount);
        Assert.Equal(0, surface.UncommittedDecisionRecordCount);
        Assert.True(surface.ProductProofCanRemainComplete);
        Assert.Equal(1, surface.RecordCount);
        Assert.Equal(1, surface.CurrentPlanRecordCount);
        Assert.Equal(1, surface.ValidCurrentPlanRecordCount);
        Assert.Equal(0, surface.StaleRecordCount);
        Assert.Equal(0, surface.InvalidRecordCount);
        Assert.Equal(0, surface.MalformedRecordCount);
        Assert.Equal(0, surface.ConflictingDecisionEntryCount);
        Assert.Equal(2, surface.RecordedDecisionEntryCount);
        Assert.Equal(0, surface.MissingDecisionEntryCount);
        Assert.Contains(surface.DecisionRecordIds, id => id == record.DecisionRecordId);
        Assert.Contains(surface.RecordedDecisionEntries, entry => entry == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.RecordedDecisionEntries, entry => entry == ".carves-platform/");
    }


    [Fact]
    public void TargetIgnoreDecisionRecord_BlocksUncommittedDecisionRecordPaths()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var service = new RuntimeTargetIgnoreDecisionRecordService(workspace.RootPath);
        var record = service.Record(new TargetIgnoreDecisionRecordRequest
        {
            Decision = "keep_local",
            AllEntries = true,
            Reason = "operator accepted local CARVES residue",
            Operator = "test-operator",
        });

        var surface = service.Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("target_ignore_decision_record_waiting_for_record_commit", surface.OverallPosture);
        Assert.False(surface.DecisionRecordReady);
        Assert.True(surface.RecordAuditReady);
        Assert.False(surface.DecisionRecordCommitReady);
        Assert.False(surface.ProductProofCanRemainComplete);
        Assert.Equal(1, surface.RecordCount);
        Assert.Equal(1, surface.UntrackedDecisionRecordCount);
        Assert.Equal(1, surface.UncommittedDecisionRecordCount);
        Assert.Contains(surface.UncommittedDecisionRecordPaths, path => path == record.RecordPath);
        Assert.Contains(surface.Gaps, gap => gap == $"target_ignore_decision_record_uncommitted:{record.RecordPath}");
    }


    [Fact]
    public void TargetIgnoreDecisionRecord_BlocksConflictingCurrentPlanDecisions()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile(".ai/runtime.json", "{}");
        workspace.WriteFile("PROJECT.md", "# target\n");
        RunGit(workspace.RootPath, "init");
        RunGit(workspace.RootPath, "config", "user.email", "carves-tests@example.invalid");
        RunGit(workspace.RootPath, "config", "user.name", "CARVES Tests");
        RunGit(workspace.RootPath, "add", ".");
        RunGit(workspace.RootPath, "commit", "-m", "baseline target truth");
        workspace.WriteFile(".ai/runtime/attach.lock.json", "{}");
        workspace.WriteFile(".carves-platform/live-state/session.json", "{}");

        var service = new RuntimeTargetIgnoreDecisionRecordService(workspace.RootPath);
        service.Record(new TargetIgnoreDecisionRecordRequest
        {
            Decision = "keep_local",
            AllEntries = true,
            Reason = "operator accepted local CARVES residue",
            Operator = "test-operator",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/target-ignore-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record initial target ignore decision");
        service.Record(new TargetIgnoreDecisionRecordRequest
        {
            Decision = "manual_cleanup_after_review",
            Entries = [".ai/runtime/attach.lock.json"],
            Reason = "operator later requested cleanup for one entry",
            Operator = "test-operator",
        });
        RunGit(workspace.RootPath, "add", ".ai/runtime/target-ignore-decisions");
        RunGit(workspace.RootPath, "commit", "-m", "Record conflicting target ignore decision");

        var surface = service.Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("target_ignore_decision_record_blocked_by_record_audit", surface.OverallPosture);
        Assert.False(surface.DecisionRecordReady);
        Assert.False(surface.RecordAuditReady);
        Assert.True(surface.DecisionRecordCommitReady);
        Assert.Equal(0, surface.UncommittedDecisionRecordCount);
        Assert.False(surface.ProductProofCanRemainComplete);
        Assert.Equal(2, surface.RecordCount);
        Assert.Equal(2, surface.CurrentPlanRecordCount);
        Assert.Equal(2, surface.ValidCurrentPlanRecordCount);
        Assert.Equal(1, surface.ConflictingDecisionEntryCount);
        Assert.Contains(surface.ConflictingDecisionEntries, entry => entry == ".ai/runtime/attach.lock.json");
        Assert.Contains(surface.Gaps, gap => gap == "target_ignore_decision_record_conflict:.ai/runtime/attach.lock.json");
    }


    [Fact]
    public void ExternalConsumerResourcePack_ProjectsRuntimeResourcesWithoutTargetPrerequisite()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("docs/runtime/runtime-governed-agent-handoff-proof.md", "# proof");
        workspace.WriteFile("docs/session-gateway/governed-agent-handoff-proof.md", "# gateway proof");
        workspace.WriteFile("carves", "#!/usr/bin/env bash");
        workspace.WriteFile("carves.ps1", "# wrapper");
        workspace.WriteFile("carves.cmd", "@echo off");

        var surface = new RuntimeExternalConsumerResourcePackService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-external-consumer-resource-pack", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("external_consumer_resource_pack_ready", surface.OverallPosture);
        Assert.True(surface.ResourcePackComplete);
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_CLI_ACTIVATION_PLAN.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_EXTERNAL_CONSUMER_RESOURCE_PACK.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md");
        Assert.Contains(surface.RuntimeOwnedResources, resource => resource.Path == "docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md");
        Assert.Contains(surface.TargetGeneratedResources, resource => resource.Path == ".ai/AGENT_BOOTSTRAP.md" && resource.MaterializationCommand == "carves agent bootstrap --write");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot invocation --json" && entry.SurfaceId == "runtime-cli-invocation-contract");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot activation --json" && entry.SurfaceId == "runtime-cli-activation-plan");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot dist-smoke --json" && entry.SurfaceId == "runtime-local-dist-freshness-smoke");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot readiness --json" && entry.SurfaceId == "runtime-alpha-external-use-readiness");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot dist-binding --json" && entry.SurfaceId == "runtime-target-dist-binding-plan");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot target-proof --json" && entry.SurfaceId == "runtime-frozen-dist-target-readback-proof");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot resources --json" && entry.SurfaceId == "runtime-external-consumer-resource-pack");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves agent start --json" && entry.SurfaceId == "runtime-agent-thread-start");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves agent context --json" && entry.SurfaceId == "runtime-agent-short-context");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves api runtime-markdown-read-path-budget" && entry.SurfaceId == "runtime-markdown-read-path-budget");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves api runtime-worker-execution-audit <query>" && entry.SurfaceId == "runtime-worker-execution-audit");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves api runtime-governance-surface-coverage-audit" && entry.SurfaceId == "runtime-governance-surface-coverage-audit");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves api runtime-default-workflow-proof" && entry.SurfaceId == "runtime-default-workflow-proof");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot problem-intake --json" && entry.SurfaceId == "runtime-agent-problem-intake");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot triage --json" && entry.SurfaceId == "runtime-agent-problem-triage-ledger");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot follow-up --json" && entry.SurfaceId == "runtime-agent-problem-follow-up-candidates");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot follow-up-plan --json" && entry.SurfaceId == "runtime-agent-problem-follow-up-decision-plan");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot follow-up-record --json" && entry.SurfaceId == "runtime-agent-problem-follow-up-decision-record");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot record-follow-up-decision <decision> --all --reason <text>" && entry.SurfaceId == "runtime-agent-problem-follow-up-decision-record");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot report-problem <json-path> --json" && entry.SurfaceId == "runtime-agent-problem-intake");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot residue --json" && entry.SurfaceId == "runtime-target-residue-policy");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot ignore-plan --json" && entry.SurfaceId == "runtime-target-ignore-decision-plan");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot ignore-record --json" && entry.SurfaceId == "runtime-target-ignore-decision-record");
        Assert.Contains(surface.CommandEntries, entry => entry.Command == "carves pilot record-ignore-decision <decision> --all --reason <text>" && entry.SurfaceId == "runtime-target-ignore-decision-record");
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("no specific external repo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot status --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves agent start --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves agent context --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves api runtime-markdown-read-path-budget");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot invocation --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot activation --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot dist-smoke --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot readiness --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot problem-intake --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot triage --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot follow-up --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot follow-up-plan --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot follow-up-record --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot dist-binding --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot target-proof --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot residue --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot ignore-plan --json");
        Assert.Contains(surface.RequiredReadbackCommands, command => command == "carves pilot ignore-record --json");
        Assert.Contains(surface.NonClaims, claim => claim.Contains("specific external target repo", StringComparison.Ordinal));
    }


    [Fact]
    public void AlphaExternalUseReadiness_ProjectsRuntimeOwnedReadinessWithoutTargetClosure()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);
        workspace.WriteFile("docs/runtime/runtime-governed-agent-handoff-proof.md", "# proof");
        workspace.WriteFile("docs/session-gateway/repeatability-readiness.md", "# repeatability");

        var service = new RuntimeAlphaExternalUseReadinessService(
            workspace.RootPath,
            () => new RuntimeLocalDistFreshnessSmokeSurface
            {
                OverallPosture = "local_dist_freshness_smoke_ready",
                LocalDistFreshnessSmokeReady = true,
                ManifestSourceCommitMatchesSourceHead = true,
                SourceGitWorktreeClean = true,
                CandidateDistRoot = "D:/Projects/CARVES.AI/.dist/CARVES.Runtime-0.2.0-beta.1",
                ManifestSourceCommit = "abc123",
                SourceGitHead = "abc123",
                IsValid = true,
            },
            () => new RuntimeExternalConsumerResourcePackSurface
            {
                OverallPosture = "external_consumer_resource_pack_ready",
                ResourcePackComplete = true,
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotGuideSurface
            {
                OverallPosture = "productized_pilot_guide_ready",
                IsValid = true,
            },
            () => new RuntimeSessionGatewayPrivateAlphaHandoffSurface
            {
                OverallPosture = "private_alpha_deliverable_ready",
                IsValid = true,
            },
            () => new RuntimeSessionGatewayRepeatabilitySurface
            {
                OverallPosture = "repeatable_private_alpha_ready",
                IsValid = true,
            });

        var surface = service.Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-alpha-external-use-readiness", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.PhaseDocumentPath);
        Assert.Equal("alpha_external_use_readiness_ready", surface.OverallPosture);
        Assert.Equal(RuntimeAlphaVersion.Current, surface.AlphaVersion);
        Assert.True(surface.AlphaExternalUseReady);
        Assert.True(surface.FrozenLocalDistReady);
        Assert.True(surface.ExternalConsumerResourcePackReady);
        Assert.True(surface.GovernedAgentHandoffReady);
        Assert.True(surface.ProductizedPilotGuideReady);
        Assert.True(surface.SessionGatewayPrivateAlphaReady);
        Assert.True(surface.SessionGatewayRepeatabilityReady);
        Assert.True(surface.ProductPilotProofRequiredPerTarget);
        Assert.Contains(surface.ReadinessChecks, check => check.CheckId == "session_gateway_private_alpha" && !check.BlocksAlphaUse);
        Assert.Contains(surface.ReadinessChecks, check => check.CheckId == "session_gateway_repeatability" && !check.BlocksAlphaUse);
        Assert.Contains(surface.ReadinessChecks, check => check.CheckId == "target_product_pilot_proof" && !check.BlocksAlphaUse && check.Ready);
        Assert.Contains(surface.MinimumOperatorReadbacks, command => command == "carves agent start --json");
        Assert.Contains(surface.MinimumOperatorReadbacks, command => command == "carves pilot readiness --json");
        Assert.Contains(surface.MinimumOperatorReadbacks, command => command == "carves pilot problem-intake --json");
        Assert.Contains(surface.MinimumOperatorReadbacks, command => command == "carves pilot triage --json");
        Assert.Contains(surface.MinimumOperatorReadbacks, command => command == "carves pilot follow-up --json");
        Assert.Contains(surface.ExternalTargetStartCommands, command => command.Contains("agent start --json", StringComparison.Ordinal));
        Assert.Contains(surface.ExternalTargetStartCommands, command => command.Contains("pilot readiness --json", StringComparison.Ordinal));
        Assert.Contains(surface.ExternalTargetStartCommands, command => command.Contains("pilot problem-intake --json", StringComparison.Ordinal));
        Assert.Contains(surface.ExternalTargetStartCommands, command => command.Contains("pilot triage --json", StringComparison.Ordinal));
        Assert.Contains(surface.ExternalTargetStartCommands, command => command.Contains("pilot follow-up --json", StringComparison.Ordinal));
        Assert.Contains(surface.BoundaryRules, rule => rule.Contains("not target product completion", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("specific target repo", StringComparison.Ordinal));
    }


    [Fact]
    public void ExternalTargetPilotStart_ProjectsStartBundleAndNextCommand()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var service = new RuntimeExternalTargetPilotStartService(
            workspace.RootPath,
            () => new RuntimeAlphaExternalUseReadinessSurface
            {
                OverallPosture = "alpha_external_use_readiness_ready",
                AlphaExternalUseReady = true,
                CandidateDistRoot = "D:/Projects/CARVES.AI/.dist/CARVES.Runtime-0.2.0-beta.1",
                IsValid = true,
                Gaps = ["alpha_readiness_advisory_not_ready:session_gateway_repeatability"],
            },
            () => new RuntimeCliInvocationContractSurface
            {
                OverallPosture = "cli_invocation_contract_ready",
                InvocationContractComplete = true,
                RecommendedInvocationMode = "local_dist_wrapper",
                RuntimeRootKind = "local_dist",
                IsValid = true,
            },
            () => new RuntimeExternalConsumerResourcePackSurface
            {
                OverallPosture = "external_consumer_resource_pack_ready",
                ResourcePackComplete = true,
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_blocked_by_runtime_init",
                RuntimeInitialized = false,
                CurrentStageId = "attach_target",
                CurrentStageOrder = 1,
                CurrentStageStatus = "blocked",
                NextCommand = "carves init [target-path] --json",
                IsValid = true,
                Gaps = ["runtime_not_initialized"],
            });

        var start = service.BuildStart();
        var next = service.BuildNext();

        Assert.True(start.IsValid, string.Join(Environment.NewLine, start.Errors));
        Assert.Equal("runtime-external-target-pilot-start", start.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", start.ProductClosurePhase);
        Assert.Equal("external_target_pilot_start_bundle_ready", start.OverallPosture);
        Assert.True(start.PilotStartBundleReady);
        Assert.Equal("attach_target", start.CurrentStageId);
        Assert.Equal("carves init [target-path] --json", start.NextGovernedCommand);
        Assert.Contains(start.StartReadbackCommands, command => command.Contains("pilot start --json", StringComparison.Ordinal));
        Assert.Contains(start.StartReadbackCommands, command => command.Contains("pilot problem-intake --json", StringComparison.Ordinal));
        Assert.Contains(start.StartReadbackCommands, command => command.Contains("pilot triage --json", StringComparison.Ordinal));
        Assert.Contains(start.StartReadbackCommands, command => command.Contains("pilot follow-up --json", StringComparison.Ordinal));
        Assert.Contains(start.StartReadbackCommands, command => command.Contains("pilot next --json", StringComparison.Ordinal));
        Assert.Contains(start.AgentOperatingRules, rule => rule.Contains("pilot next", StringComparison.Ordinal));
        Assert.Contains(start.AgentOperatingRules, rule => rule.Contains("pilot report-problem", StringComparison.Ordinal));
        Assert.Contains(start.StopAndReportTriggers, trigger => trigger.Contains("protected truth root", StringComparison.Ordinal));
        Assert.Contains(start.Gaps, gap => gap == "runtime-product-closure-pilot-status:runtime_not_initialized");
        Assert.DoesNotContain(start.Gaps, gap => gap.StartsWith("runtime-alpha-external-use-readiness:", StringComparison.Ordinal));
        Assert.Contains("carves init [target-path] --json", start.RecommendedNextAction, StringComparison.Ordinal);
        Assert.DoesNotContain("problem-intake", start.RecommendedNextAction, StringComparison.Ordinal);
        Assert.DoesNotContain("triage", start.RecommendedNextAction, StringComparison.Ordinal);

        Assert.True(next.IsValid, string.Join(Environment.NewLine, next.Errors));
        Assert.Equal("runtime-external-target-pilot-next", next.SurfaceId);
        Assert.Equal("external_target_pilot_next_ready", next.OverallPosture);
        Assert.True(next.ReadyToRunNextCommand);
        Assert.Equal("carves init [target-path] --json", next.NextGovernedCommand);
        Assert.Contains("carves init [target-path] --json", next.RecommendedNextAction, StringComparison.Ordinal);
        Assert.DoesNotContain(next.Gaps, gap => gap.StartsWith("runtime-alpha-external-use-readiness:", StringComparison.Ordinal));
    }


    [Fact]
    public void ExternalTargetPilotStart_KeepsAlphaReadinessGapsWhenAlphaUseIsBlocked()
    {
        using var workspace = new TemporaryWorkspace();
        WritePilotStatusDocs(workspace);

        var service = new RuntimeExternalTargetPilotStartService(
            workspace.RootPath,
            () => new RuntimeAlphaExternalUseReadinessSurface
            {
                OverallPosture = "alpha_external_use_readiness_blocked",
                AlphaExternalUseReady = false,
                CandidateDistRoot = "D:/Projects/CARVES.AI/.dist/CARVES.Runtime-0.2.0-beta.1",
                IsValid = true,
                Gaps = ["alpha_readiness_check_not_ready:frozen_local_dist"],
            },
            () => new RuntimeCliInvocationContractSurface
            {
                OverallPosture = "cli_invocation_contract_ready",
                InvocationContractComplete = true,
                RecommendedInvocationMode = "local_dist_wrapper",
                RuntimeRootKind = "local_dist",
                IsValid = true,
            },
            () => new RuntimeExternalConsumerResourcePackSurface
            {
                OverallPosture = "external_consumer_resource_pack_ready",
                ResourcePackComplete = true,
                IsValid = true,
            },
            () => new RuntimeGovernedAgentHandoffProofSurface
            {
                OverallPosture = "bounded_governed_agent_handoff_proof_ready",
                IsValid = true,
            },
            () => new RuntimeProductClosurePilotStatusSurface
            {
                OverallPosture = "pilot_status_product_pilot_proof_required",
                RuntimeInitialized = true,
                CurrentStageId = "product_pilot_proof",
                CurrentStageOrder = 25,
                CurrentStageStatus = "ready",
                NextCommand = "carves pilot proof --json",
                IsValid = true,
            });

        var start = service.BuildStart();
        var next = service.BuildNext();

        Assert.Equal("external_target_pilot_start_bundle_blocked", start.OverallPosture);
        Assert.False(start.PilotStartBundleReady);
        Assert.Contains(start.Gaps, gap => gap == "alpha_external_use_not_ready");
        Assert.Contains(start.Gaps, gap => gap == "runtime-alpha-external-use-readiness:alpha_readiness_check_not_ready:frozen_local_dist");

        Assert.Equal("external_target_pilot_next_blocked", next.OverallPosture);
        Assert.False(next.ReadyToRunNextCommand);
        Assert.Contains(next.Gaps, gap => gap == "alpha_external_use_not_ready");
        Assert.Contains(next.Gaps, gap => gap == "runtime-alpha-external-use-readiness:alpha_readiness_check_not_ready:frozen_local_dist");
    }

}
