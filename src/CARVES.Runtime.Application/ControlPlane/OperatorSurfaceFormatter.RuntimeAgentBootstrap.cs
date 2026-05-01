using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeAgentBootstrapPacket(RuntimeAgentBootstrapPacketSurface surface)
    {
        var packet = surface.Packet;
        var lines = new List<string>
        {
            "Runtime agent bootstrap packet",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {packet.PolicyVersion}",
            $"Startup mode: {packet.StartupMode}",
            $"Initialization heading: {packet.InitializationHeading}",
            $"Sources heading: {packet.SourcesHeading}",
            $"Repo posture: {packet.RepoPosture.SessionStatus}/{packet.RepoPosture.LoopMode}/{packet.RepoPosture.CurrentActionability}",
            $"Host snapshot: {packet.HostSnapshot.State} ({packet.HostSnapshot.SessionStatus}/{packet.HostSnapshot.HostControlState})",
            $"Posture basis: {packet.PostureBasis}",
            $"Hot path route: {packet.HotPathContext.RecommendedStartupRoute}",
            $"Hot path boundary: {packet.HotPathContext.GovernanceBoundary}",
            $"Hot path summary: {packet.HotPathContext.Summary}",
            "Entry order:",
        };

        foreach (var item in packet.EntryOrder)
        {
            lines.Add($"- {item}");
        }

        lines.Add("Current card memory refs:");
        foreach (var item in packet.CurrentCardMemoryRefs)
        {
            lines.Add($"- {item}");
        }

        lines.Add($"Report fields: {string.Join(" | ", packet.ReportFields)}");
        lines.Add($"Source fields: {string.Join(" | ", packet.SourceFields)}");
        lines.Add($"Host-routed actions: {string.Join(" | ", packet.HostRoutedActions)}");
        lines.Add($"Startup inspect commands: {string.Join(" | ", packet.StartupInspectCommands)}");
        lines.Add($"Warm-resume inspect commands: {string.Join(" | ", packet.WarmResumeInspectCommands)}");
        lines.Add($"Optional deep-read commands: {string.Join(" | ", packet.OptionalDeepReadCommands)}");
        lines.Add($"Not yet proven: {string.Join(" | ", packet.NotYetProven)}");
        lines.Add($"Hot path default inspect commands: {JoinBootstrapItems(packet.HotPathContext.DefaultInspectCommands)}");
        lines.Add($"Hot path task overlay commands: {JoinBootstrapItems(packet.HotPathContext.TaskOverlayCommands)}");
        lines.Add($"Hot path bounded next commands: {JoinBootstrapItems(packet.HotPathContext.BoundedNextCommands)}");
        AppendMarkdownReadPolicy(lines, packet.HotPathContext.MarkdownReadPolicy);
        lines.Add("Full governance read triggers:");
        foreach (var trigger in packet.HotPathContext.FullGovernanceReadTriggers)
        {
            lines.Add($"- {trigger}");
        }

        lines.Add("Active task summaries:");
        if (packet.HotPathContext.ActiveTasks.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var task in packet.HotPathContext.ActiveTasks)
            {
                lines.Add($"- {task.TaskId} [{task.Status}] {task.Title}");
                lines.Add($"  card: {task.CardId}; inspect: {task.InspectCommand}; overlay: {task.OverlayCommand}");
                if (!string.IsNullOrWhiteSpace(task.Summary))
                {
                    lines.Add($"  summary: {task.Summary}");
                }
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RuntimeAgentBootstrapReceipt(RuntimeAgentBootstrapReceiptSurface surface)
    {
        var receipt = surface.Receipt;
        var lines = new List<string>
        {
            "Runtime agent bootstrap receipt",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {receipt.PolicyVersion}",
            $"Repo root: {receipt.RepoRoot}",
            $"Current task: {receipt.CurrentTaskId}",
            $"Current card memory refs: {(receipt.CurrentCardMemoryRefs.Length == 0 ? "N/A" : string.Join(" | ", receipt.CurrentCardMemoryRefs))}",
            $"Repo posture: {receipt.RepoPosture.SessionStatus}/{receipt.RepoPosture.LoopMode}/{receipt.RepoPosture.CurrentActionability}",
            $"Host snapshot: {receipt.HostSnapshot.State} ({receipt.HostSnapshot.SessionStatus}/{receipt.HostSnapshot.HostControlState})",
            $"Posture basis: {receipt.PostureBasis}",
            $"Verified at: {receipt.VerifiedAt:O}",
            $"Comparison status: {receipt.ComparisonStatus}",
            $"Compared receipt path: {receipt.ComparedReceiptPath}",
            $"Resume decision: {receipt.ResumeDecision}",
            $"Resume reason: {receipt.ResumeReason}",
            $"Required receipt fields: {string.Join(" | ", receipt.RequiredReceiptFields)}",
            $"Validation inspect commands: {string.Join(" | ", receipt.ValidationInspectCommands)}",
            $"Warm-resume checks: {string.Join(" | ", receipt.WarmResumeChecks)}",
            $"Invalidation reasons: {string.Join(" | ", receipt.InvalidationReasons)}",
            $"Cold-init triggers: {string.Join(" | ", receipt.ColdInitTriggers)}",
            $"Hot path route: {receipt.HotPathContext.RecommendedStartupRoute}",
            $"Hot path boundary: {receipt.HotPathContext.GovernanceBoundary}",
            $"Hot path bounded next commands: {JoinBootstrapItems(receipt.HotPathContext.BoundedNextCommands)}",
            $"Resume guidance: {receipt.ResumeGuidance.Guidance}",
            $"Resume machine-surface-first: {receipt.ResumeGuidance.MachineSurfaceFirst}",
            $"Resume skip actions: {JoinBootstrapItems(receipt.ResumeGuidance.SkipActions)}",
            $"Resume required actions: {JoinBootstrapItems(receipt.ResumeGuidance.RequiredActions)}",
            $"Working context mode: {receipt.WorkingContext.DefaultEntryMode}",
            $"Working context boundary: {receipt.WorkingContext.GovernanceBoundary}",
            $"Working context summary: {receipt.WorkingContext.Summary}",
            $"Working context safety posture: {receipt.WorkingContext.SafetyPosture}",
            $"Working context backend posture: {receipt.WorkingContext.BackendPosture}",
            $"Working context recommended commands: {JoinBootstrapItems(receipt.WorkingContext.RecommendedNextCommands)}",
            $"Working context unavailable checks: {JoinBootstrapItems(receipt.WorkingContext.UnavailableChecks)}",
        };
        lines.Add("Working context recent executions:");
        if (receipt.WorkingContext.RecentExecutions.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var execution in receipt.WorkingContext.RecentExecutions)
            {
                lines.Add($"- {execution.TaskId}: run={execution.RunId}; backend={execution.Backend}; status={execution.Status}; failure={execution.FailureKind}; retryable={execution.Retryable}");
                lines.Add($"  summary: {execution.Summary}; detail={execution.DetailRef}");
            }
        }

        AppendMarkdownReadPolicy(lines, receipt.HotPathContext.MarkdownReadPolicy);
        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RuntimeAgentQueueProjection(RuntimeAgentQueueProjectionSurface surface)
    {
        var projection = surface.Projection;
        var counts = projection.Counts;
        var pointers = projection.ExpansionPointers;
        var lines = new List<string>
        {
            "Runtime agent queue projection",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Boundary: {projection.TruthBoundary}",
            $"Summary: {projection.Summary}",
            $"Current task: {projection.CurrentTask.TaskId} [{projection.CurrentTask.Status}] {projection.CurrentTask.Title}",
            $"Current task actionability: {projection.CurrentTask.Actionability}",
            $"Current task commands: {projection.CurrentTask.InspectCommand} | {projection.CurrentTask.OverlayCommand}",
            $"Counts: total={counts.TotalCount}; pending={counts.PendingCount}; running={counts.RunningCount}; review={counts.ReviewCount}; blocked={counts.BlockedCount}; deferred={counts.DeferredCount}; completed={counts.CompletedCount}",
            $"Additional counts: testing={counts.TestingCount}; approval_wait={counts.ApprovalWaitCount}; failed={counts.FailedCount}; suggested={counts.SuggestedCount}; merged={counts.MergedCount}; discarded={counts.DiscardedCount}; superseded={counts.SupersededCount}",
            $"Expansion pointers: queue={pointers.FullQueuePath}; graph={pointers.FullGraphPath}; current={pointers.CurrentTaskPath}; mode={pointers.ReadMode}",
            $"Truth note: {pointers.CanonicalTruthNote}",
            "First actionable tasks:",
        };

        if (projection.FirstActionableTasks.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var task in projection.FirstActionableTasks)
            {
                lines.Add($"- {task.TaskId} [{task.Priority}/{task.Status}] {task.Title}; card={task.CardId}; run={task.RunCommand}; overlay={task.OverlayCommand}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RuntimeAgentTaskOverlay(RuntimeAgentTaskBootstrapOverlaySurface surface)
    {
        var overlay = surface.Overlay;
        var lines = new List<string>
        {
            "Runtime agent task overlay",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Task: {overlay.TaskId}",
            $"Card: {overlay.CardId}",
            $"Title: {overlay.Title}",
            $"Status: {overlay.TaskStatus}",
            $"Scope files: {(overlay.ScopeFiles.Length == 0 ? "N/A" : string.Join(" | ", overlay.ScopeFiles))}",
            $"Acceptance: {JoinBootstrapItems(overlay.Acceptance)}",
            $"Constraints: {JoinBootstrapItems(overlay.Constraints)}",
            $"Acceptance contract: {overlay.AcceptanceContract.BindingState}; id={overlay.AcceptanceContract.ContractId}; status={overlay.AcceptanceContract.Status}; human_review={overlay.AcceptanceContract.HumanReviewRequired}; provisional={overlay.AcceptanceContract.ProvisionalAllowed}",
            $"Acceptance contract goal: {overlay.AcceptanceContract.Goal}",
            $"Acceptance contract evidence: {JoinBootstrapItems(overlay.AcceptanceContract.EvidenceRequired)}",
            $"Acceptance contract must-not: {JoinBootstrapItems(overlay.AcceptanceContract.MustNot)}",
            $"Editable roots: {JoinBootstrapItems(overlay.EditableRoots)}",
            $"Read-only roots: {JoinBootstrapItems(overlay.ReadOnlyRoots)}",
            $"Truth roots: {JoinBootstrapItems(overlay.TruthRoots)}",
            $"Repo mirror roots: {JoinBootstrapItems(overlay.RepoMirrorRoots)}",
            $"Protected roots: {(overlay.ProtectedRoots.Length == 0 ? "N/A" : string.Join(" | ", overlay.ProtectedRoots))}",
            $"Safety context: {overlay.SafetyContext.Summary}",
            $"Safety layers: {overlay.SafetyContext.LayerSummary}",
            $"Safety budgets: files={overlay.SafetyContext.MaxFilesChanged}; lines={overlay.SafetyContext.MaxLinesChanged}; shell={overlay.SafetyContext.MaxShellCommands}",
            $"Safety non-claims: {JoinBootstrapItems(overlay.SafetyContext.NonClaims)}",
            $"Planner-only actions: {JoinBootstrapItems(overlay.SafetyContext.PlannerOnlyActions)}",
            $"Allowed actions: {(overlay.AllowedActions.Length == 0 ? "N/A" : string.Join(" | ", overlay.AllowedActions))}",
            $"Required verification: {(overlay.RequiredVerification.Length == 0 ? "N/A" : string.Join(" | ", overlay.RequiredVerification))}",
            $"Stable evidence surfaces: {(overlay.StableEvidenceSurfaces.Length == 0 ? "N/A" : string.Join(" | ", overlay.StableEvidenceSurfaces))}",
            $"Stop conditions: {(overlay.StopConditions.Length == 0 ? "N/A" : string.Join(" | ", overlay.StopConditions))}",
            $"Memory bundle refs: {(overlay.MemoryBundleRefs.Length == 0 ? "N/A" : string.Join(" | ", overlay.MemoryBundleRefs))}",
            $"Validation commands: {JoinBootstrapItems(overlay.ValidationContext.Commands)}",
            $"Validation checks: {JoinBootstrapItems(overlay.ValidationContext.Checks)}",
            $"Expected evidence: {JoinBootstrapItems(overlay.ValidationContext.ExpectedEvidence)}",
            $"Planner review: {overlay.PlannerReview.Verdict}/{overlay.PlannerReview.DecisionStatus}; acceptance_met={overlay.PlannerReview.AcceptanceMet}; boundary_preserved={overlay.PlannerReview.BoundaryPreserved}; scope_drift={overlay.PlannerReview.ScopeDriftDetected}",
            $"Planner reason: {(string.IsNullOrWhiteSpace(overlay.PlannerReview.Reason) ? "N/A" : overlay.PlannerReview.Reason)}",
            $"Planner follow-ups: {JoinBootstrapItems(overlay.PlannerReview.FollowUpSuggestions)}",
            $"Last worker: run={overlay.LastWorker.RunId}; backend={overlay.LastWorker.Backend}; failure={overlay.LastWorker.FailureKind}; retryable={overlay.LastWorker.Retryable}",
            $"Last worker summary: {overlay.LastWorker.Summary}",
            $"Last worker refs: worker={overlay.LastWorker.DetailRef}; provider={overlay.LastWorker.ProviderDetailRef}",
            $"Recovery: {overlay.LastWorker.RecoveryAction}; reason={overlay.LastWorker.RecoveryReason}",
            $"Markdown read mode: {overlay.MarkdownReadGuidance.DefaultReadMode}",
            $"Markdown read boundary: {overlay.MarkdownReadGuidance.GovernanceBoundary}",
            $"Markdown read summary: {overlay.MarkdownReadGuidance.Summary}",
            $"Task-scoped Markdown refs: {JoinBootstrapItems(overlay.MarkdownReadGuidance.TaskScopedMarkdownRefs)}",
            $"Required before edit refs: {JoinBootstrapItems(overlay.MarkdownReadGuidance.RequiredBeforeEditRefs)}",
            $"Markdown replacement surfaces: {JoinBootstrapItems(overlay.MarkdownReadGuidance.ReplacementSurfaces)}",
            $"Markdown escalation triggers: {JoinBootstrapItems(overlay.MarkdownReadGuidance.EscalationTriggers)}",
        };
        lines.Add("Safety layer semantics:");
        if (overlay.SafetyContext.Layers.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var layer in overlay.SafetyContext.Layers)
            {
                lines.Add($"- {layer.LayerId}: phase={layer.Phase}; authority={layer.Authority}; timing={layer.EnforcementTiming}");
                lines.Add($"  enforces: {JoinBootstrapItems(layer.Enforces)}");
                lines.Add($"  non_claims: {JoinBootstrapItems(layer.NonClaims)}");
            }
        }

        lines.Add("Scope file contexts:");
        if (overlay.ScopeFileContexts.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var item in overlay.ScopeFileContexts)
            {
                lines.Add($"- {item.Path}: exists={item.Exists}; git={item.GitStatus}; boundary={item.BoundaryClass}; source={item.Source}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    private static void AppendMarkdownReadPolicy(List<string> lines, AgentMarkdownReadPolicy policy)
    {
        lines.Add($"Markdown read policy: {policy.PolicyId}");
        lines.Add($"Markdown read summary: {policy.Summary}");
        lines.Add($"Markdown post-init mode: {policy.DefaultPostInitializationMode}");
        lines.Add($"Markdown warm-resume mode: {policy.WarmResumeMode}");
        lines.Add($"Markdown read boundary: {policy.GovernanceBoundary}");
        lines.Add($"Markdown required initial sources: {JoinBootstrapItems(policy.RequiredInitialSources)}");
        lines.Add($"Markdown never-replaced sources: {JoinBootstrapItems(policy.NeverReplacedSources)}");
        lines.Add($"Markdown hot-path surfaces: {JoinBootstrapItems(policy.PostInitializationHotPathSurfaces)}");
        lines.Add($"Markdown deferred after init: {JoinBootstrapItems(policy.DeferredAfterInitializationSources)}");
        lines.Add($"Markdown escalation triggers: {JoinBootstrapItems(policy.EscalationTriggers)}");
        lines.Add("Markdown read tiers:");
        foreach (var tier in policy.ReadTiers)
        {
            lines.Add($"- {tier.TierId}: action={tier.DefaultAction}; when={tier.ReadWhen}");
            lines.Add($"  sources: {JoinBootstrapItems(tier.Sources)}");
            lines.Add($"  surfaces: {JoinBootstrapItems(tier.PreferredSurfaces)}");
            lines.Add($"  notes: {JoinBootstrapItems(tier.Notes)}");
        }
    }

    private static string JoinBootstrapItems(IReadOnlyList<string> items)
    {
        return items.Count == 0 ? "N/A" : string.Join(" | ", items);
    }

    public static OperatorCommandResult RuntimeAgentModelProfileRouting(RuntimeAgentModelProfileRoutingSurface surface)
    {
        var routing = surface.Routing;
        var lines = new List<string>
        {
            "Runtime agent model-profile routing",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Summary: {routing.Summary}",
            "Profiles:",
        };

        foreach (var profile in routing.Profiles.OrderBy(item => item.ProfileId, StringComparer.Ordinal))
        {
            lines.Add($"- {profile.ProfileId}: ceiling={profile.GovernanceCeiling}; startup_sources={profile.MaxStartupSources}; deep_governance_ready={profile.DeepGovernanceReady}");
        }

        lines.Add("Available lanes:");
        if (routing.AvailableLanes.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var lane in routing.AvailableLanes)
            {
                lines.Add($"- {lane.LaneId} [{lane.ProviderId}/{lane.BackendId}/{lane.Model}] -> {lane.MatchedProfileId}");
                lines.Add($"  reason: {lane.Reason}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RuntimeAgentLoopStallGuard(RuntimeAgentLoopStallGuardSurface surface)
    {
        var guard = surface.Guard;
        var lines = new List<string>
        {
            "Runtime agent loop-stall guard",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Task: {guard.TaskId}",
            $"Detector window: {guard.DetectorWindow}",
            $"Detection lineage: {guard.DetectionLineage}",
            $"Pattern: {guard.Pattern.Type}",
            $"Severity: {guard.Pattern.Severity}",
            $"Suggestion: {guard.Pattern.Suggestion}",
            $"Summary: {guard.Pattern.Summary}",
            $"Runs analyzed: {guard.Pattern.RunsAnalyzed}",
            "Profile outcomes:",
        };

        foreach (var outcome in guard.ProfileOutcomes)
        {
            lines.Add($"- {outcome.ProfileId} -> {outcome.ForcedAction}");
            lines.Add($"  reason: {outcome.Reason}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }

    public static OperatorCommandResult RuntimeWeakModelExecutionLane(RuntimeWeakModelExecutionLaneSurface surface)
    {
        var snapshot = surface.LaneSnapshot;
        var lines = new List<string>
        {
            "Runtime weak-model execution lane",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Summary: {snapshot.Summary}",
            "Weak lanes:",
        };

        foreach (var lane in snapshot.Lanes.OrderBy(item => item.LaneId, StringComparer.Ordinal))
        {
            lines.Add($"- {lane.LaneId}: profile={lane.ModelProfileId}; task_types={string.Join(" | ", lane.AllowedTaskTypes)}");
            lines.Add($"  scope ceiling: entries={lane.ScopeCeiling.MaxScopeEntries}; relevant_files={lane.ScopeCeiling.MaxRelevantFiles}; files_changed={lane.ScopeCeiling.MaxFilesChanged}; lines_changed={lane.ScopeCeiling.MaxLinesChanged}");
            lines.Add($"  required verification: {string.Join(" | ", lane.RequiredVerification)}");
            lines.Add($"  stop conditions: {string.Join(" | ", lane.StopConditions)}");
        }

        lines.Add("Matched qualified lanes:");
        if (snapshot.MatchedQualifiedLanes.Length == 0)
        {
            lines.Add("- (none)");
        }
        else
        {
            foreach (var lane in snapshot.MatchedQualifiedLanes)
            {
                lines.Add($"- {lane.LaneId} [{lane.ProviderId}/{lane.BackendId}/{lane.Model}]");
                lines.Add($"  reason: {lane.Reason}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
