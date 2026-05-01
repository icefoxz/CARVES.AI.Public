using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimePinCurrentPack(string? reason)
    {
        var result = CreateRuntimePackSwitchPolicyService().PinCurrentSelection(reason);
        if (!result.Accepted)
        {
            var lines = new List<string>
            {
                "Runtime pack pin rejected.",
                result.Summary,
            };

            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackSwitchPolicy(CreateRuntimePackSwitchPolicyService().BuildSurface());
    }

    public OperatorCommandResult RuntimeClearPackPin(string? reason)
    {
        var result = CreateRuntimePackSwitchPolicyService().ClearPin(reason);
        if (!result.Accepted)
        {
            var lines = new List<string>
            {
                "Runtime pack pin clear rejected.",
                result.Summary,
            };

            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackSwitchPolicy(CreateRuntimePackSwitchPolicyService().BuildSurface());
    }

    public OperatorCommandResult InspectRuntimePackSwitchPolicy()
    {
        return FormatRuntimePackSwitchPolicy(CreateRuntimePackSwitchPolicyService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimePackSwitchPolicy()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackSwitchPolicyService().BuildSurface()));
    }

    private RuntimePackSwitchPolicyService CreateRuntimePackSwitchPolicyService()
    {
        return new RuntimePackSwitchPolicyService(artifactRepository);
    }

    private static OperatorCommandResult FormatRuntimePackSwitchPolicy(RuntimePackSwitchPolicySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack switch policy",
            surface.Summary,
        };

        if (surface.CurrentSelection is null)
        {
            lines.Add("Current selection: none");
        }
        else
        {
            lines.Add($"Current selection: {surface.CurrentSelection.PackId}@{surface.CurrentSelection.PackVersion} ({surface.CurrentSelection.Channel})");
            lines.Add($"Selection id: {surface.CurrentSelection.SelectionId}");
        }

        lines.Add($"Policy mode: {surface.CurrentPolicy.PolicyMode}");
        lines.Add($"Pin active: {surface.CurrentPolicy.PinActive}");
        if (surface.CurrentPolicy.PinActive)
        {
            lines.Add($"Pinned selection: {surface.CurrentPolicy.PinnedSelectionId}");
            lines.Add($"Pinned identity: {surface.CurrentPolicy.PackId}@{surface.CurrentPolicy.PackVersion} ({surface.CurrentPolicy.Channel})");
        }
        lines.Add($"Reason: {surface.CurrentPolicy.Reason}");
        if (surface.CurrentPolicy.ChecksPassed.Length > 0)
        {
            lines.Add($"Checks passed: {string.Join("; ", surface.CurrentPolicy.ChecksPassed)}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
