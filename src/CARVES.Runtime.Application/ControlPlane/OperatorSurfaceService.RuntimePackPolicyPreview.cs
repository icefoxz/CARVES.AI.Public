using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimePreviewPackPolicy(string inputPath)
    {
        var result = CreateRuntimePackPolicyPreviewService().Preview(inputPath);
        if (!result.Succeeded)
        {
            var lines = new List<string>
            {
                "Runtime pack policy preview rejected.",
                result.Summary,
            };
            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackPolicyPreview(CreateRuntimePackPolicyPreviewService().BuildSurface());
    }

    public OperatorCommandResult InspectRuntimePackPolicyPreview()
    {
        return FormatRuntimePackPolicyPreview(CreateRuntimePackPolicyPreviewService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimePackPolicyPreview()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackPolicyPreviewService().BuildSurface()));
    }

    private RuntimePackPolicyPreviewService CreateRuntimePackPolicyPreviewService()
    {
        return new RuntimePackPolicyPreviewService(repoRoot, artifactRepository, configRepository);
    }

    private static OperatorCommandResult FormatRuntimePackPolicyPreview(RuntimePackPolicyPreviewSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack policy preview",
            surface.Summary,
        };

        if (surface.CurrentPreview is null)
        {
            lines.Add("Current preview: none");
            lines.Add("Supported commands: runtime preview-pack-policy <input-path> | inspect runtime-pack-policy-preview | api runtime-pack-policy-preview");
            return OperatorCommandResult.Success(lines.ToArray());
        }

        lines.Add($"Input path: {surface.CurrentPreview.InputPath}");
        lines.Add($"Package id: {surface.CurrentPreview.PackageId}");
        lines.Add($"Admission policy: {surface.CurrentPreview.CurrentAdmissionPolicy.PolicyId} -> {surface.CurrentPreview.IncomingAdmissionPolicy.PolicyId}");
        lines.Add($"Switch policy: {surface.CurrentPreview.CurrentSwitchPolicy.PolicyId} -> {surface.CurrentPreview.IncomingSwitchPolicy.PolicyId}");
        lines.Add($"Difference count: {surface.CurrentPreview.Differences.Count}");

        if (surface.CurrentPreview.Differences.Count == 0)
        {
            lines.Add("Differences: none");
        }
        else
        {
            lines.Add("Differences:");
            foreach (var difference in surface.CurrentPreview.Differences)
            {
                lines.Add($"- {difference.DiffCode}: {difference.Summary}");
                lines.Add($"  current: {difference.CurrentValue ?? "(none)"}");
                lines.Add($"  incoming: {difference.IncomingValue ?? "(none)"}");
            }
        }

        if (surface.CurrentPreview.ChecksPassed.Length > 0)
        {
            lines.Add($"Checks passed: {string.Join("; ", surface.CurrentPreview.ChecksPassed)}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
