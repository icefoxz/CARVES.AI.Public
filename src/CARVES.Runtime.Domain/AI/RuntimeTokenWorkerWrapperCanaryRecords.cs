namespace Carves.Runtime.Domain.AI;

public sealed record RuntimeTokenWorkerWrapperCanaryDecision
{
    public string TargetSurface { get; init; } = "worker:system:$.instructions";

    public string RequestKind { get; init; } = "worker";

    public bool MainPathDefaultEnabled { get; init; }

    public bool CanaryEnabled { get; init; }

    public bool GlobalKillSwitchActive { get; init; }

    public bool RequestKindAllowlisted { get; init; }

    public bool SurfaceAllowlisted { get; init; }

    public bool CandidateVersionPinned { get; init; }

    public bool CandidateApplied { get; init; }

    public string DecisionMode { get; init; } = "fallback_original";

    public string DecisionReason { get; init; } = "default_off";

    public string ApprovalScope { get; init; } = "limited_explicit_allowlist";

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = "original_worker_system_instructions";

    public string EffectiveInstructions { get; init; } = string.Empty;
}

public sealed record RuntimeTokenWorkerWrapperCanaryMechanismContract
{
    public string TargetSurface { get; init; } = "worker:system:$.instructions";

    public string RequestKind { get; init; } = "worker";

    public string ApprovalScope { get; init; } = "limited_explicit_allowlist";

    public string CandidateVersion { get; init; } = string.Empty;

    public string FallbackVersion { get; init; } = "original_worker_system_instructions";

    public bool DefaultOffSupported { get; init; }

    public bool MainPathDefaultSupported { get; init; }

    public bool GlobalKillSwitchSupported { get; init; }

    public bool RequestKindAllowlistSupported { get; init; }

    public bool SurfaceAllowlistSupported { get; init; }

    public bool CandidateVersionPinSupported { get; init; }

    public IReadOnlyList<string> EnvironmentVariables { get; init; } = Array.Empty<string>();
}
