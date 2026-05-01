using Carves.Runtime.Application.Platform;

namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult RuntimeAdmitPackV1(string manifestPath, string? channel, string? publishedBy, string? sourcePackLine)
    {
        var result = CreateRuntimePackV1ManifestAdmissionBridgeService().Admit(manifestPath, channel, publishedBy, sourcePackLine);
        if (!result.Admitted)
        {
            var lines = new List<string>
            {
                "Runtime Pack v1 manifest admission bridge rejected.",
                result.Summary,
                $"Manifest validation: {(result.ManifestValidation.IsValid ? "passed" : "failed")}",
            };

            if (!string.IsNullOrWhiteSpace(result.GeneratedPackArtifactPath))
            {
                lines.Add($"Generated artifact path: {result.GeneratedPackArtifactPath}");
            }

            if (!string.IsNullOrWhiteSpace(result.GeneratedAttributionPath))
            {
                lines.Add($"Generated attribution path: {result.GeneratedAttributionPath}");
            }

            if (result.FailureCodes.Count > 0)
            {
                lines.Add($"Failure codes: {string.Join(", ", result.FailureCodes)}");
            }

            return OperatorCommandResult.Failure(lines.ToArray());
        }

        var admissionSurface = CreateRuntimePackAdmissionService().BuildSurface();
        var formattedAdmission = FormatRuntimePackAdmission(admissionSurface, admitted: true);
        var output = new List<string>
        {
            "Runtime Pack v1 manifest admission bridge",
            $"Manifest path: {result.ManifestPath}",
            $"Generated artifact path: {result.GeneratedPackArtifactPath}",
            $"Generated attribution path: {result.GeneratedAttributionPath}",
            result.Summary,
            string.Empty,
        };
        output.AddRange(formattedAdmission.Lines);
        return OperatorCommandResult.Success(output.ToArray());
    }

    private RuntimePackV1ManifestAdmissionBridgeService CreateRuntimePackV1ManifestAdmissionBridgeService()
    {
        return new RuntimePackV1ManifestAdmissionBridgeService(repoRoot, paths, configRepository, artifactRepository, specificationValidationService);
    }
}
