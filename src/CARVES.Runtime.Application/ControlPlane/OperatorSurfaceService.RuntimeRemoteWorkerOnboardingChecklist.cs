using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult InspectRuntimeRemoteWorkerOnboardingChecklist()
    {
        return FormatRuntimeRemoteWorkerOnboardingChecklist(
            new RuntimeRemoteWorkerOnboardingChecklistService(providerRegistryService).BuildSurface());
    }

    public OperatorCommandResult ApiRuntimeRemoteWorkerOnboardingChecklist()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(
            new RuntimeRemoteWorkerOnboardingChecklistService(providerRegistryService).BuildSurface()));
    }

    private static OperatorCommandResult FormatRuntimeRemoteWorkerOnboardingChecklist(RuntimeRemoteWorkerOnboardingChecklistSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime remote worker onboarding checklist",
            surface.Summary,
            $"Current activation mode: {surface.CurrentActivationMode}",
            $"Allowed external worker paths: {string.Join(", ", surface.AllowedExternalWorkerPaths)}",
            $"SDK/API worker backends allowed: {surface.SdkApiWorkerBackendsAllowed}",
            $"SDK/API worker boundary: {surface.SdkApiWorkerBoundary}",
            $"Disallowed worker backends: {string.Join(", ", surface.DisallowedWorkerBackendIds)}",
        };

        foreach (var provider in surface.Providers)
        {
            lines.Add($"Provider/backend: {provider.ProviderId}/{provider.BackendId}");
            lines.Add($"Routing profile: {provider.RoutingProfileId}");
            lines.Add($"Qualification policy: {provider.QualificationPolicyId}");
            lines.Add($"Qualification surface: {provider.QualificationSurfaceId}");
            lines.Add($"Activation state: {provider.ActivationState}");
            lines.Add($"Current worker selection eligible: {provider.CurrentWorkerSelectionEligible}");
            lines.Add("Checklist:");
            lines.AddRange(provider.Checklist.Select(item =>
                $"- {item.ItemId} [{item.State}]: {item.Summary} ({string.Join(", ", item.EvidenceReferences)})"));
            if (provider.Notes.Length > 0)
            {
                lines.Add("Provider notes:");
                lines.AddRange(provider.Notes.Select(item => $"- {item}"));
            }
        }

        if (surface.Notes.Length > 0)
        {
            lines.Add("Notes:");
            lines.AddRange(surface.Notes.Select(item => $"- {item}"));
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
