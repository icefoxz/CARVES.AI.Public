using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackMismatchDiagnosticsSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-mismatch-diagnostics.v1";

    public string SurfaceId { get; init; } = "runtime-pack-mismatch-diagnostics";

    public string Summary { get; init; } = string.Empty;

    public RuntimePackAdmissionArtifact? CurrentAdmission { get; init; }

    public RuntimePackSelectionArtifact? CurrentSelection { get; init; }

    public RuntimePackSwitchPolicyArtifact CurrentPolicy { get; init; } = RuntimePackSwitchPolicyArtifact.CreateDefault();

    public RuntimePackExecutionAuditCoverage ExecutionCoverage { get; init; } = new();

    public IReadOnlyList<RuntimePackMismatchDiagnostic> Diagnostics { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed class RuntimePackMismatchDiagnostic
{
    public string DiagnosticCode { get; init; } = string.Empty;

    public string Severity { get; init; } = "warning";

    public string Summary { get; init; } = string.Empty;

    public string Details { get; init; } = string.Empty;

    public IReadOnlyList<string> RelatedTaskIds { get; init; } = [];

    public IReadOnlyList<string> RecommendedActions { get; init; } = [];
}
