using Carves.Runtime.Application.Platform;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimeAdmitPack(string packArtifactPath, string attributionPath)
    {
        var result = CreateRuntimePackAdmissionService().Admit(packArtifactPath, attributionPath);
        if (!result.Admitted)
        {
            var lines = new List<string>
            {
                "Runtime pack admission rejected.",
                result.Summary,
                $"Pack validation: {(result.PackValidation.IsValid ? "passed" : "failed")}",
                $"Attribution validation: {(result.AttributionValidation.IsValid ? "passed" : "failed")}",
            };

            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        return FormatRuntimePackAdmission(CreateRuntimePackAdmissionService().BuildSurface(), admitted: true);
    }

    public OperatorCommandResult InspectRuntimePackAdmission()
    {
        return FormatRuntimePackAdmission(CreateRuntimePackAdmissionService().BuildSurface(), admitted: false);
    }

    public OperatorCommandResult ApiRuntimePackAdmission()
    {
        return OperatorCommandResult.Success(operatorApiService.ToJson(CreateRuntimePackAdmissionService().BuildSurface()));
    }

    private RuntimePackAdmissionService CreateRuntimePackAdmissionService()
    {
        return new RuntimePackAdmissionService(repoRoot, configRepository, artifactRepository, specificationValidationService);
    }

    private static OperatorCommandResult FormatRuntimePackAdmission(RuntimePackAdmissionSurface surface, bool admitted)
    {
        var lines = new List<string>
        {
            "Runtime pack admission",
            $"Runtime standard: {surface.RuntimeStandardVersion}",
            surface.Summary,
        };

        if (surface.CurrentAdmission is null)
        {
            lines.Add("Current admission: none");
            return admitted
                ? OperatorCommandResult.Success(lines.ToArray())
                : OperatorCommandResult.Success(lines.ToArray());
        }

        lines.Add($"Pack: {surface.CurrentAdmission.PackId}@{surface.CurrentAdmission.PackVersion} ({surface.CurrentAdmission.Channel})");
        lines.Add($"Artifact path: {surface.CurrentAdmission.PackArtifactPath}");
        lines.Add($"Attribution path: {surface.CurrentAdmission.RuntimePackAttributionPath}");
        lines.Add($"Assignment mode: {surface.CurrentAdmission.Source.AssignmentMode}");
        if (!string.IsNullOrWhiteSpace(surface.CurrentAdmission.Source.AssignmentRef))
        {
            lines.Add($"Assignment ref: {surface.CurrentAdmission.Source.AssignmentRef}");
        }

        if (!string.IsNullOrWhiteSpace(surface.CurrentAdmission.ArtifactRef))
        {
            lines.Add($"Artifact ref: {surface.CurrentAdmission.ArtifactRef}");
        }

        lines.Add(
            $"Profiles: policy={surface.CurrentAdmission.ExecutionProfiles.PolicyPreset}, gate={surface.CurrentAdmission.ExecutionProfiles.GatePreset}, validator={surface.CurrentAdmission.ExecutionProfiles.ValidatorProfile}, environment={surface.CurrentAdmission.ExecutionProfiles.EnvironmentProfile}, routing={surface.CurrentAdmission.ExecutionProfiles.RoutingProfile}");
        if (surface.CurrentAdmission.ChecksPassed.Length > 0)
        {
            lines.Add($"Checks passed: {string.Join("; ", surface.CurrentAdmission.ChecksPassed)}");
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
