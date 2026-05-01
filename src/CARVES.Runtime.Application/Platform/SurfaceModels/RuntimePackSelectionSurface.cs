using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackSelectionSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-selection.v3";

    public string SurfaceId { get; init; } = "runtime-pack-selection";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackSelectionArtifact? CurrentSelection { get; init; }

    public RuntimePackAdmissionArtifact? CurrentAdmission { get; init; }

    public IReadOnlyList<RuntimePackSelectionArtifact> History { get; init; } = [];

    public string AuditSummary { get; init; } = string.Empty;

    public IReadOnlyList<RuntimePackSelectionAuditEntry> AuditTrail { get; init; } = [];

    public RuntimePackSelectionRollbackContext RollbackContext { get; init; } = new();

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimePackSelectionArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string SelectionId { get; init; } = string.Empty;

    public DateTimeOffset SelectedAt { get; init; } = DateTimeOffset.UtcNow;

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public string PackArtifactPath { get; init; } = string.Empty;

    public string RuntimePackAttributionPath { get; init; } = string.Empty;

    public string ArtifactRef { get; init; } = string.Empty;

    public RuntimePackAdmissionProfileSelection ExecutionProfiles { get; init; } = new();

    public RuntimePackAdmissionSource AdmissionSource { get; init; } = new();

    public DateTimeOffset AdmissionCapturedAt { get; init; }

    public string SelectionMode { get; init; } = string.Empty;

    public string SelectionReason { get; init; } = string.Empty;

    public string? PreviousSelectionId { get; init; }

    public string? RollbackTargetSelectionId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] ChecksPassed { get; init; } = [];
}

public sealed class RuntimePackSelectionRollbackContext
{
    public string Summary { get; init; } = string.Empty;

    public bool CanRollback { get; init; }

    public string CurrentAdmissionIdentity { get; init; } = string.Empty;

    public IReadOnlyList<RuntimePackSelectionArtifact> EligibleTargets { get; init; } = [];
}

public sealed class RuntimePackSelectionAuditEntry
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string AuditId { get; init; } = string.Empty;

    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;

    public string EventKind { get; init; } = string.Empty;

    public string SelectionId { get; init; } = string.Empty;

    public string? PreviousSelectionId { get; init; }

    public string? RollbackTargetSelectionId { get; init; }

    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string ArtifactRef { get; init; } = string.Empty;

    public string SelectionMode { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] ChecksPassed { get; init; } = [];
}
