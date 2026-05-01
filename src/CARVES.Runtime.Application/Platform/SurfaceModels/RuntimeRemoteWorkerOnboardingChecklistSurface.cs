using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeRemoteWorkerOnboardingChecklistSurface
{
    public string SchemaVersion { get; init; } = "runtime-remote-worker-onboarding-checklist.v1";

    public string SurfaceId { get; init; } = "runtime-remote-worker-onboarding-checklist";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public string CurrentActivationMode { get; init; } = "external_app_cli_only";

    public string[] AllowedExternalWorkerPaths { get; init; } = [];

    public string[] DisallowedWorkerBackendIds { get; init; } = [];

    public bool SdkApiWorkerBackendsAllowed { get; init; }

    public string SdkApiWorkerBoundary { get; init; } = string.Empty;

    public RuntimeRemoteWorkerOnboardingProviderEntry[] Providers { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimeRemoteWorkerOnboardingProviderEntry
{
    public string ProviderId { get; init; } = string.Empty;

    public string BackendId { get; init; } = string.Empty;

    public string RoutingProfileId { get; init; } = string.Empty;

    public string ProtocolFamily { get; init; } = string.Empty;

    public string QualificationPolicyId { get; init; } = string.Empty;

    public string QualificationSurfaceId { get; init; } = "runtime-remote-worker-qualification";

    public string ActivationState { get; init; } = string.Empty;

    public bool CurrentWorkerSelectionEligible { get; init; }

    public RuntimeRemoteWorkerOnboardingChecklistItem[] Checklist { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimeRemoteWorkerOnboardingChecklistItem
{
    public string ItemId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] EvidenceReferences { get; init; } = [];
}
