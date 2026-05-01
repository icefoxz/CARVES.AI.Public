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
    public void AdapterHandoffContract_ProjectsCliAcpAndMcpLanes()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-adapter-handoff-contract.md", "# adapter");
        workspace.WriteFile("docs/session-gateway/adapter-handoff-contract.md", "# gateway adapter");

        var surface = new RuntimeAdapterHandoffContractService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-adapter-handoff-contract", surface.SurfaceId);
        Assert.Equal("cli_first", surface.BaselineLaneId);
        Assert.Contains(surface.Lanes, lane => lane.LaneId == "cli_first" && lane.TransportPosture == "portable_baseline");
        Assert.Contains(surface.Lanes, lane => lane.LaneId == "acp_second" && lane.NonAuthorityBoundaries.Any(boundary => boundary.Contains("planning truth", StringComparison.Ordinal)));
        Assert.Contains(surface.Lanes, lane => lane.LaneId == "mcp_optional" && lane.NonAuthorityBoundaries.Any(boundary => boundary.Contains("optional acceleration", StringComparison.Ordinal)));
        Assert.Contains(surface.NonClaims, item => item.Contains("full ACP", StringComparison.Ordinal));
    }


    [Fact]
    public void ProtectedTruthRootPolicy_ProjectsClassifiedRootsAndRemediation()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-protected-truth-root-policy.md", "# protected roots");
        workspace.WriteFile(".ai/PROJECT_BOUNDARY.md", "# boundary");

        var surface = new RuntimeProtectedTruthRootPolicyService(workspace.RootPath).Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-protected-truth-root-policy", surface.SurfaceId);
        Assert.Contains(surface.ProtectedRoots, root => root.Root == ".ai/tasks/" && root.Classification == "task_truth");
        Assert.Contains(surface.ProtectedRoots, root => root.Root == ".ai/artifacts/reviews/" && root.UnauthorizedMutationOutcome == "block_before_writeback");
        Assert.Contains(surface.DeniedRoots, root => root.Classification == "secret_material");
        var violation = RuntimeProtectedTruthRootPolicyService.ClassifyViolation(".ai/tasks/graph.json");
        Assert.Equal("task_truth", violation.ProtectedClassification);
        Assert.Contains("governed", violation.RemediationAction, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void WorkspaceMutationAudit_ProjectsChangedPathsAndLeaseViolations()
    {
        using var workspace = new TemporaryWorkspace();
        var taskId = "T-HANDOFF-AUDIT-001";
        var task = new TaskNode
        {
            TaskId = taskId,
            CardId = "CARD-HANDOFF",
            Title = "Audit task",
            Description = "Audit workspace mutations.",
            Status = DomainTaskStatus.Pending,
            TaskType = TaskType.Execution,
            Priority = "P1",
            Scope = ["docs/runtime/"],
            Acceptance = ["audit projects violations"],
        };
        var taskGraphService = new TaskGraphService(
            new InMemoryTaskGraphRepository(new DomainTaskGraph([task])),
            new Carves.Runtime.Application.TaskGraph.TaskScheduler());
        var leaseRepository = new InMemoryManagedWorkspaceLeaseRepository();
        leaseRepository.Save(new ManagedWorkspaceLeaseSnapshot
        {
            Leases =
            [
                new ManagedWorkspaceLease
                {
                    LeaseId = "lease-audit-001",
                    TaskId = taskId,
                    CardId = "CARD-HANDOFF",
                    Status = ManagedWorkspaceLeaseStatus.Active,
                    RepoRoot = workspace.RootPath,
                    WorkspacePath = Path.Combine(workspace.RootPath, "..", "audit-workspace"),
                    AllowedWritablePaths = ["docs/runtime/"],
                    ApprovalPosture = "host_routed_review_and_writeback_required",
                },
            ],
        });
        var pathPolicyService = new ManagedWorkspacePathPolicyService(workspace.RootPath, leaseRepository);
        var artifactRepository = new JsonRuntimeArtifactRepository(workspace.Paths);
        workspace.WriteFile(
            $".ai/execution/{taskId}/result.json",
            JsonSerializer.Serialize(
                new ResultEnvelope
                {
                    TaskId = taskId,
                    ExecutionRunId = "RUN-HANDOFF-AUDIT-001",
                    ExecutionEvidencePath = ".ai/artifacts/worker-executions/RUN-HANDOFF-AUDIT-001/evidence.json",
                    Status = "success",
                    Changes = new ResultEnvelopeChanges
                    {
                        FilesModified = ["docs/runtime/Allowed.md", "src/Outside.cs", ".ai/tasks/graph.json"],
                    },
                },
                JsonOptions));

        var service = new RuntimeWorkspaceMutationAuditService(
            workspace.RootPath,
            taskGraphService,
            artifactRepository,
            (id, changedPaths) => pathPolicyService.Evaluate(id, changedPaths));

        var surface = service.Build(taskId);

        Assert.Equal("runtime-workspace-mutation-audit", surface.SurfaceId);
        Assert.Equal("host_only", surface.Status);
        Assert.True(surface.LeaseAware);
        Assert.Equal("lease-audit-001", surface.LeaseId);
        Assert.Equal(3, surface.ChangedPathCount);
        Assert.False(surface.CanProceedToWriteback);
        Assert.Contains(surface.ChangedPaths, path => path.Path == "docs/runtime/Allowed.md" && path.PolicyClass == "workspace_open");
        Assert.Contains(surface.ChangedPaths, path => path.Path == "src/Outside.cs" && path.PolicyClass == "scope_escape");
        Assert.Contains(surface.ChangedPaths, path => path.Path == ".ai/tasks/graph.json" && path.PolicyClass == "host_only");
        Assert.Contains(surface.Blockers, blocker => blocker.BlockerId == "mutation_audit_scope_escape");
        Assert.Contains(surface.Blockers, blocker => blocker.BlockerId == "mutation_audit_host_only");
    }


    [Fact]
    public void GovernedAgentHandoffProof_ProjectsStagesAndConstraintClasses()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/runtime-governed-agent-handoff-proof.md", "# proof");
        workspace.WriteFile("docs/session-gateway/governed-agent-handoff-proof.md", "# gateway proof");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-0-baseline.md", "# phase 0");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-1-cli-distribution.md", "# phase 1");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-2-readiness-separation.md", "# phase 2");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-3-minimal-init-onboarding.md", "# phase 3");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-4-external-target-dogfood-proof.md", "# phase 4");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-5-real-project-pilot.md", "# phase 5");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-6-official-truth-writeback.md", "# phase 6");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-7-managed-workspace-execution.md", "# phase 7");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md", "# phase 8");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md", "# phase 9");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md", "# phase 10");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md", "# phase 11b");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md", "# phase 12");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md", "# phase 13");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-14-target-commit-plan.md", "# phase 14");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-15-target-commit-closure.md", "# phase 15");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md", "# phase 16");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md", "# phase 17");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md", "# phase 18");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md", "# phase 19");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md", "# phase 20");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md", "# phase 21");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md", "# phase 22");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", "# phase 23");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "# phase 24");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "# phase 25");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "# phase 26a");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "# phase 26");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md", "# phase 27");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md", "# phase 28");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md", "# phase 29");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md", "# phase 30");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md", "# phase 31");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "# phase 32");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "# phase 33");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "# phase 34");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "# phase 35");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "# phase 36");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", "# phase 37");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md", "# phase 38");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", "# phase 39");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", "# phase 40");

        var service = new RuntimeGovernedAgentHandoffProofService(
            workspace.RootPath,
            () => new RuntimeAgentWorkingModesSurface
            {
                ExternalAgentRecommendationPosture = "mode_e_recommended_for_packet_bound_handoff",
            },
            () => new RuntimeAdapterHandoffContractSurface
            {
                OverallPosture = "adapter_handoff_contract_ready",
            },
            () => new RuntimeProtectedTruthRootPolicySurface
            {
                OverallPosture = "protected_truth_root_policy_ready",
            });

        var surface = service.Build();

        Assert.True(surface.IsValid);
        Assert.Equal("runtime-governed-agent-handoff-proof", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", surface.ProductClosureCurrentDocumentPath);
        Assert.Equal("bounded_governed_agent_handoff_proof_ready", surface.OverallPosture);
        Assert.Contains(surface.ProofStages, stage => stage.StageId == "formal_planning_entry" && stage.Gate == "active_planning_slot_single_owner");
        Assert.Contains(surface.ProofStages, stage => stage.StageId == "pre_writeback_blocker_projection" && stage.EvidenceProjected.Contains("mutation-audit", StringComparison.Ordinal));
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot problem-intake [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-agent-problem-intake");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot triage [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-agent-problem-triage-ledger");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot follow-up [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-agent-problem-follow-up-candidates");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot follow-up-record [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-agent-problem-follow-up-decision-record");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot follow-up-intake [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-agent-problem-follow-up-planning-intake");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot follow-up-gate [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-agent-problem-follow-up-planning-gate");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot readiness [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-alpha-external-use-readiness");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot invocation [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-cli-invocation-contract");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot activation [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-cli-activation-plan");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot dist-smoke [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-local-dist-freshness-smoke");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot dist-binding [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-dist-binding-plan");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot target-proof [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-frozen-dist-target-readback-proof");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot guide [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-product-closure-pilot-guide");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot status [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-product-closure-pilot-status");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot resources [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-external-consumer-resource-pack");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot commit-hygiene [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-commit-hygiene");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot commit-plan [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-commit-plan");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot closure [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-commit-closure");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot residue [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-residue-policy");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot ignore-plan [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-ignore-decision-plan");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot ignore-record [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-target-ignore-decision-record");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot dist [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-local-dist-handoff");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "pilot proof [--json]");
        Assert.Contains(surface.RequiredColdReadbacks, readback => readback == "inspect runtime-product-pilot-proof");
        Assert.Contains(surface.ConstraintClasses, constraint => constraint.ClassId == "soft_advisory");
        Assert.Contains(surface.ConstraintClasses, constraint => constraint.ClassId == "hard_runtime_gate");
        Assert.Contains(surface.ConstraintClasses, constraint => constraint.ClassId == "vendor_optional");
        Assert.Contains(surface.ConstraintClasses, constraint => constraint.ClassId == "deferred");
    }


    [Fact]
    public void ProductClosurePilotGuide_ProjectsReadOnlyExternalProjectLoop()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-8-managed-workspace-writeback.md", "# phase 8");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-9-productized-pilot-guide.md", "# phase 9");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-10-productized-pilot-status.md", "# phase 10");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-11b-target-agent-bootstrap-pack.md", "# phase 11b");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-12-existing-target-bootstrap-repair.md", "# phase 12");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-13-target-commit-hygiene.md", "# phase 13");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-14-target-commit-plan.md", "# phase 14");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-16-local-dist-handoff.md", "# phase 16");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-17-product-pilot-proof.md", "# phase 17");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-18-external-consumer-resource-pack.md", "# phase 18");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-19-cli-invocation-contract.md", "# phase 19");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-20-cli-activation-plan.md", "# phase 20");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-21-target-dist-binding-plan.md", "# phase 21");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-22-local-dist-freshness-smoke.md", "# phase 22");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-23-frozen-dist-target-readback-proof.md", "# phase 23");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-24-wrapper-runtime-root-binding.md", "# phase 24");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-25-external-target-product-proof-closure.md", "# phase 25");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26a-product-closure-projection-cleanup.md", "# phase 26a");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-26-real-external-repo-pilot.md", "# phase 26");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-27-external-target-residue-policy.md", "# phase 27");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-28-target-ignore-decision-plan.md", "# phase 28");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-29-target-ignore-decision-record.md", "# phase 29");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-30-target-ignore-decision-record-audit.md", "# phase 30");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-31-target-ignore-decision-record-commit-readback.md", "# phase 31");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-32-alpha-external-use-readiness-rollup.md", "# phase 32");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-33-external-target-pilot-start-bundle.md", "# phase 33");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-34-agent-problem-intake.md", "# phase 34");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-35-agent-problem-triage-ledger.md", "# phase 35");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-36-agent-problem-follow-up-candidates.md", "# phase 36");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-37-agent-problem-follow-up-decision-plan.md", "# phase 37");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-38-agent-problem-follow-up-decision-record.md", "# phase 38");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-39-agent-problem-follow-up-planning-intake.md", "# phase 39");
        workspace.WriteFile("docs/runtime/carves-product-closure-phase-40-agent-problem-follow-up-planning-gate.md", "# phase 40");
        workspace.WriteFile("docs/guides/CARVES_PRODUCTIZED_PILOT_GUIDE.md", "# guide");
        workspace.WriteFile("docs/guides/CARVES_EXTERNAL_AGENT_QUICKSTART.md", "# external agent quickstart");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_INTAKE.md", "# problem intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_TRIAGE_LEDGER.md", "# problem triage");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_CANDIDATES.md", "# problem follow-up");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_PLAN.md", "# problem follow-up decision plan");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_DECISION_RECORD.md", "# problem follow-up decision record");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_INTAKE.md", "# problem follow-up planning intake");
        workspace.WriteFile("docs/guides/CARVES_AGENT_PROBLEM_FOLLOW_UP_PLANNING_GATE.md", "# problem follow-up planning gate");
        workspace.WriteFile("docs/guides/CARVES_CLI_INVOCATION_CONTRACT.md", "# invocation guide");
        workspace.WriteFile("docs/guides/CARVES_CLI_ACTIVATION_PLAN.md", "# activation guide");
        workspace.WriteFile("docs/guides/CARVES_TARGET_DIST_BINDING_PLAN.md", "# target dist binding guide");
        workspace.WriteFile("docs/guides/CARVES_LOCAL_DIST_FRESHNESS_SMOKE.md", "# local dist freshness smoke guide");
        workspace.WriteFile("docs/guides/CARVES_FROZEN_DIST_TARGET_READBACK_PROOF.md", "# frozen target proof guide");

        var surface = new RuntimeProductClosurePilotGuideService(workspace.RootPath).Build();

        Assert.True(surface.IsValid, string.Join(Environment.NewLine, surface.Errors));
        Assert.Equal("runtime-product-closure-pilot-guide", surface.SurfaceId);
        Assert.Equal("phase_40_agent_problem_follow_up_planning_gate_ready", surface.ProductClosurePhase);
        Assert.Equal("productized_pilot_guide_ready", surface.OverallPosture);
        Assert.Equal("carves pilot status --json", surface.StatusCommandEntry);
        Assert.Equal("read_only_productized_pilot_guide", surface.AuthorityModel);
        Assert.Contains(surface.Steps, step => step.StageId == "external_agent_thread_start" && step.Command.Contains("agent start", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "attach_target" && step.Command.Contains("carves init", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "target_agent_bootstrap" && step.Command.Contains("agent bootstrap", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "cli_invocation_contract" && step.Command.Contains("pilot invocation", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "cli_activation_plan" && step.Command.Contains("pilot activation", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "external_consumer_resource_pack" && step.Command.Contains("pilot resources", StringComparison.Ordinal));
        Assert.Contains("agent start --json", surface.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains("next_governed_command", surface.RecommendedNextAction, StringComparison.Ordinal);
        Assert.Contains(surface.Steps, step => step.StageId == "workspace_submit" && step.Command.Contains("submit-workspace", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "review_writeback" && step.AuthorityClass == "host_owned_writeback");
        Assert.Contains(surface.Steps, step => step.StageId == "target_commit_plan" && step.Command.Contains("pilot commit-plan", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "target_commit_closure" && step.Command.Contains("pilot closure", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "target_residue_policy" && step.Command.Contains("pilot residue", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "target_ignore_decision_plan" && step.Command.Contains("pilot ignore-plan", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "target_ignore_decision_record" && step.Command.Contains("pilot ignore-record", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "local_dist_freshness_smoke" && step.Command.Contains("pilot dist-smoke", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "target_dist_binding_plan" && step.Command.Contains("pilot dist-binding", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "local_dist_handoff" && step.Command.Contains("pilot dist", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "frozen_dist_target_readback_proof" && step.Command.Contains("pilot target-proof", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "product_pilot_proof" && step.Command.Contains("pilot proof", StringComparison.Ordinal));
        Assert.Contains(surface.Steps, step => step.StageId == "ready_for_new_intent" && step.Command == "carves discuss context");
        Assert.Contains(surface.CommitHygieneRules, rule => rule.Contains(".ai/runtime/live-state/", StringComparison.Ordinal));
        Assert.Contains(surface.NonClaims, claim => claim.Contains("does not create", StringComparison.Ordinal));
    }

}
