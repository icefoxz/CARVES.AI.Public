using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackPolicyAuditSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-policy-audit.v1";

    public string SurfaceId { get; init; } = "runtime-pack-policy-audit";

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<RuntimePackPolicyAuditEntry> Entries { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record RuntimePackPolicyAuditEntry
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string AuditId { get; init; } = $"packpolicyaudit-{Guid.NewGuid():N}";

    public DateTimeOffset RecordedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string EventKind { get; init; } = string.Empty;

    public string SourceKind { get; init; } = string.Empty;

    public string? PackageId { get; init; }

    public string? PackagePath { get; init; }

    public string? ResultingAdmissionPolicyId { get; init; }

    public string? ResultingSwitchPolicyId { get; init; }

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> ChecksPassed { get; init; } = [];
}
