using Carves.Runtime.Domain.AI;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.AI;

public sealed class RuntimeTokenWorkerWrapperCanaryService
{
    internal const string TargetSurface = "worker:system:$.instructions";
    internal const string RequestKind = "worker";
    internal const string ApprovalScope = "limited_explicit_allowlist";
    internal const string ApprovedCandidateVersion = "wrapper_candidate_20260421_worker_system___instructions";
    internal const string FallbackVersion = "original_worker_system_instructions";

    internal const string MainPathDefaultEnabledEnvironmentVariable = "CARVES_RUNTIME_TOKEN_WORKER_WRAPPER_MAIN_PATH_DEFAULT_ENABLED";
    internal const string CanaryEnabledEnvironmentVariable = "CARVES_RUNTIME_TOKEN_WORKER_WRAPPER_CANARY_ENABLED";
    internal const string GlobalKillSwitchEnvironmentVariable = "CARVES_RUNTIME_TOKEN_WORKER_WRAPPER_CANARY_KILL_SWITCH";
    internal const string RequestKindAllowlistEnvironmentVariable = "CARVES_RUNTIME_TOKEN_WORKER_WRAPPER_CANARY_REQUEST_KIND_ALLOWLIST";
    internal const string SurfaceAllowlistEnvironmentVariable = "CARVES_RUNTIME_TOKEN_WORKER_WRAPPER_CANARY_SURFACE_ALLOWLIST";
    internal const string CandidateVersionEnvironmentVariable = "CARVES_RUNTIME_TOKEN_WORKER_WRAPPER_CANARY_CANDIDATE_VERSION";

    private readonly Func<string, string?> getEnvironmentVariable;

    public RuntimeTokenWorkerWrapperCanaryService()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal RuntimeTokenWorkerWrapperCanaryService(Func<string, string?> getEnvironmentVariable)
    {
        this.getEnvironmentVariable = getEnvironmentVariable;
    }

    public RuntimeTokenWorkerWrapperCanaryDecision ResolveWorkerSystemInstructions(
        TaskNode task,
        ExecutionPacket packet,
        WorkerRequestBudget requestBudget,
        string originalInstructions)
    {
        var mainPathDefaultEnabled = ParseBoolean(MainPathDefaultEnabledEnvironmentVariable);
        var canaryEnabled = ParseBoolean(CanaryEnabledEnvironmentVariable);
        var killSwitchActive = ParseBoolean(GlobalKillSwitchEnvironmentVariable);
        var requestKindAllowlisted = IsAllowlisted(RequestKindAllowlistEnvironmentVariable, RequestKind);
        var surfaceAllowlisted = IsAllowlisted(SurfaceAllowlistEnvironmentVariable, TargetSurface);
        var candidateVersion = Normalize(getEnvironmentVariable(CandidateVersionEnvironmentVariable));
        var candidateVersionPinned = string.Equals(candidateVersion, ApprovedCandidateVersion, StringComparison.Ordinal);

        if (killSwitchActive)
        {
            return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "kill_switch_active");
        }

        if (mainPathDefaultEnabled)
        {
            if (!requestKindAllowlisted)
            {
                return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "request_kind_not_allowlisted");
            }

            if (!surfaceAllowlisted)
            {
                return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "surface_not_allowlisted");
            }

            if (!candidateVersionPinned)
            {
                return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "candidate_version_not_pinned");
            }

            var candidateInstructions = RuntimeTokenWorkerWrapperCandidateRenderer.RenderWorkerSystemInstructions(task, packet, requestBudget);
            return BuildDecision(candidateInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, true, "limited_main_path_default", candidateVersion, "main_path_default");
        }

        if (!canaryEnabled)
        {
            return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "default_off");
        }

        if (!requestKindAllowlisted)
        {
            return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "request_kind_not_allowlisted");
        }

        if (!surfaceAllowlisted)
        {
            return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "surface_not_allowlisted");
        }

        if (!candidateVersionPinned)
        {
            return BuildDecision(originalInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, false, "fallback_original", candidateVersion, "candidate_version_not_pinned");
        }

        var canaryInstructions = RuntimeTokenWorkerWrapperCandidateRenderer.RenderWorkerSystemInstructions(task, packet, requestBudget);
        return BuildDecision(canaryInstructions, mainPathDefaultEnabled, canaryEnabled, killSwitchActive, requestKindAllowlisted, surfaceAllowlisted, candidateVersionPinned, true, "active_canary", candidateVersion, "candidate_applied");
    }

    public RuntimeTokenWorkerWrapperCanaryMechanismContract DescribeMechanismContract()
    {
        return new RuntimeTokenWorkerWrapperCanaryMechanismContract
        {
            TargetSurface = TargetSurface,
            RequestKind = RequestKind,
            ApprovalScope = ApprovalScope,
            CandidateVersion = ApprovedCandidateVersion,
            FallbackVersion = FallbackVersion,
            DefaultOffSupported = true,
            MainPathDefaultSupported = true,
            GlobalKillSwitchSupported = true,
            RequestKindAllowlistSupported = true,
            SurfaceAllowlistSupported = true,
            CandidateVersionPinSupported = true,
            EnvironmentVariables =
            [
                MainPathDefaultEnabledEnvironmentVariable,
                CanaryEnabledEnvironmentVariable,
                GlobalKillSwitchEnvironmentVariable,
                RequestKindAllowlistEnvironmentVariable,
                SurfaceAllowlistEnvironmentVariable,
                CandidateVersionEnvironmentVariable
            ]
        };
    }

    private RuntimeTokenWorkerWrapperCanaryDecision BuildDecision(
        string effectiveInstructions,
        bool mainPathDefaultEnabled,
        bool canaryEnabled,
        bool killSwitchActive,
        bool requestKindAllowlisted,
        bool surfaceAllowlisted,
        bool candidateVersionPinned,
        bool candidateApplied,
        string decisionMode,
        string candidateVersion,
        string decisionReason)
    {
        return new RuntimeTokenWorkerWrapperCanaryDecision
        {
            TargetSurface = TargetSurface,
            RequestKind = RequestKind,
            MainPathDefaultEnabled = mainPathDefaultEnabled,
            CanaryEnabled = canaryEnabled,
            GlobalKillSwitchActive = killSwitchActive,
            RequestKindAllowlisted = requestKindAllowlisted,
            SurfaceAllowlisted = surfaceAllowlisted,
            CandidateVersionPinned = candidateVersionPinned,
            CandidateApplied = candidateApplied,
            DecisionMode = decisionMode,
            DecisionReason = decisionReason,
            ApprovalScope = ApprovalScope,
            CandidateVersion = candidateVersion,
            FallbackVersion = FallbackVersion,
            EffectiveInstructions = effectiveInstructions
        };
    }

    private bool ParseBoolean(string variableName)
    {
        var value = Normalize(getEnvironmentVariable(variableName));
        return string.Equals(value, "1", StringComparison.Ordinal)
               || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAllowlisted(string variableName, string value)
    {
        var raw = Normalize(getEnvironmentVariable(variableName));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => string.Equals(item, value, StringComparison.Ordinal));
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
