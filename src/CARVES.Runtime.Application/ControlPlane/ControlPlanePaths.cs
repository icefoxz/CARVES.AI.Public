namespace Carves.Runtime.Application.ControlPlane;

public sealed record ControlPlanePaths(
    string RepoRoot,
    string AiRoot,
    string FailuresRoot,
    string PlatformRoot,
    string ConfigRoot,
    string TasksRoot,
    string RuntimeRoot,
    string RuntimeLiveStateRoot,
    string PlanningRoot,
    string PlanningCardDraftsRoot,
    string PlanningTaskGraphDraftsRoot,
    string OpportunitiesRoot,
    string PlatformReposRoot,
    string PlatformRuntimeRoot,
    string PlatformProvidersRoot,
    string PlatformWorkersRoot,
    string PlatformPoliciesRoot,
    string PlatformEventsRoot,
    string PlatformFleetRoot,
    string PlatformRuntimeStateRoot,
    string PlatformProviderLiveStateRoot,
    string TaskNodesRoot,
    string CardsRoot,
    string ArtifactsRoot,
    string WorkerArtifactsRoot,
    string WorkerExecutionArtifactsRoot,
    string WorkerPermissionArtifactsRoot,
    string PlannerArtifactsRoot,
    string SafetyArtifactsRoot,
    string ReviewArtifactsRoot,
    string ProviderArtifactsRoot,
    string MergeArtifactsRoot,
    string RuntimeFailureArtifactsRoot,
    string TaskGraphFile,
    string RuntimeSessionFile,
    string RuntimeFailureFile,
    string OpportunitiesFile,
    string SystemConfigFile,
    string AiProviderConfigFile,
    string PlannerAutonomyConfigFile,
    string CarvesCodeStandardFile,
    string WorkerOperationalPolicyFile,
    string SafetyRulesFile,
    string ModuleDependenciesFile,
    string PlatformRepoRegistryFile,
    string PlatformHostRegistryFile,
    string PlatformRepoRuntimeRegistryFile,
    string PlatformRuntimeInstancesFile,
    string PlatformProviderRegistryFile,
    string PlatformProviderQuotaFile,
    string PlatformProviderHealthFile,
    string PlatformWorkerRegistryFile,
    string PlatformWorkerLeasesFile,
    string PlatformActorSessionsFile,
    string PlatformOwnershipFile,
    string PlatformGovernanceFile,
    string PlatformAgentGovernanceKernelFile,
    string PlatformCodeUnderstandingEngineFile,
    string PlatformMinimalWorkerBaselineFile,
    string PlatformDurableExecutionSemanticsFile,
    string PlatformRepoAuthoredGateLoopFile,
    string PlatformGitNativeCodingLoopFile,
    string PlatformDelegationPolicyFile,
    string PlatformApprovalPolicyFile,
    string PlatformRoleGovernancePolicyFile,
    string PlatformWorkerSelectionPolicyFile,
    string PlatformTrustProfilesFile,
    string PlatformHostInvokePolicyFile,
    string PlatformGovernanceContinuationGatePolicyFile,
    string PlatformGovernanceEventsFile,
    string PlatformPermissionAuditFile,
    string PlatformIncidentTimelineFile,
    string PlatformOperatorOsEventsFile,
    string PlatformWorkerRoutingOverridesFile,
    string PlatformActiveRoutingProfileFile,
    string PlatformCandidateRoutingProfileFile,
    string PlatformQualificationMatrixFile,
    string PlatformQualificationRunLedgerFile,
    string PlatformDelegatedRunLifecycleFile,
    string PlatformDelegatedRunRecoveryLedgerFile,
    string PlatformHostSnapshotFile,
    string RuntimeWorktreeStateFile,
    string RuntimeManagedWorkspaceLeaseStateFile)
{
    public string MemoryRoot => Path.Combine(AiRoot, "memory");

    public string MemoryInboxRoot => Path.Combine(MemoryRoot, "inbox");

    public string MemorySessionRoot => Path.Combine(MemoryRoot, "session");

    public string MemoryAuditsRoot => Path.Combine(MemoryRoot, "audits");

    public string MemoryPromotionsRoot => Path.Combine(MemoryRoot, "promotions");

    public string RuntimeContextRoot => Path.Combine(RuntimeRoot, "context");

    public string RuntimeContextBudgetsRoot => Path.Combine(RuntimeContextRoot, "budgets");

    public string RuntimeRequestEnvelopeAttributionRoot => Path.Combine(RuntimeContextRoot, "llm-request-envelope-attribution");

    public string RuntimeConsumerRouteGraphRoot => Path.Combine(RuntimeContextRoot, "consumer-route-graph");

    public string RuntimeConsumerRouteGraphSurfacesFile => Path.Combine(RuntimeConsumerRouteGraphRoot, "surfaces.json");

    public string RuntimeConsumerRouteGraphEdgesFile => Path.Combine(RuntimeConsumerRouteGraphRoot, "route_edges.json");

    public string EvidenceRoot => Path.Combine(AiRoot, "evidence");

    public string EvidenceTranscriptsRoot => Path.Combine(EvidenceRoot, "transcripts");

    public string EvidenceExcerptsRoot => Path.Combine(EvidenceRoot, "excerpts");

    public string EvidenceFactsRoot => Path.Combine(EvidenceRoot, "facts");

    public string PlatformFleetLiveStateRoot => Path.Combine(PlatformRuntimeStateRoot, "fleet");

    public string PlatformSessionLiveStateRoot => Path.Combine(PlatformRuntimeStateRoot, "sessions");

    public string PlatformWorkerLiveStateRoot => Path.Combine(PlatformRuntimeStateRoot, "workers");

    public string PlatformEventRuntimeRoot => Path.Combine(PlatformRuntimeStateRoot, "events");

    public string PlatformHostStateRoot => Path.Combine(PlatformRuntimeStateRoot, "host");

    public string PlatformDelegationLiveStateRoot => Path.Combine(PlatformRuntimeStateRoot, "delegation");

    public string PlatformHostRegistryLiveStateFile => Path.Combine(PlatformFleetLiveStateRoot, "hosts.json");

    public string PlatformRepoRuntimeRegistryLiveStateFile => Path.Combine(PlatformFleetLiveStateRoot, "repos.json");

    public string PlatformRuntimeInstancesLiveStateFile => Path.Combine(PlatformSessionLiveStateRoot, "runtime_instances.json");

    public string PlatformActorSessionsLiveStateFile => Path.Combine(PlatformSessionLiveStateRoot, "actor_sessions.json");

    public string PlatformOwnershipLiveStateFile => Path.Combine(PlatformSessionLiveStateRoot, "ownership.json");

    public string PlatformHostSessionLiveStateFile => Path.Combine(PlatformHostStateRoot, "host_session.json");

    public string PlatformWorkerRegistryLiveStateFile => Path.Combine(PlatformWorkerLiveStateRoot, "registry.json");

    public string PlatformWorkerLeasesLiveStateFile => Path.Combine(PlatformWorkerLiveStateRoot, "leases.json");

    public string PlatformWorkerSupervisorStateLiveStateFile => Path.Combine(PlatformWorkerLiveStateRoot, "supervisor_state.json");

    public string PlatformGovernanceEventsRuntimeFile => Path.Combine(PlatformEventRuntimeRoot, "governance_events.json");

    public string PlatformPermissionAuditRuntimeFile => Path.Combine(PlatformEventRuntimeRoot, "permission_audit.json");

    public string PlatformIncidentTimelineRuntimeFile => Path.Combine(PlatformEventRuntimeRoot, "incidents.json");

    public string PlatformOperatorOsEventsRuntimeFile => Path.Combine(PlatformEventRuntimeRoot, "operator_os_events.json");

    public string PlatformAgentGatewayReportsRuntimeFile => Path.Combine(PlatformEventRuntimeRoot, "agent_gateway_reports.json");

    public string PlatformDelegatedRunLifecycleLiveStateFile => Path.Combine(PlatformDelegationLiveStateRoot, "delegated_run_lifecycles.json");

    public string PlatformDelegatedRunRecoveryLedgerLiveStateFile => Path.Combine(PlatformDelegationLiveStateRoot, "delegated_run_recovery_ledger.json");

    public string PlatformHostSnapshotLiveStateFile => Path.Combine(PlatformHostStateRoot, "host_snapshot.json");

    public static ControlPlanePaths FromRepoRoot(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var aiRoot = Path.Combine(root, ".ai");
        var failuresRoot = Path.Combine(aiRoot, "failures");
        var platformRoot = Path.Combine(root, ".carves-platform");
        var configRoot = Path.Combine(aiRoot, "config");
        var tasksRoot = Path.Combine(aiRoot, "tasks");
        var runtimeRoot = Path.Combine(aiRoot, "runtime");
        var runtimeLiveStateRoot = Path.Combine(runtimeRoot, "live-state");
        var planningRoot = Path.Combine(runtimeRoot, "planning");
        var opportunitiesRoot = Path.Combine(aiRoot, "opportunities");
        var platformReposRoot = Path.Combine(platformRoot, "repos");
        var platformRuntimeRoot = Path.Combine(platformRoot, "sessions");
        var platformProvidersRoot = Path.Combine(platformRoot, "providers");
        var platformWorkersRoot = Path.Combine(platformRoot, "workers");
        var platformPoliciesRoot = Path.Combine(platformRoot, "policies");
        var platformEventsRoot = Path.Combine(platformRoot, "events");
        var platformFleetRoot = Path.Combine(platformRoot, "fleet");
        var platformRuntimeStateRoot = Path.Combine(platformRoot, "runtime-state");
        var platformProviderLiveStateRoot = Path.Combine(platformRuntimeStateRoot, "providers");

        return new ControlPlanePaths(
            root,
            aiRoot,
            failuresRoot,
            platformRoot,
            configRoot,
            tasksRoot,
            runtimeRoot,
            runtimeLiveStateRoot,
            planningRoot,
            Path.Combine(planningRoot, "card-drafts"),
            Path.Combine(planningRoot, "taskgraph-drafts"),
            opportunitiesRoot,
            platformReposRoot,
            platformRuntimeRoot,
            platformProvidersRoot,
            platformWorkersRoot,
            platformPoliciesRoot,
            platformEventsRoot,
            platformFleetRoot,
            platformRuntimeStateRoot,
            platformProviderLiveStateRoot,
            Path.Combine(tasksRoot, "nodes"),
            Path.Combine(tasksRoot, "cards"),
            Path.Combine(aiRoot, "artifacts"),
            Path.Combine(aiRoot, "artifacts", "worker"),
            Path.Combine(aiRoot, "artifacts", "worker-executions"),
            Path.Combine(aiRoot, "artifacts", "worker-permissions"),
            Path.Combine(aiRoot, "artifacts", "planner"),
            Path.Combine(aiRoot, "artifacts", "safety"),
            Path.Combine(aiRoot, "artifacts", "reviews"),
            Path.Combine(aiRoot, "artifacts", "provider"),
            Path.Combine(aiRoot, "artifacts", "merge-candidates"),
            Path.Combine(aiRoot, "artifacts", "runtime-failures"),
            Path.Combine(tasksRoot, "graph.json"),
            Path.Combine(runtimeLiveStateRoot, "session.json"),
            Path.Combine(runtimeLiveStateRoot, "last_failure.json"),
            Path.Combine(opportunitiesRoot, "index.json"),
            Path.Combine(configRoot, "system.json"),
            Path.Combine(configRoot, "ai_provider.json"),
            Path.Combine(configRoot, "planner_autonomy.json"),
            Path.Combine(configRoot, "carves_code_standard.json"),
            Path.Combine(configRoot, "worker_operational_policy.json"),
            Path.Combine(configRoot, "safety_rules.json"),
            Path.Combine(configRoot, "module_dependencies.json"),
            Path.Combine(platformReposRoot, "registry.json"),
            Path.Combine(platformFleetRoot, "hosts.json"),
            Path.Combine(platformFleetRoot, "repos.json"),
            Path.Combine(platformRuntimeRoot, "runtime_instances.json"),
            Path.Combine(platformProvidersRoot, "registry.json"),
            Path.Combine(platformProviderLiveStateRoot, "quota_usage.json"),
            Path.Combine(platformProviderLiveStateRoot, "health.json"),
            Path.Combine(platformWorkersRoot, "registry.json"),
            Path.Combine(platformWorkersRoot, "leases.json"),
            Path.Combine(platformRuntimeRoot, "actor_sessions.json"),
            Path.Combine(platformRuntimeRoot, "ownership.json"),
            Path.Combine(platformPoliciesRoot, "governance.json"),
            Path.Combine(platformPoliciesRoot, "agent-governance-kernel.json"),
            Path.Combine(platformPoliciesRoot, "code-understanding-engine.json"),
            Path.Combine(platformPoliciesRoot, "minimal-worker-baseline.json"),
            Path.Combine(platformPoliciesRoot, "durable-execution-semantics.json"),
            Path.Combine(platformPoliciesRoot, "repo-authored-gate-loop.json"),
            Path.Combine(platformPoliciesRoot, "git-native-coding-loop.json"),
            Path.Combine(platformPoliciesRoot, "delegation.policy.json"),
            Path.Combine(platformPoliciesRoot, "approval.policy.json"),
            Path.Combine(platformPoliciesRoot, "role-governance.policy.json"),
            Path.Combine(platformPoliciesRoot, "worker-selection.policy.json"),
            Path.Combine(platformPoliciesRoot, "trust-profiles.json"),
            Path.Combine(platformPoliciesRoot, "host-invoke.policy.json"),
            Path.Combine(platformPoliciesRoot, "governance-continuation-gate.policy.json"),
            Path.Combine(platformEventsRoot, "governance_events.json"),
            Path.Combine(platformEventsRoot, "permission_audit.json"),
            Path.Combine(platformEventsRoot, "incidents.json"),
            Path.Combine(platformEventsRoot, "operator_os_events.json"),
            Path.Combine(platformRuntimeStateRoot, "worker-routing-overrides.json"),
            Path.Combine(platformRuntimeStateRoot, "active_routing_profile.json"),
            Path.Combine(platformRuntimeStateRoot, "candidate_routing_profile.json"),
            Path.Combine(platformRuntimeStateRoot, "qualification_matrix.json"),
            Path.Combine(platformRuntimeStateRoot, "qualification_run_ledger.json"),
            Path.Combine(platformRuntimeStateRoot, "delegated_run_lifecycles.json"),
            Path.Combine(platformRuntimeStateRoot, "delegated_run_recovery_ledger.json"),
            Path.Combine(platformRuntimeStateRoot, "host_snapshot.json"),
            Path.Combine(runtimeLiveStateRoot, "worktrees.json"),
            Path.Combine(runtimeLiveStateRoot, "managed_workspace_leases.json"));
    }
}
