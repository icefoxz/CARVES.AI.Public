using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackPolicyTransferSurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-policy-transfer.v1";

    public string SurfaceId { get; init; } = "runtime-pack-policy-transfer";

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackAdmissionPolicyArtifact CurrentAdmissionPolicy { get; init; } = RuntimePackAdmissionPolicyArtifact.CreateDefault(string.Empty);

    public RuntimePackSwitchPolicyArtifact CurrentSwitchPolicy { get; init; } = RuntimePackSwitchPolicyArtifact.CreateDefault();

    public IReadOnlyList<string> SupportedCommands { get; init; } = [];

    public IReadOnlyList<string> Notes { get; init; } = [];
}

public sealed record RuntimePackPolicyPackage
{
    public string SchemaVersion { get; init; } = "runtime-pack-policy-package.v1";

    public string PackageId { get; init; } = $"packpolpkg-{Guid.NewGuid():N}";

    public DateTimeOffset ExportedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public string SourceMode { get; init; } = "local_runtime_export";

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public RuntimePackAdmissionPolicyArtifact AdmissionPolicy { get; init; } = RuntimePackAdmissionPolicyArtifact.CreateDefault(string.Empty);

    public RuntimePackSwitchPolicyArtifact SwitchPolicy { get; init; } = RuntimePackSwitchPolicyArtifact.CreateDefault();

    public string Summary { get; init; } = string.Empty;

    public IReadOnlyList<string> Notes { get; init; } = [];
}
