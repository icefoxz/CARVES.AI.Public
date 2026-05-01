using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimeExportPackPolicy(string outputPath)
    {
        var result = CreateRuntimePackPolicyTransferService().Export(outputPath);
        if (!result.Succeeded || result.Package is null || string.IsNullOrWhiteSpace(result.Path))
        {
            var lines = new List<string>
            {
                "Runtime pack policy export rejected.",
                result.Summary,
            };
            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return OperatorCommandResult.Success(
        [
            "Runtime pack policy exported.",
            result.Summary,
            $"Output path: {result.Path}",
            $"Package id: {result.Package.PackageId}",
        ]);
    }

    public OperatorCommandResult RuntimeImportPackPolicy(string inputPath)
    {
        var result = CreateRuntimePackPolicyTransferService().Import(inputPath);
        if (!result.Succeeded)
        {
            var lines = new List<string>
            {
                "Runtime pack policy import rejected.",
                result.Summary,
            };
            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackPolicyTransfer(CreateRuntimePackPolicyTransferService().BuildSurface());
    }

    public OperatorCommandResult InspectRuntimePackPolicyTransfer()
    {
        return FormatRuntimePackPolicyTransfer(CreateRuntimePackPolicyTransferService().BuildSurface());
    }

    public OperatorCommandResult ApiRuntimePackPolicyTransfer()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackPolicyTransferService().BuildSurface()));
    }

    private RuntimePackPolicyTransferService CreateRuntimePackPolicyTransferService()
    {
        return new RuntimePackPolicyTransferService(repoRoot, artifactRepository, configRepository);
    }

    private static OperatorCommandResult FormatRuntimePackPolicyTransfer(RuntimePackPolicyTransferSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime pack policy transfer",
            $"Runtime standard: {surface.RuntimeStandardVersion}",
            surface.Summary,
            $"Admission policy: {surface.CurrentAdmissionPolicy.PolicyId}",
            $"Switch policy: {surface.CurrentSwitchPolicy.PolicyId}",
            $"Supported commands: {string.Join(" | ", surface.SupportedCommands)}",
        };

        if (surface.CurrentAdmissionPolicy.AllowedChannels.Count > 0)
        {
            lines.Add($"Allowed channels: {string.Join(", ", surface.CurrentAdmissionPolicy.AllowedChannels)}");
        }

        lines.Add($"Pin active: {surface.CurrentSwitchPolicy.PinActive}");
        return OperatorCommandResult.Success(lines.ToArray());
    }
}
