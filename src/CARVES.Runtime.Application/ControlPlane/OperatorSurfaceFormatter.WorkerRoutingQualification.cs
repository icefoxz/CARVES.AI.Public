using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    private static OperatorCommandResult WorkerNodesCore(IReadOnlyList<WorkerNode> nodes)
    {
        var lines = new List<string> { "Worker nodes:" };
        lines.AddRange(nodes.Count == 0
            ? ["(none)"]
            : nodes.Select(node => $"- {node.NodeId}: {node.Status} [leases={node.ActiveLeaseCount}/{node.Capabilities.MaxConcurrentTasks}; heartbeat={node.LastHeartbeatAt:O}; reason={node.LastReason ?? "(none)"}]"));
        return new OperatorCommandResult(0, lines);
    }

    private static OperatorCommandResult WorkerProvidersCore(IReadOnlyList<WorkerBackendDescriptor> backends)
    {
        var lines = new List<string> { "Worker providers:" };
        lines.AddRange(backends.Count == 0
            ? ["(none)"]
            : backends.Select(backend =>
                $"- {backend.BackendId}: provider={backend.ProviderId}; routing={backend.RoutingIdentity}; protocol={backend.ProtocolFamily}/{backend.RequestFamily}; health={backend.Health.State}; trust={string.Join(", ", backend.CompatibleTrustProfiles)}; caps=exec:{backend.Capabilities.SupportsExecution},json:{backend.Capabilities.SupportsJsonMode},system:{backend.Capabilities.SupportsSystemPrompt},tools:{backend.Capabilities.SupportsToolCalls},stream:{backend.Capabilities.SupportsStreaming}"));
        return new OperatorCommandResult(0, lines);
    }

    private static OperatorCommandResult WorkerProfilesCore(IReadOnlyList<Domain.Execution.WorkerExecutionProfile> profiles, string? repoId)
    {
        var lines = new List<string> { $"Worker trust profiles: {(string.IsNullOrWhiteSpace(repoId) ? "(default)" : repoId)}" };
        lines.AddRange(profiles.Count == 0
            ? ["(none)"]
            : profiles.Select(profile =>
                $"- {profile.ProfileId}: trusted={profile.Trusted}; sandbox={profile.SandboxMode}; approval={profile.ApprovalMode}; boundary={profile.WorkspaceBoundary}; scope={profile.FilesystemScope}; network={profile.NetworkAccessEnabled}"));
        return new OperatorCommandResult(0, lines);
    }

    private static OperatorCommandResult WorkerOperationalSummaryCore(OperationalSummary summary)
    {
        var lines = new List<string>
        {
            $"Operational summary stage: {summary.Stage}",
            $"Session status/actionability: {summary.SessionStatus}/{summary.SessionActionability}",
            $"Operator actionability: {summary.Actionability} ({summary.ActionabilityReason})",
            $"Operator summary: {summary.ActionabilitySummary}",
            $"Pending approvals: {summary.PendingApprovalCount}",
            $"Blocked tasks: {summary.BlockedTaskCount}",
            $"Review tasks: {summary.ReviewTaskCount}",
            $"Unhealthy providers: actionable={summary.ProviderHealthIssueCount}; optional={summary.OptionalProviderHealthIssueCount}; disabled={summary.DisabledProviderCount}",
            $"Recent incidents: {summary.RecentIncidentCount}",
            $"Pending rebuilds: {summary.PendingRebuildCount}",
            $"Recommended next action: {summary.RecommendedNextAction}",
            $"Notes: {(summary.Notes.Count == 0 ? "(none)" : string.Join(" | ", summary.Notes))}",
            "Blocked queue:",
        };
        lines.AddRange(summary.BlockedQueue.Count == 0
            ? ["(none)"]
            : summary.BlockedQueue.Select(item => $"- {item.TaskId}: {item.Status}; category={item.Category}; reason={item.Reason}; next={item.RecommendedNextAction}"));
        lines.Add("Pending approvals:");
        lines.AddRange(summary.ApprovalQueue.Count == 0
            ? ["(none)"]
            : summary.ApprovalQueue.Select(item => $"- {item.ItemId}: task={item.TaskId}; category={item.Category}; reason={item.Reason}; next={item.RecommendedNextAction}"));
        lines.Add("Provider health preview:");
        lines.AddRange(summary.Providers.Count == 0
            ? ["(none)"]
            : summary.Providers.Select(item => $"- {item.BackendId}: {item.State}; role={item.SelectionRole}; impact={item.ActionabilityImpact}; actionable={item.ActionabilityRelevant}; failures={item.ConsecutiveFailureCount}; latency={item.LatencyMs?.ToString() ?? "(none)"}ms; {item.Summary}; next={item.RecommendedNextAction}"));
        lines.Add("Incident drill-down:");
        lines.AddRange(summary.Incidents.Count == 0
            ? ["(none)"]
            : summary.Incidents.Select(item => $"- {item.IncidentType}: task={item.TaskId ?? "(none)"} backend={item.BackendId ?? "(none)"} action={item.RecoveryAction}; {item.Summary}"));
        lines.Add("Recovery outcomes:");
        lines.AddRange(summary.RecoveryOutcomes.Count == 0
            ? ["(none)"]
            : summary.RecoveryOutcomes.Select(item => $"- task={item.TaskId}; action={item.RecoveryAction}; outcome={item.Outcome}; {item.Summary}"));
        return new OperatorCommandResult(0, lines);
    }

    private static OperatorCommandResult WorkerSelectionCore(Domain.Execution.WorkerSelectionDecision decision)
    {
        var lines = new List<string>
        {
            $"Repo: {decision.RepoId}",
            $"Task: {decision.TaskId ?? "(none)"}",
            $"Allowed: {decision.Allowed}",
            $"Selected backend: {decision.SelectedBackendId ?? "(none)"}",
            $"Selected provider: {decision.SelectedProviderId ?? "(none)"}",
            $"Selected adapter: {decision.SelectedAdapterId ?? "(none)"}",
            $"Selected model: {decision.SelectedModelId ?? "(none)"}",
            $"Active routing profile: {decision.ActiveRoutingProfileId ?? "(none)"}",
            $"Routing profile: {decision.SelectedRoutingProfileId ?? "(none)"}",
            $"Routing rule: {decision.AppliedRoutingRuleId ?? "(none)"}",
            $"Routing intent: {decision.RoutingIntent ?? "(none)"}",
            $"Routing module: {decision.RoutingModuleId ?? "(none)"}",
            $"Route source: {decision.RouteSource}",
            $"Route reason: {decision.RouteReason}",
            $"Preferred route eligibility: {decision.PreferredRouteEligibility?.ToString() ?? "(none)"}",
            $"Preferred ineligibility reason: {decision.PreferredIneligibilityReason ?? "(none)"}",
            $"Trust profile: {decision.RequestedTrustProfileId}",
            $"Used fallback: {decision.UsedFallback}",
            $"Reason code: {decision.ReasonCode}",
            $"Summary: {decision.Summary}",
        };
        lines.Add("Selected because:");
        lines.AddRange(decision.SelectedBecause.Count == 0
            ? ["(none)"]
            : decision.SelectedBecause.Select(reason => $"- {reason}"));
        lines.Add("Candidates:");
        lines.AddRange(decision.Candidates.Count == 0
            ? ["(none)"]
            : decision.Candidates.Select(candidate =>
                $"- {candidate.BackendId}: selected={candidate.Selected}; provider={candidate.ProviderId}; routing={candidate.RoutingProfileId ?? "(none)"}; rule={candidate.RoutingRuleId ?? "(none)"}; route={candidate.RouteDisposition}; eligibility={candidate.Eligibility}; quota={candidate.Signals.QuotaState}; token_fit={candidate.Signals.TokenBudgetFit}; latency_ms={candidate.Signals.RecentLatencyMs?.ToString() ?? "(none)"}; failures={candidate.Signals.RecentFailureCount}; health={candidate.HealthState}; profile_ok={candidate.ProfileCompatible}; capabilities_ok={candidate.CapabilityCompatible}; {candidate.Reason}"));
        return new OperatorCommandResult(decision.Allowed ? 0 : 1, lines);
    }

    private static OperatorCommandResult CodexRoutingEligibilityCore(
        IReadOnlyList<WorkerBackendDescriptor> codexBackends,
        Domain.Execution.WorkerSelectionDecision selection)
    {
        var lines = new List<string> { "Codex worker routing eligibility:" };
        if (codexBackends.Count == 0)
        {
            lines.Add("- codex_sdk: (not registered)");
            lines.Add("- codex_cli: (not registered)");
        }
        else
        {
            foreach (var backend in codexBackends.OrderBy(b => b.BackendId, StringComparer.Ordinal))
            {
                var candidate = selection.Candidates.FirstOrDefault(c =>
                    string.Equals(c.BackendId, backend.BackendId, StringComparison.OrdinalIgnoreCase));
                var eligibility = candidate?.Eligibility.ToString() ?? "unknown";
                var reason = candidate?.Reason ?? "no candidate evaluation available";
                var health = backend.Health.State.ToString().ToLowerInvariant();
                var configured = backend.Health.State != WorkerBackendHealthState.Unavailable
                    && backend.Health.State != WorkerBackendHealthState.Disabled;
                lines.Add($"- {backend.BackendId}:");
                lines.Add($"  display_name={backend.DisplayName}");
                lines.Add($"  provider={backend.ProviderId}");
                lines.Add($"  protocol={backend.ProtocolFamily}/{backend.RequestFamily}");
                lines.Add($"  health={health}");
                lines.Add($"  configured={configured}");
                lines.Add($"  routing_profiles={string.Join(", ", backend.RoutingProfiles)}");
                lines.Add($"  trust_profiles={string.Join(", ", backend.CompatibleTrustProfiles)}");
                lines.Add($"  eligibility={eligibility}");
                lines.Add($"  reason={reason}");
                lines.Add($"  health_summary={backend.Health.Summary}");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Codex candidates from last selection:");
        var codexCandidates = selection.Candidates
            .Where(c => string.Equals(c.ProviderId, "codex", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.BackendId, StringComparer.Ordinal)
            .ToArray();
        lines.AddRange(codexCandidates.Length == 0
            ? ["- (none — no codex candidates evaluated in last selection)"]
            : codexCandidates.Select(c =>
                $"- {c.BackendId}: selected={c.Selected}; route={c.RouteDisposition}; eligibility={c.Eligibility}; quota={c.Signals.QuotaState}; token_fit={c.Signals.TokenBudgetFit}; health={c.HealthState}; profile_ok={c.ProfileCompatible}; caps_ok={c.CapabilityCompatible}; {c.Reason}"));

        lines.Add(string.Empty);
        lines.Add($"Active routing profile: {selection.ActiveRoutingProfileId ?? "(none)"}");
        lines.Add($"Applied routing rule: {selection.AppliedRoutingRuleId ?? "(none)"}");
        lines.Add($"Route source: {selection.RouteSource}");
        lines.Add($"Preferred route eligibility: {selection.PreferredRouteEligibility?.ToString() ?? "(none)"}");
        lines.Add($"Preferred ineligibility reason: {selection.PreferredIneligibilityReason ?? "(none)"}");
        return OperatorCommandResult.Success(lines.ToArray());
    }

    private static OperatorCommandResult WorkerNodeChangedCore(string action, WorkerNode node)
    {
        return OperatorCommandResult.Success(
            $"{action} worker node {node.NodeId}.",
            $"Status: {node.Status}",
            $"Active leases: {node.ActiveLeaseCount}",
            $"Last heartbeat: {node.LastHeartbeatAt:O}",
            $"Last reason: {node.LastReason ?? "(none)"}");
    }

    private static OperatorCommandResult WorkerLeasesCore(IReadOnlyList<WorkerLeaseRecord> leases)
    {
        var lines = new List<string> { "Worker leases:" };
        lines.AddRange(leases.Count == 0
            ? ["(none)"]
            : leases.Select(lease => $"- {lease.LeaseId}: {lease.Status} [task={lease.TaskId}; node={lease.NodeId}; expires={lease.ExpiresAt:O}; on_expiry={lease.OnExpiry}]"));
        return new OperatorCommandResult(0, lines);
    }

    private static OperatorCommandResult WorkerLeaseExpiredCore(WorkerLeaseRecord lease)
    {
        return OperatorCommandResult.Success(
            $"Expired worker lease {lease.LeaseId}.",
            $"Status: {lease.Status}",
            $"Task: {lease.TaskId}",
            $"Node: {lease.NodeId}",
            $"Repo path: {lease.RepoPath}",
            $"Completion reason: {lease.CompletionReason ?? "(none)"}");
    }
}
