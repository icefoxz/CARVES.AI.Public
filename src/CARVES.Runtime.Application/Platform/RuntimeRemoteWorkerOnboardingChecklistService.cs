using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class RuntimeRemoteWorkerOnboardingChecklistService
{
    private readonly ProviderRegistryService providerRegistryService;
    private readonly RuntimeRemoteWorkerQualificationService qualificationService;

    public RuntimeRemoteWorkerOnboardingChecklistService(
        ProviderRegistryService providerRegistryService,
        RuntimeRemoteWorkerQualificationService? qualificationService = null)
    {
        this.providerRegistryService = providerRegistryService;
        this.qualificationService = qualificationService ?? new RuntimeRemoteWorkerQualificationService();
    }

    public RuntimeRemoteWorkerOnboardingChecklistSurface BuildSurface()
    {
        var backends = providerRegistryService.ListWorkerBackends()
            .ToDictionary(item => item.BackendId, item => item, StringComparer.OrdinalIgnoreCase);
        var policies = qualificationService.BuildSurface().CurrentPolicies;
        return new RuntimeRemoteWorkerOnboardingChecklistSurface
        {
            Summary = "Remote worker onboarding is a governed checklist. Current worker activation is external app/CLI only: Codex App schedule/evidence callbacks and Codex CLI Host-routed execution are the current proven paths, while other external app/CLI adapters require separate governed onboarding.",
            CurrentActivationMode = "external_app_cli_only",
            AllowedExternalWorkerPaths =
            [
                "codex_app_schedule_evidence_callback",
                "codex_cli_host_routed_execution",
                "external_agent_app_cli_adapter_governed_onboarding"
            ],
            DisallowedWorkerBackendIds =
            [
                "codex_sdk",
                "openai_api",
                "claude_api",
                "gemini_api"
            ],
            SdkApiWorkerBackendsAllowed = false,
            SdkApiWorkerBoundary = "closed_until_separate_governed_activation",
            Providers = policies
                .Select(policy => BuildProviderEntry(policy, backends))
                .OrderBy(item => item.ProviderId, StringComparer.Ordinal)
                .ToArray(),
            Notes =
            [
                "This checklist is a governance projection over existing runtime truth, not a provider-owned source of truth.",
                "SDK/API worker backends remain closed for current worker selection; provider qualification here is future/onboarding evidence only.",
                "Claude is not blocked by provider name, but claude_api remains closed; a Claude external app/CLI adapter would need separate governed onboarding before selection.",
                "The checklist stays local: registry rollout, automatic promotion, and multi-runtime distribution remain out of scope.",
                "Checkpoint evidence must still be recorded separately before a provider is promoted beyond bounded lanes."
            ],
        };
    }

    private static RuntimeRemoteWorkerOnboardingProviderEntry BuildProviderEntry(
        RuntimeRemoteWorkerQualificationPolicy policy,
        IReadOnlyDictionary<string, WorkerBackendDescriptor> backends)
    {
        backends.TryGetValue(policy.BackendId, out var backend);
        var protocolReady = backend is not null;
        var routingReady = backend?.RoutingProfiles.Contains(policy.RoutingProfileId, StringComparer.Ordinal) == true;
        var auditReady = backend is not null && backend.Capabilities.SupportsExecution;
        return new RuntimeRemoteWorkerOnboardingProviderEntry
        {
            ProviderId = policy.ProviderId,
            BackendId = policy.BackendId,
            RoutingProfileId = policy.RoutingProfileId,
            ProtocolFamily = policy.ProtocolFamily,
            QualificationPolicyId = policy.PolicyId,
            ActivationState = IsSdkOrApiWorkerBackend(policy.BackendId)
                ? "closed_current_policy_future_external_adapter_onboarding_only"
                : "available_if_explicitly_activated",
            CurrentWorkerSelectionEligible = !IsSdkOrApiWorkerBackend(policy.BackendId),
            Checklist =
            [
                new RuntimeRemoteWorkerOnboardingChecklistItem
                {
                    ItemId = "protocol_adapter",
                    Title = "Protocol adapter is registered",
                    State = protocolReady ? "ready" : "missing",
                    Summary = protocolReady
                        ? $"Backend '{policy.BackendId}' is registered as a real runtime worker adapter."
                        : $"Backend '{policy.BackendId}' is not registered in the provider registry.",
                    EvidenceReferences =
                    [
                        "inspect worker-providers",
                        "api worker-providers"
                    ],
                },
                new RuntimeRemoteWorkerOnboardingChecklistItem
                {
                    ItemId = "bounded_routing_profile",
                    Title = "Bounded routing profile is present",
                    State = routingReady ? "ready" : "missing",
                    Summary = routingReady
                        ? $"Routing profile '{policy.RoutingProfileId}' is bound to backend '{policy.BackendId}'."
                        : $"Routing profile '{policy.RoutingProfileId}' is not bound to backend '{policy.BackendId}'.",
                    EvidenceReferences =
                    [
                        "inspect worker-profiles",
                        "api worker-profiles"
                    ],
                },
                new RuntimeRemoteWorkerOnboardingChecklistItem
                {
                    ItemId = "qualification_policy",
                    Title = "Bounded qualification policy exists",
                    State = policy.Lanes.Length > 0 ? "ready" : "missing",
                    Summary = $"Qualification policy '{policy.PolicyId}' defines {policy.Lanes.Length} bounded lanes for {policy.ProviderId}.",
                    EvidenceReferences =
                    [
                        "inspect runtime-remote-worker-qualification",
                        "api runtime-remote-worker-qualification"
                    ],
                },
                new RuntimeRemoteWorkerOnboardingChecklistItem
                {
                    ItemId = "audit_artifact_support",
                    Title = "Provider artifact path is auditable",
                    State = auditReady ? "ready" : "missing",
                    Summary = auditReady
                        ? $"Remote worker backend '{policy.BackendId}' executes through the auditable provider artifact path."
                        : $"Remote worker backend '{policy.BackendId}' does not currently expose auditable execution support.",
                    EvidenceReferences =
                    [
                        ".ai/artifacts/provider/<task-id>.json",
                        "explain-task <task-id>"
                    ],
                },
                new RuntimeRemoteWorkerOnboardingChecklistItem
                {
                    ItemId = "checkpoint_proof",
                    Title = "Checkpoint proof is required before promotion",
                    State = "documented",
                    Summary = $"Targeted checkpoint proof remains mandatory before {policy.ProviderId} is promoted beyond bounded lanes.",
                    EvidenceReferences = ResolveCheckpointReferences(policy.ProviderId),
                }
            ],
            Notes =
            [
                $"Provider/backend: {policy.ProviderId}/{policy.BackendId}",
                IsSdkOrApiWorkerBackend(policy.BackendId)
                    ? "Current worker activation excludes this SDK/API backend; keep this entry as future onboarding evidence only and route external app/CLI support through a separate adapter."
                    : "Current worker activation may use this backend only through an explicit governed activation.",
                "Checklist items stay reusable for later provider onboarding instead of re-deriving the same control points."
            ],
        };
    }

    private static bool IsSdkOrApiWorkerBackend(string backendId)
    {
        return backendId.EndsWith("_api", StringComparison.Ordinal)
               || backendId.EndsWith("_sdk", StringComparison.Ordinal)
               || string.Equals(backendId, "codex_sdk", StringComparison.Ordinal);
    }

    private static string[] ResolveCheckpointReferences(string providerId)
    {
        return providerId switch
        {
            "claude" =>
            [
                "docs/runtime/POST_346_CHECKPOINT.md",
                "inspect runtime-claude-worker-qualification"
            ],
            "gemini" =>
            [
                "docs/runtime/POST_350_CHECKPOINT.md",
                "inspect runtime-remote-worker-qualification"
            ],
            _ =>
            [
                "docs/runtime/",
                "inspect runtime-remote-worker-qualification"
            ],
        };
    }
}
