using Carves.Runtime.Application.CodeGraph;
using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Interaction;
using Carves.Runtime.Application.Orchestration;
using Carves.Runtime.Application.Platform;
using Carves.Runtime.Application.Platform.SurfaceModels;
using Carves.Runtime.Application.Planning;
using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Planning;
using Carves.Runtime.Domain.Runtime;
using Carves.Runtime.Domain.Safety;
using Carves.Runtime.Domain.Tasks;
using DomainTaskGraph = Carves.Runtime.Domain.Tasks.TaskGraph;

namespace Carves.Runtime.Application.ControlPlane;

internal static class OperatorRuntimeStatusFormatter
{
    public static OperatorCommandResult Status(
        string repoRoot,
        ControlPlanePaths paths,
        SystemConfig systemConfig,
        AiProviderConfig aiProviderConfig,
        CarvesCodeStandard carvesCodeStandard,
        PlannerAutonomyPolicy plannerAutonomyPolicy,
        bool aiClientConfigured,
        SafetyRules safetyRules,
        OpportunitySnapshot opportunities,
        int moduleDependencyCount,
        DomainTaskGraph graph,
        DispatchProjection dispatch,
        RuntimeSessionState? session,
        PlatformStatusSummary platformStatus,
        InteractionSnapshot interaction,
        OperationalSummary operationalSummary,
        RuntimeAgentWorkingModesSurface agentWorkingModes,
        RuntimeFormalPlanningPostureSurface formalPlanningPosture,
        RuntimeVendorNativeAccelerationSurface vendorNativeAcceleration,
        RuntimeSessionGatewayGovernanceAssistSurface sessionGatewayGovernanceAssist,
        RuntimeAcceptanceContractIngressPolicySurface acceptanceContractIngressPolicy)
    {
        var profiles = aiProviderConfig.GetProfiles();
        var roleProfiles = aiProviderConfig.GetRoleProfiles();
        var roleOverrides = aiProviderConfig.GetRoleOverrides();
        var topPressure = sessionGatewayGovernanceAssist.ChangePressures.FirstOrDefault();
        var dispatchProjectionService = new DispatchProjectionService();
        var lines = new List<string>
        {
            "CARVES.Runtime .NET skeleton",
            $"Repo root: {repoRoot}",
            $"AI root: {paths.AiRoot}",
            $"Configured repo name: {systemConfig.RepoName}",
            $"Runtime stage: {RuntimeStageInfo.CurrentStage}",
            $"Next stage: {RuntimeStageInfo.NextStage}",
            $"CARVES standard: v{carvesCodeStandard.Version}",
            $"CARVES authority: {string.Join(", ", carvesCodeStandard.Authority.RecorderWritableBy)} writes Recorder; {string.Join(", ", carvesCodeStandard.Authority.DomainEventsEmittedBy)} emits domain events",
            $"CARVES applicability: {(carvesCodeStandard.Applicability.DirectoryLayoutRequired ? "directory-forced" : "interpretation-first")} / runtime separated={(carvesCodeStandard.Applicability.RuntimePurpose.Length > 0 ? "yes" : "unknown")}",
            $"CARVES AI-friendly: file lines {carvesCodeStandard.AiFriendlyArchitecture.RecommendedFileLinesLowerBound}-{carvesCodeStandard.AiFriendlyArchitecture.RecommendedFileLinesUpperBound}; refactor > {carvesCodeStandard.AiFriendlyArchitecture.RefactorFileLinesThreshold}",
            $"CARVES physical split: logical strict={carvesCodeStandard.PhysicalSplitting.LogicalLayersStrict}; physical elastic={carvesCodeStandard.PhysicalSplitting.PhysicalSplittingElastic}",
            $"CARVES naming: {carvesCodeStandard.ExtremeNaming.NamingGrammar}; canonical roles={carvesCodeStandard.ExtremeNaming.CanonicalArchitecturalTerms.Count + carvesCodeStandard.ExtremeNaming.CanonicalExecuteTerms.Count}; forbidden generic words={carvesCodeStandard.ExtremeNaming.ForbiddenGenericWords.Count}",
            $"CARVES dependency: one-way={carvesCodeStandard.DependencyContract.DependencyDirectionOneWay}; recorder model={carvesCodeStandard.DependencyContract.RecorderAccessModel}; forbidden rules={carvesCodeStandard.DependencyContract.ForbiddenDiagnosticRules.Count}",
            $"Code directories: {string.Join(", ", CodeDirectoryDiscoveryPolicy.ResolveEffectiveDirectories(repoRoot, systemConfig))}",
            $"AI provider: global={aiProviderConfig.Provider}; active_profile={aiProviderConfig.ProfileId ?? "(none)"}; default_profile={aiProviderConfig.DefaultProfileId ?? "(none)"}; {(aiClientConfigured ? "configured" : "fallback/null")}",
            $"Planner autonomy: rounds={plannerAutonomyPolicy.MaxPlannerRounds}; generated tasks={plannerAutonomyPolicy.MaxGeneratedTasks}; refactor scope={plannerAutonomyPolicy.MaxRefactorScopeFiles}; opportunities/round={plannerAutonomyPolicy.MaxOpportunitiesPerRound}",
            $"Safety limits: {safetyRules.MaxFilesChanged} files / {safetyRules.MaxLinesChanged} lines",
            $"Module dependency entries: {moduleDependencyCount}",
            $"Known opportunities: {opportunities.Items.Count} (open {opportunities.Items.Count(item => item.Status == OpportunityStatus.Open)})",
            $"Platform repos: {platformStatus.RegisteredRepoCount}",
            $"Platform runtime instances: {platformStatus.RuntimeInstanceCount}",
            $"Platform providers: {platformStatus.ProviderCount}",
            $"Platform worker nodes: {platformStatus.WorkerNodeCount}",
            $"Platform active leases: {platformStatus.ActiveLeaseCount}",
            $"Platform stale projections: {platformStatus.StaleProjectionCount}",
            $"Interaction mode: {interaction.ProtocolMode}",
            $"Conversation phase: {interaction.Protocol.CurrentPhase.ToString().ToLowerInvariant()}",
            $"Intent state: {interaction.Intent.State.ToString().ToLowerInvariant()}",
            $"Prompt kernel: {interaction.PromptKernel.KernelId}@{interaction.PromptKernel.Version}",
            $"Prompt template: {interaction.ActiveTemplate.TemplateId}@{interaction.ActiveTemplate.Version}",
            $"Project understanding: {interaction.ProjectUnderstanding.State.ToString().ToLowerInvariant()} ({interaction.ProjectUnderstanding.Action})",
            $"Project summary: {interaction.ProjectUnderstanding.Summary}",
            $"Interaction next action: {interaction.RecommendedNextAction}",
            $"Known cards: {graph.Cards.Count}",
            $"Known tasks: {graph.Tasks.Count}",
            $"Agent working mode: {agentWorkingModes.CurrentMode}",
            $"Strongest runtime working mode: {agentWorkingModes.StrongestRuntimeSupportedMode}",
            $"External agent recommended mode: {agentWorkingModes.ExternalAgentRecommendedMode}",
            $"External agent recommendation posture: {agentWorkingModes.ExternalAgentRecommendationPosture}",
            $"External agent constraint tier: {agentWorkingModes.ExternalAgentConstraintTier}",
            $"External agent stronger-mode blockers: {agentWorkingModes.ExternalAgentStrongerModeBlockerCount}",
            $"External agent first stronger-mode blocker: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerId ?? "(none)"}",
            $"External agent first stronger-mode blocker action: {agentWorkingModes.ExternalAgentFirstStrongerModeBlockerRequiredAction ?? "(none)"}",
            $"Mode E activation: {agentWorkingModes.ModeEOperationalActivationState}",
            $"Mode E activation task: {agentWorkingModes.ModeEActivationTaskId ?? "(none)"}",
            $"Mode E result return channel: {agentWorkingModes.ModeEActivationResultReturnChannel ?? "(none)"}",
            $"Mode E activation command: {agentWorkingModes.ModeEActivationCommands.FirstOrDefault() ?? "(none)"}",
            $"Mode E activation next action: {agentWorkingModes.ModeEActivationRecommendedNextAction}",
            $"Mode E activation blocking checks: {agentWorkingModes.ModeEActivationBlockingCheckCount}",
            $"Mode E activation first blocker: {agentWorkingModes.ModeEActivationFirstBlockingCheckId ?? "(none)"}",
            $"Mode E activation first blocker action: {agentWorkingModes.ModeEActivationFirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode E activation playbook: {agentWorkingModes.ModeEActivationPlaybookSummary}",
            $"Mode E activation playbook steps: {agentWorkingModes.ModeEActivationPlaybookStepCount}",
            $"Mode E activation first playbook command: {agentWorkingModes.ModeEActivationFirstPlaybookStepCommand ?? "(none)"}",
            $"Planning coupling posture: {agentWorkingModes.PlanningCouplingPosture}",
            $"Formal planning posture: {formalPlanningPosture.OverallPosture}",
            $"Formal planning entry trigger: {formalPlanningPosture.FormalPlanningEntryTriggerState}",
            $"Formal planning entry command: {formalPlanningPosture.FormalPlanningEntryCommand}",
            $"Formal planning entry next action: {formalPlanningPosture.FormalPlanningEntryRecommendedNextAction}",
            $"Active planning slot state: {formalPlanningPosture.ActivePlanningSlotState}",
            $"Active planning slot conflict reason: {(string.IsNullOrWhiteSpace(formalPlanningPosture.ActivePlanningSlotConflictReason) ? "(none)" : formalPlanningPosture.ActivePlanningSlotConflictReason)}",
            $"Active planning slot remediation: {formalPlanningPosture.ActivePlanningSlotRemediationAction}",
            $"Planning card invariant state: {formalPlanningPosture.PlanningCardInvariantState}",
            $"Planning card invariant violations: {formalPlanningPosture.PlanningCardInvariantViolationCount}",
            $"Planning card invariant remediation: {formalPlanningPosture.PlanningCardInvariantRemediationAction}",
            $"Active planning card fill state: {formalPlanningPosture.ActivePlanningCardFillState}",
            $"Active planning card fill missing required fields: {formalPlanningPosture.ActivePlanningCardFillMissingRequiredFieldCount}",
            $"Active planning card fill next action: {formalPlanningPosture.ActivePlanningCardFillRecommendedNextAction}",
            $"Active planning card: {formalPlanningPosture.PlanningCardId ?? "(none)"}",
            $"Active plan handle: {formalPlanningPosture.PlanHandle ?? "(none)"}",
            $"Managed workspace posture: {formalPlanningPosture.ManagedWorkspacePosture}",
            $"Vendor-native acceleration: {vendorNativeAcceleration.OverallPosture}",
            $"Codex reinforcement: {vendorNativeAcceleration.CodexReinforcementState}",
            $"Claude reinforcement: {vendorNativeAcceleration.ClaudeReinforcementState}",
            $"Dispatch state: {dispatch.State} ({dispatchProjectionService.DescribeIdleReason(dispatch.IdleReason)})",
            $"Next dispatchable task: {dispatch.NextTaskId ?? "(none)"}",
            $"Dispatch-blocking acceptance contract gaps: {dispatch.AcceptanceContractGapCount}",
            $"Dispatch-blocking formal planning gaps: {dispatch.PlanRequiredBlockCount}",
            $"Dispatch-blocking managed workspace gaps: {dispatch.WorkspaceRequiredBlockCount}",
            $"Mode C/D entry first blocked task: {dispatch.FirstBlockedTaskId ?? "(none)"}",
            $"Mode C/D entry first blocker: {dispatch.FirstBlockingCheckId ?? "(none)"}",
            $"Mode C/D entry first blocker action: {dispatch.FirstBlockingCheckRequiredAction ?? "(none)"}",
            $"Mode C/D entry first blocker command: {dispatch.FirstBlockingCheckRequiredCommand ?? "(none)"}",
            $"Mode C/D entry next command: {dispatch.RecommendedNextCommand ?? "(none)"}",
            $"Formal planning missing prerequisites: {formalPlanningPosture.MissingPrerequisites.Count}",
            $"Acceptance contract ingress policy: {acceptanceContractIngressPolicy.PolicySummary}",
            $"Session status: {session?.Status.ToString() ?? "(none)"}",
            $"Operational actionability: {operationalSummary.Actionability} ({operationalSummary.ActionabilityReason})",
            $"Operational summary: {operationalSummary.ActionabilitySummary}",
            $"Operational next action: {operationalSummary.RecommendedNextAction}",
            $"Session Gateway assist: {sessionGatewayGovernanceAssist.OverallPosture}",
            $"Session Gateway assist top pressure: {topPressure?.PressureKind ?? "(none)"} [{topPressure?.Level ?? "none"}]",
            $"Session Gateway assist next action: {sessionGatewayGovernanceAssist.RecommendedNextAction}",
            $"Projection writeback: {operationalSummary.ProjectionWritebackState} ({operationalSummary.ProjectionWritebackSummary})",
        };

        if (profiles.Count > 0)
        {
            lines.Add($"AI profiles: {string.Join(", ", profiles.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value.Provider}/{item.Value.Model}"))}");
        }

        if (roleProfiles.Count > 0)
        {
            lines.Add($"AI role bindings: {string.Join(", ", roleProfiles.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value}"))}");
        }

        if (roleOverrides.Count > 0)
        {
            lines.Add($"AI role overrides: {string.Join(", ", roleOverrides.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value.Provider ?? aiProviderConfig.Provider}"))}");
        }

        if (session is not null)
        {
            lines.Add($"Session active workers: {session.ActiveWorkerCount}");
            lines.Add($"Session active tasks: {(session.ActiveTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ActiveTaskIds))}");
            lines.Add($"Session review pending: {(session.ReviewPendingTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ReviewPendingTaskIds))}");
            lines.Add($"Session loop mode: {session.LoopMode}");
            lines.Add($"Session actionability: {RuntimeActionabilitySemantics.Describe(session.CurrentActionability)}");
            lines.Add($"Session next action: {RuntimeActionabilitySemantics.DescribeNextAction(session)}");
            lines.Add($"Session waiting reason: {session.WaitingReason ?? "(none)"}");
            lines.Add($"Session stop reason: {session.StopReason ?? "(none)"}");
            lines.Add($"Session pending permissions: {(session.PendingPermissionRequestIds.Count == 0 ? "(none)" : string.Join(", ", session.PendingPermissionRequestIds))}");
            lines.Add($"Session last permission request: {session.LastPermissionRequestId ?? "(none)"}");
            lines.Add($"Session last permission summary: {session.LastPermissionSummary ?? "(none)"}");
            lines.Add($"Session planner re-entry: {session.LastPlannerReentryOutcome ?? "(none)"}");
            lines.Add($"Session planner round: {session.PlannerRound}");
            lines.Add($"Session planner state: {PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState)}");
            lines.Add($"Session planner wake reason: {PlannerLifecycleSemantics.DescribeWakeReason(session.PlannerWakeReason)}");
            lines.Add($"Session planner pending wakes: {session.PendingPlannerWakeSignals.Count}");
            lines.Add($"Session planner last consumed wake: {session.LastConsumedPlannerWakeSummary ?? "(none)"}");
            lines.Add($"Session planner sleep reason: {PlannerLifecycleSemantics.DescribeSleepReason(session.PlannerSleepReason)}");
            lines.Add($"Session planner escalation: {PlannerLifecycleSemantics.DescribeEscalationReason(session.PlannerEscalationReason)}");
            lines.Add($"Session planner lease: {session.PlannerLeaseId ?? "(none)"}");
            lines.Add($"Session planner lease active: {session.PlannerLeaseActive}");
            lines.Add($"Session planner lease mode: {session.PlannerLeaseMode}");
            lines.Add($"Session planner lease owner: {session.PlannerLeaseOwner ?? "(none)"}");
            lines.Add($"Session planner adapter: {session.PlannerAdapterId ?? "(none)"}");
            lines.Add($"Session planner proposal: {session.PlannerProposalId ?? "(none)"}");
            lines.Add($"Session planner lifecycle reason: {session.PlannerLifecycleReason ?? "(none)"}");
            lines.Add($"Session opportunities detected: {session.DetectedOpportunityCount}");
            lines.Add($"Session opportunities evaluated: {session.EvaluatedOpportunityCount}");
            lines.Add($"Session opportunity source: {session.LastOpportunitySource ?? "(none)"}");
            lines.Add($"Session analysis reason: {session.AnalysisReason ?? "(none)"}");
            lines.Add($"Session worker run: {session.LastWorkerRunId ?? "(none)"}");
            lines.Add($"Session worker backend: {session.LastWorkerBackend ?? "(none)"}");
            lines.Add($"Session worker failure kind: {session.LastWorkerFailureKind}");
            lines.Add($"Session worker summary: {session.LastWorkerSummary ?? "(none)"}");
            lines.Add($"Session recovery action: {session.LastRecoveryAction}");
            lines.Add($"Session recovery reason: {session.LastRecoveryReason ?? "(none)"}");
            lines.Add($"Session last reason: {session.LastReason}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult SessionStatus(RuntimeSessionState? session)
    {
        if (session is null)
        {
            return OperatorCommandResult.Success("No runtime session is attached.");
        }

        return OperatorCommandResult.Success(
            $"Session ID: {session.SessionId}",
            $"Attached repo: {session.AttachedRepoRoot}",
            $"Status: {session.Status}",
            $"Loop mode: {session.LoopMode}",
            $"Dry run: {session.DryRun}",
            $"Ticks: {session.TickCount}",
            $"Active workers: {session.ActiveWorkerCount}",
            $"Current task: {session.CurrentTaskId ?? "(none)"}",
            $"Active tasks: {(session.ActiveTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ActiveTaskIds))}",
            $"Last review task: {session.LastReviewTaskId ?? "(none)"}",
            $"Review pending tasks: {(session.ReviewPendingTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.ReviewPendingTaskIds))}",
            $"Current actionability: {RuntimeActionabilitySemantics.Describe(session.CurrentActionability)}",
            $"Next action: {RuntimeActionabilitySemantics.DescribeNextAction(session)}",
            $"Waiting reason: {session.WaitingReason ?? "(none)"}",
            $"Waiting actionability: {RuntimeActionabilitySemantics.Describe(session.WaitingActionability)}",
            $"Stop reason: {session.StopReason ?? "(none)"}",
            $"Stop actionability: {RuntimeActionabilitySemantics.Describe(session.StopActionability)}",
            $"Pending permission requests: {(session.PendingPermissionRequestIds.Count == 0 ? "(none)" : string.Join(", ", session.PendingPermissionRequestIds))}",
            $"Last permission request: {session.LastPermissionRequestId ?? "(none)"}",
            $"Last permission summary: {session.LastPermissionSummary ?? "(none)"}",
            $"Planner re-entry: {session.LastPlannerReentryOutcome ?? "(none)"}",
            $"Planner re-entry tasks: {(session.LastPlannerReentryTaskIds.Count == 0 ? "(none)" : string.Join(", ", session.LastPlannerReentryTaskIds))}",
            $"Planner round: {session.PlannerRound}",
            $"Planner lifecycle state: {PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState)}",
            $"Planner wake reason: {PlannerLifecycleSemantics.DescribeWakeReason(session.PlannerWakeReason)}",
            $"Planner pending wakes: {session.PendingPlannerWakeSignals.Count}",
            $"Planner last consumed wake: {session.LastConsumedPlannerWakeSummary ?? "(none)"}",
            $"Planner sleep reason: {PlannerLifecycleSemantics.DescribeSleepReason(session.PlannerSleepReason)}",
            $"Planner escalation reason: {PlannerLifecycleSemantics.DescribeEscalationReason(session.PlannerEscalationReason)}",
            $"Planner lease: {session.PlannerLeaseId ?? "(none)"}",
            $"Planner lease active: {session.PlannerLeaseActive}",
            $"Planner lease mode: {session.PlannerLeaseMode}",
            $"Planner lease owner: {session.PlannerLeaseOwner ?? "(none)"}",
            $"Planner adapter: {session.PlannerAdapterId ?? "(none)"}",
            $"Planner proposal: {session.PlannerProposalId ?? "(none)"}",
            $"Planner lifecycle reason: {session.PlannerLifecycleReason ?? "(none)"}",
            $"Detected opportunities: {session.DetectedOpportunityCount}",
            $"Evaluated opportunities: {session.EvaluatedOpportunityCount}",
            $"Opportunity source: {session.LastOpportunitySource ?? "(none)"}",
            $"Analysis reason: {session.AnalysisReason ?? "(none)"}",
            $"Last worker run: {session.LastWorkerRunId ?? "(none)"}",
            $"Last worker backend: {session.LastWorkerBackend ?? "(none)"}",
            $"Last worker failure kind: {session.LastWorkerFailureKind}",
            $"Last worker summary: {session.LastWorkerSummary ?? "(none)"}",
            $"Last recovery action: {session.LastRecoveryAction}",
            $"Last recovery reason: {session.LastRecoveryReason ?? "(none)"}",
            $"Loop reason: {session.LoopReason}",
            $"Loop actionability: {RuntimeActionabilitySemantics.Describe(session.LoopActionability)}",
            $"Last reason: {session.LastReason}");
    }

    public static OperatorCommandResult SessionChanged(string action, RuntimeSessionState session)
    {
        return OperatorCommandResult.Success(
            $"{action} session {session.SessionId}.",
            $"Attached repo: {session.AttachedRepoRoot}",
            $"Status: {session.Status}",
            $"Loop mode: {session.LoopMode}",
            $"Last reason: {session.LastReason}");
    }

    public static OperatorCommandResult ContinuousLoop(ContinuousLoopResult result)
    {
        var lines = new List<string>
        {
            $"Continuous loop iterations: {result.IterationsRun}/{result.MaxIterations}",
            $"Loop result: {result.Message}",
        };

        foreach (var iteration in result.Iterations.Take(5))
        {
            lines.Add($"- iteration: {iteration.Message}");
        }

        if (result.LastPlannerReentry is not null)
        {
            lines.Add($"Planner re-entry outcome: {result.LastPlannerReentry.Outcome}");
            lines.Add($"Planner round: {result.LastPlannerReentry.PlannerRound}");
            lines.Add($"Opportunities detected/evaluated: {result.LastPlannerReentry.DetectedOpportunityCount}/{result.LastPlannerReentry.EvaluatedOpportunityCount}");
            if (result.LastPlannerReentry.ProposedTaskIds.Count > 0)
            {
                lines.Add($"Planner re-entry tasks: {string.Join(", ", result.LastPlannerReentry.ProposedTaskIds)}");
            }
        }

        if (result.Session is not null)
        {
            lines.Add($"Session status: {result.Session.Status}");
            lines.Add($"Current actionability: {RuntimeActionabilitySemantics.Describe(result.Session.CurrentActionability)}");
            lines.Add($"Next action: {RuntimeActionabilitySemantics.DescribeNextAction(result.Session)}");
            lines.Add($"Planner state: {PlannerLifecycleSemantics.DescribeState(result.Session.PlannerLifecycleState)}");
            lines.Add($"Planner lifecycle reason: {result.Session.PlannerLifecycleReason ?? "(none)"}");
            lines.Add($"Waiting reason: {result.Session.WaitingReason ?? "(none)"}");
            lines.Add($"Stop reason: {result.Session.StopReason ?? "(none)"}");
            lines.Add($"Pending permissions: {(result.Session.PendingPermissionRequestIds.Count == 0 ? "(none)" : string.Join(", ", result.Session.PendingPermissionRequestIds))}");
            lines.Add($"Last permission summary: {result.Session.LastPermissionSummary ?? "(none)"}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult PlannerStatus(RuntimeSessionState? session)
    {
        if (session is null)
        {
            return OperatorCommandResult.Success("No runtime session is attached.");
        }

        return OperatorCommandResult.Success(
            $"Planner state: {PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState)}",
            $"Planner wake reason: {PlannerLifecycleSemantics.DescribeWakeReason(session.PlannerWakeReason)}",
            $"Planner pending wakes: {session.PendingPlannerWakeSignals.Count}",
            $"Planner last consumed wake: {session.LastConsumedPlannerWakeSummary ?? "(none)"}",
            $"Planner sleep reason: {PlannerLifecycleSemantics.DescribeSleepReason(session.PlannerSleepReason)}",
            $"Planner escalation reason: {PlannerLifecycleSemantics.DescribeEscalationReason(session.PlannerEscalationReason)}",
            $"Planner lease: {session.PlannerLeaseId ?? "(none)"}",
            $"Planner lease active: {session.PlannerLeaseActive}",
            $"Planner lease mode: {session.PlannerLeaseMode}",
            $"Planner lease owner: {session.PlannerLeaseOwner ?? "(none)"}",
            $"Planner adapter: {session.PlannerAdapterId ?? "(none)"}",
            $"Planner proposal: {session.PlannerProposalId ?? "(none)"}",
            $"Planner lifecycle reason: {session.PlannerLifecycleReason ?? "(none)"}",
            $"Planner round: {session.PlannerRound}",
            $"Detected opportunities: {session.DetectedOpportunityCount}",
            $"Evaluated opportunities: {session.EvaluatedOpportunityCount}",
            $"Current actionability: {RuntimeActionabilitySemantics.Describe(session.CurrentActionability)}");
    }

    public static OperatorCommandResult PlannerRun(PlannerHostResult result)
    {
        var lines = new List<string>
        {
            $"Planner state: {PlannerLifecycleSemantics.DescribeState(result.Session.PlannerLifecycleState)}",
            $"Planner wake reason: {PlannerLifecycleSemantics.DescribeWakeReason(result.Session.PlannerWakeReason)}",
            $"Planner pending wakes: {result.Session.PendingPlannerWakeSignals.Count}",
            $"Planner lease active: {result.Session.PlannerLeaseActive}",
            $"Planner lease mode: {result.Session.PlannerLeaseMode}",
            $"Planner lifecycle reason: {result.Session.PlannerLifecycleReason ?? "(none)"}",
            $"Planner re-entry outcome: {result.Reentry.Outcome}",
            $"Planner re-entry reason: {result.Reentry.Reason}",
            $"Planner round: {result.Reentry.PlannerRound}",
        };

        if (result.Proposal is not null)
        {
            lines.Add($"Planner proposal id: {result.Proposal.ProposalId}");
            lines.Add($"Planner adapter: {result.Proposal.AdapterId}/{result.Proposal.ProviderId}");
            lines.Add($"Planner acceptance: {result.Proposal.AcceptanceStatus}");
            lines.Add($"Planner acceptance reason: {result.Proposal.AcceptanceReason}");
            lines.Add($"Planner accepted tasks: {(result.Proposal.AcceptedTaskIds.Count == 0 ? "(none)" : string.Join(", ", result.Proposal.AcceptedTaskIds))}");
        }

        if (result.Validation is not null)
        {
            lines.Add($"Planner proposal valid: {result.Validation.IsValid}");
            lines.AddRange(result.Validation.Errors.Select(error => $"Validation error: {error}"));
            lines.AddRange(result.Validation.Warnings.Select(warning => $"Validation warning: {warning}"));
        }

        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult PlannerLoop(PlannerHostLoopResult result)
    {
        var lines = new List<string>
        {
            $"Planner loop iterations: {result.IterationsRun}/{result.MaxIterations}",
            $"Planner loop result: {result.Message}",
        };

        lines.AddRange(result.Iterations.Take(5).Select(iteration =>
            $"- {iteration.Reentry.Outcome}: {iteration.Reentry.Reason}"));

        if (result.Session is not null)
        {
            lines.Add($"Planner state: {PlannerLifecycleSemantics.DescribeState(result.Session.PlannerLifecycleState)}");
            lines.Add($"Planner lifecycle reason: {result.Session.PlannerLifecycleReason ?? "(none)"}");
            lines.Add($"Planner pending wakes: {result.Session.PendingPlannerWakeSignals.Count}");
            lines.Add($"Planner lease active: {result.Session.PlannerLeaseActive}");
        }

        return new OperatorCommandResult(0, lines);
    }

    public static OperatorCommandResult PlannerLifecycleChanged(string action, RuntimeSessionState session)
    {
        return OperatorCommandResult.Success(
            $"{action} planner lifecycle.",
            $"Planner state: {PlannerLifecycleSemantics.DescribeState(session.PlannerLifecycleState)}",
            $"Planner wake reason: {PlannerLifecycleSemantics.DescribeWakeReason(session.PlannerWakeReason)}",
            $"Planner pending wakes: {session.PendingPlannerWakeSignals.Count}",
            $"Planner sleep reason: {PlannerLifecycleSemantics.DescribeSleepReason(session.PlannerSleepReason)}",
            $"Planner lease active: {session.PlannerLeaseActive}",
            $"Planner lease mode: {session.PlannerLeaseMode}",
            $"Planner lifecycle reason: {session.PlannerLifecycleReason ?? "(none)"}");
    }
}
