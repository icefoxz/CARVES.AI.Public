namespace Carves.Runtime.Application.ControlPlane;

public sealed partial class OperatorSurfaceService
{
    public OperatorCommandResult ValidateCard(string cardPath, bool strict)
        => FormatValidationResult(specificationValidationService.ValidateCard(cardPath, strict));

    public OperatorCommandResult ValidateTask(string taskPath)
        => FormatValidationResult(specificationValidationService.ValidateTask(taskPath));

    public OperatorCommandResult ValidateMemory(string memoryMetaPath, bool strict)
        => FormatValidationResult(specificationValidationService.ValidateMemory(memoryMetaPath, strict));

    public OperatorCommandResult ValidatePackArtifact(string packArtifactPath)
        => FormatValidationResult(specificationValidationService.ValidatePackArtifact(packArtifactPath));

    public OperatorCommandResult ValidateRuntimePackV1(string manifestPath)
        => FormatValidationResult(specificationValidationService.ValidateRuntimePackV1(manifestPath));

    public OperatorCommandResult ValidateRuntimePackAttribution(string attributionPath)
        => FormatValidationResult(specificationValidationService.ValidateRuntimePackAttribution(attributionPath));

    public OperatorCommandResult ValidateRuntimePackPolicyPackage(string packagePath)
        => FormatValidationResult(specificationValidationService.ValidateRuntimePackPolicyPackage(packagePath));

    public OperatorCommandResult ValidateSafety(string actor, string operation, IReadOnlyList<string> targetPaths)
        => FormatValidationResult(specificationValidationService.ValidateSafety(actor, operation, targetPaths));

    private static OperatorCommandResult FormatValidationResult(SpecificationValidationResult result)
    {
        var warningCount = result.Issues.Count(issue => string.Equals(issue.Severity, "warning", StringComparison.OrdinalIgnoreCase));
        var errorCount = result.Issues.Count(issue => string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase));
        var lines = new List<string>
        {
            result.IsValid ? "Validation passed." : "Validation failed.",
            $"Validator: {result.Validator}",
            $"Target: {result.Target}",
            $"Errors: {errorCount}",
            $"Warnings: {warningCount}",
        };

        foreach (var issue in result.Issues)
        {
            var field = string.IsNullOrWhiteSpace(issue.Field) ? string.Empty : $" [{issue.Field}]";
            lines.Add($"{issue.Severity}: {issue.Code}{field} {issue.Message}");
        }

        return new OperatorCommandResult(result.IsValid ? 0 : 1, lines);
    }
}
