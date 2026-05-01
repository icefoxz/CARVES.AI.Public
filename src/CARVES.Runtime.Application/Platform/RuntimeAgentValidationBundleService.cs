namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeAgentValidationBundleService
{
    private readonly string repoRoot;

    public RuntimeAgentValidationBundleService(string repoRoot)
    {
        this.repoRoot = Path.GetFullPath(repoRoot);
    }

    public RuntimeAgentValidationBundleSurface Build()
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        const string boundaryDocumentPath = "docs/runtime/runtime-agent-governed-validation-bundle-test-hardening-contract.md";
        const string guidePath = "docs/guides/RUNTIME_AGENT_V1_VALIDATION_BUNDLE.md";
        const string workmapPath = "docs/runtime/runtime-agent-v1-delivery-workmap.md";
        const string architecturePath = "docs/runtime/runtime-agent-v1-architecture.md";

        ValidateDocument(boundaryDocumentPath, "Stage 6 validation bundle contract", errors);
        ValidateDocument(guidePath, "Runtime Agent v1 validation bundle guide", errors);
        ValidateDocument(workmapPath, "Runtime Agent v1 delivery workmap", errors);
        ValidateDocument(architecturePath, "Runtime Agent v1 architecture", errors);

        var lanes = BuildLanes();
        foreach (var lane in lanes)
        {
            foreach (var path in lane.StableEvidencePaths.Concat(lane.TestFileRefs))
            {
                ValidateDocument(path, $"Validation bundle path for lane '{lane.LaneId}'", errors);
            }
        }

        if (lanes.Count == 0)
        {
            errors.Add("No bounded validation lanes were projected.");
        }

        if (lanes.All(item => item.ValidationCommands.Count == 0))
        {
            warnings.Add("Validation bundle does not currently project executable test commands.");
        }

        return new RuntimeAgentValidationBundleSurface
        {
            BoundaryDocumentPath = boundaryDocumentPath,
            GuidePath = guidePath,
            WorkmapPath = workmapPath,
            ArchitecturePath = architecturePath,
            OverallPosture = errors.Count == 0
                ? "bounded_v1_validation_bundle_ready"
                : "blocked_by_validation_bundle_gaps",
            Lanes = lanes,
            RecommendedNextAction = errors.Count == 0
                ? "Inspect runtime-agent-validation-bundle, then run the listed bounded commands before claiming friend-trial or delivery readiness."
                : "Restore the missing Stage 6 validation bundle anchors before treating the v1 lane as repeatably validated.",
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            NonClaims =
            [
                "This bundle does not claim whole-suite perfection or global flake elimination.",
                "This bundle does not promote heavy or historically unstable integration batches into the default delivery gate.",
                "This bundle does not replace Runtime truth with test output; it packages stable evidence around existing Runtime-owned surfaces.",
            ],
        };
    }

    private static List<RuntimeAgentValidationBundleLaneSurface> BuildLanes()
    {
        return
        [
            new RuntimeAgentValidationBundleLaneSurface
            {
                LaneId = "contract_and_read_model_lane",
                Summary = "Keep the contract manifest and Stage 1-6 read models queryable before wider delivery claims.",
                RuntimeSurfaceRefs =
                [
                    "runtime-agent-bootstrap-packet",
                    "runtime-first-run-operator-packet",
                    "runtime-agent-validation-bundle",
                    "runtime-agent-failure-recovery-closure",
                ],
                StableEvidencePaths =
                [
                    "tests/Carves.Runtime.Application.Tests/TestData/runtime-contract-presence.manifest.json",
                    "docs/runtime/runtime-agent-governed-failure-classification-recovery-closure-contract.md",
                ],
                TestFileRefs =
                [
                    "tests/Carves.Runtime.Application.Tests/RuntimeContractTests.cs",
                    "tests/Carves.Runtime.Application.Tests/RuntimeAgentBootstrapSurfaceServiceTests.cs",
                    "tests/Carves.Runtime.Application.Tests/RuntimeAgentValidationBundleServiceTests.cs",
                    "tests/Carves.Runtime.Application.Tests/RuntimeAgentFailureRecoveryClosureServiceTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/RuntimeSurfaceCommandRegistryHostContractTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/RuntimeAgentValidationBundleHostContractTests.cs",
                ],
                ValidationCommands =
                [
                    "dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj --filter \"FullyQualifiedName~RuntimeContractTests|FullyQualifiedName~RuntimeAgentBootstrapSurfaceServiceTests|FullyQualifiedName~RuntimeAgentValidationBundleServiceTests|FullyQualifiedName~RuntimeAgentFailureRecoveryClosureServiceTests\"",
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter \"FullyQualifiedName~RuntimeSurfaceCommandRegistryHostContractTests|FullyQualifiedName~RuntimeAgentValidationBundleHostContractTests\"",
                ],
                Notes =
                [
                    "This lane keeps the default contract/read-model gate small and stable.",
                ],
            },
            new RuntimeAgentValidationBundleLaneSurface
            {
                LaneId = "attach_and_first_run_lane",
                Summary = "Prove startup, attach, cold wrapper entry, and first-run guidance on the frozen Runtime-owned lane.",
                RuntimeSurfaceRefs =
                [
                    "runtime-first-run-operator-packet",
                    "runtime-agent-validation-bundle",
                ],
                StableEvidencePaths =
                [
                    "docs/runtime/runtime-first-run-operator-packet.md",
                    "docs/guides/HOST_AND_PROVIDER_QUICKSTART.md",
                ],
                TestFileRefs =
                [
                    "tests/Carves.Runtime.IntegrationTests/FriendlyCliEntryTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/ColdHostCommandLauncherTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/RuntimeFirstRunOperatorPacketHostContractTests.cs",
                ],
                ValidationCommands =
                [
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter \"FullyQualifiedName~RuntimeFirstRunOperatorPacketHostContractTests|FullyQualifiedName~FriendlyCliEntryTests.Attach_WithoutHost_ShowsFriendlyHostEnsureGuidance|FullyQualifiedName~FriendlyCliEntryTests.Attach_FromNestedProjectDirectory_ResolvesGitRootWithoutRepoRootFlag|FullyQualifiedName~HostClientSurfaceTests.Attach_ThroughHostReportsAttachModeAndReadiness|FullyQualifiedName~HostClientSurfaceTests.ProjectRepoAttach_WithoutExplicitPath_UsesThinClientDiscoveryAndWritesRuntimeManifest|FullyQualifiedName~ColdHostCommandLauncherTests\"",
                ],
                Notes =
                [
                    "This lane keeps attach and first-run proof on stable host/CLI surfaces instead of cross-repo exploratory flows.",
                ],
            },
            new RuntimeAgentValidationBundleLaneSurface
            {
                LaneId = "gateway_and_governed_entry_lane",
                Summary = "Prove session binding, governed ingress, and thin shell routing without reopening gateway-owned truth.",
                RuntimeSurfaceRefs =
                [
                    "runtime-session-gateway-private-alpha-handoff",
                    "runtime-session-gateway-repeatability",
                ],
                StableEvidencePaths =
                [
                    "docs/session-gateway/session-gateway-v1.md",
                    "docs/session-gateway/gateway-boundary.md",
                ],
                TestFileRefs =
                [
                    "tests/Carves.Runtime.Application.Tests/RuntimeSessionGatewayServiceTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/SessionGatewayHostContractTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/SessionGatewayShellHostContractTests.cs",
                ],
                ValidationCommands =
                [
                    "dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj --filter \"FullyQualifiedName~RuntimeSessionGatewayServiceTests\"",
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter \"FullyQualifiedName~SessionGatewayShellHostContractTests|FullyQualifiedName~SessionGatewayHostContractTests.SessionGatewayGovernedIngress_RemainsThinUntilHostMutationForwarding\"",
                ],
                Notes =
                [
                    "This lane excludes the known mutation-forwarding timeout drift from the default bundle.",
                ],
            },
            new RuntimeAgentValidationBundleLaneSurface
            {
                LaneId = "governed_dev_lane",
                Summary = "Prove host status capability preflight, read-only task inspection, plan, dry-run gate, review/writeback hold, result-ingestion fail-closed posture, and sync-state routing on the stable governed dev lane.",
                RuntimeSurfaceRefs =
                [
                    "runtime-agent-task-overlay",
                    "execution-packet",
                    "execution-hardening",
                ],
                StableEvidencePaths =
                [
                    "docs/runtime/runtime-agent-governed-inspect-plan-surfaces-contract.md",
                    "docs/runtime/runtime-agent-governed-run-diff-review-surfaces-contract.md",
                    "docs/runtime/runtime-agent-governed-safety-validation-gate-hardening-contract.md",
                ],
                TestFileRefs =
                [
                    "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.RoutingAndAcceptedOperations.cs",
                    "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.ControlPlaneMutationRouting.cs",
                ],
                ValidationCommands =
                [
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter \"FullyQualifiedName~HostClientSurfaceTests.HostStatusJson_WithHost_ProjectsSelectedLoopCapabilityReadiness|FullyQualifiedName~HostClientSurfaceTests.PlanCard_WithHost_RoutesThroughResidentHost|FullyQualifiedName~HostClientSurfaceTests.TaskIngestResult_WithoutHost_RequiresExplicitHostEnsureAndDoesNotMutateTruth|FullyQualifiedName~HostClientSurfaceTests.TaskInspectWithRuns_WithHost_ProjectsCandidateReadBeforeDryRun|FullyQualifiedName~HostClientSurfaceTests.TaskRunDryRun_WithHost_RoutesThroughResidentHostAndPreservesRoleModeGate|FullyQualifiedName~HostClientSurfaceTests.ReviewTask_WithHost_RoutesThroughResidentHostAndMutatesTruth|FullyQualifiedName~HostClientSurfaceTests.ApproveReview_WithHost_RoutesThroughResidentHostAndMutatesTruth|FullyQualifiedName~HostClientSurfaceTests.SyncState_WithHost_RoutesThroughResidentHost\"",
                ],
                Notes =
                [
                    "This lane uses the stable dry-run role-mode gate instead of provider-backed live execution drift.",
                ],
            },
            new RuntimeAgentValidationBundleLaneSurface
            {
                LaneId = "failure_and_recovery_lane",
                Summary = "Prove explicit failure classification, verify/reconcile drift handling, and bounded operator handoff posture.",
                RuntimeSurfaceRefs =
                [
                    "runtime-agent-failure-recovery-closure",
                ],
                StableEvidencePaths =
                [
                    "docs/runtime/runtime-consistency-check.md",
                    "docs/runtime/delegated-worker-lifecycle-reconciliation.md",
                ],
                TestFileRefs =
                [
                    "tests/Carves.Runtime.Application.Tests/RuntimeAgentFailureRecoveryClosureServiceTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/RuntimeAgentFailureRecoveryClosureHostContractTests.cs",
                ],
                ValidationCommands =
                [
                    "dotnet test tests/Carves.Runtime.Application.Tests/Carves.Runtime.Application.Tests.csproj --filter \"FullyQualifiedName~RuntimeAgentFailureRecoveryClosureServiceTests\"",
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj --filter \"FullyQualifiedName~RuntimeAgentFailureRecoveryClosureHostContractTests\"",
                ],
                Notes =
                [
                    "This lane keeps failure proof bounded to Runtime-owned inspect/verify/reconcile surfaces rather than broad provider flake hunts.",
                    "Extended recovery drills such as RuntimeConsistencyCommandTests stay outside the default bundle until their historical drift is separately stabilized.",
                ],
            },
            new RuntimeAgentValidationBundleLaneSurface
            {
                LaneId = "host_lifecycle_and_entry_honesty_lane",
                Summary = "Prove resident-host startup transaction, stale-pointer recovery, explicit replace-stale boundaries, and default-entry honesty across operator surfaces.",
                RuntimeSurfaceRefs =
                [
                    "host status",
                    "doctor",
                    "runtime-product-closure-pilot-status",
                    "workbench",
                ],
                StableEvidencePaths =
                [
                    "docs/runtime/runtime-host-lifecycle-proof-gate.md",
                    "docs/guides/RUNTIME_OPERATOR_HOST_AND_LIFECYCLE_REFERENCE.md",
                    "scripts/beta/host-lifecycle-proof-lane.ps1",
                ],
                TestFileRefs =
                [
                    "tests/Carves.Runtime.IntegrationTests/HostClientSurfaceTests.HostLifecycle.cs",
                    "tests/Carves.Runtime.IntegrationTests/FriendlyCliEntryTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/ColdHostCommandLauncherTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/RuntimeGovernedAgentHandoffHostContractTests.cs",
                    "tests/Carves.Runtime.IntegrationTests/GuardCheckCliTests.cs",
                ],
                ValidationCommands =
                [
                    "pwsh ./scripts/beta/host-lifecycle-proof-lane.ps1",
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj -m:1 --filter \"FullyQualifiedName~HostStart_DeploysResidentHostOutsideSourceBuildOutput|FullyQualifiedName~HostEnsureJson_FailsClosedWhenAliveStaleDescriptorExists|FullyQualifiedName~HostEnsureJson_FailsClosedWhenStartupLockIsHeld|FullyQualifiedName~HostEnsureJson_ReconcilesHealthyExistingGenerationWhenActiveDescriptorIsStale|FullyQualifiedName~HostStatusJson_ProjectsHealthyWithPointerRepairWhenActiveDescriptorIsRepaired|FullyQualifiedName~HostHonesty_ProjectsConsistentConflictAcrossHostStatusDoctorAndPilotStatus|FullyQualifiedName~Doctor_ProjectsHealthyWithPointerRepairWhenDoctorFirstReconcilesStalePointer|FullyQualifiedName~PilotStatusApi_ProjectsHealthyWithPointerRepairWhenPilotStatusFirstReconcilesStalePointer|FullyQualifiedName~HostReconcileJson_ReplaceStale_ReplacesConflictingGenerationAndStartsFreshHost|FullyQualifiedName~HostReconcileJson_ReplaceStale_DoesNotReplaceWhenHealthyGenerationCanBeReconciled|FullyQualifiedName~HostReconcileJson_ReplaceStale_DoesNotStartFreshHostWhenNoStaleConflictExists|FullyQualifiedName~HostReconcileJson_ReplaceStale_FailsClosedWhenConflictingProcessCannotBeTerminated|FullyQualifiedName~Workbench_CommandFallsBackWithConflictHonestyWhenResidentHostSessionConflicts|FullyQualifiedName~Workbench_CommandTracksCurrentHostAfterResidentRestart|FullyQualifiedName~HostStatus_RecoversTransientHandshakeAndReplacesPersistedStaleSnapshot\"",
                    "dotnet test tests/Carves.Runtime.IntegrationTests/Carves.Runtime.IntegrationTests.csproj -m:1 --filter \"FullyQualifiedName~Doctor_WhenHostSessionConflictExists_ProjectsConflictAndReconcileNextAction|FullyQualifiedName~Init_WhenHostSessionConflictExists_ProjectsJsonReconcileNextAction|FullyQualifiedName~Attach_WhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance|FullyQualifiedName~Run_WithHostTransportWhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance|FullyQualifiedName~Status_WhenHostSessionConflictExists_ShowsFriendlyReconcileGuidance|FullyQualifiedName~HostEnsureJson_StartsOrValidatesResidentHost|FullyQualifiedName~ColdLauncher_StatusConflictFallsBackWithReconcileGuidance|FullyQualifiedName~PilotStatusApi_ProjectsHostHonestyWhenResidentHostIsNotRunning|FullyQualifiedName~GuardRunJson_WithHostSessionConflict_ProjectsReconcileNextAction\"",
                ],
                Notes =
                [
                    "This lane is intentionally serial because the current integration build graph can collide on obj/ref locks under parallel dotnet test.",
                    "This lane proves default entry and cold-fallback honesty instead of stopping at low-level lifecycle helpers only.",
                ],
            },
        ];
    }

    private void ValidateDocument(string repoRelativePath, string label, List<string> errors)
    {
        var fullPath = Path.Combine(repoRoot, repoRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            errors.Add($"{label} '{repoRelativePath}' is missing.");
        }
    }
}
