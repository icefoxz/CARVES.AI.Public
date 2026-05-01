using Carves.Runtime.Application.Configuration;
using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult PolicyInspect()
    {
        var bundle = runtimePolicyBundleService.Load();
        var exportProfiles = CreateRuntimeExportProfileService().BuildSurface();
        var validation = MergePolicyValidation(runtimePolicyBundleService.Validate(), CreateRuntimeExportProfileService().Validate());
        return OperatorSurfaceFormatter.PolicyInspect(paths, bundle, validation, exportProfiles);
    }

    public OperatorCommandResult PolicyValidate()
    {
        return OperatorSurfaceFormatter.PolicyValidate(MergePolicyValidation(runtimePolicyBundleService.Validate(), CreateRuntimeExportProfileService().Validate()));
    }

    private RuntimeExportProfileService CreateRuntimeExportProfileService()
    {
        return new RuntimeExportProfileService(repoRoot, paths, systemConfig);
    }

    private static RuntimePolicyValidationResult MergePolicyValidation(
        RuntimePolicyValidationResult primary,
        RuntimePolicyValidationResult secondary)
    {
        return new RuntimePolicyValidationResult(
            primary.IsValid && secondary.IsValid,
            primary.Errors.Concat(secondary.Errors).ToArray(),
            primary.Warnings.Concat(secondary.Warnings).ToArray());
    }
}
