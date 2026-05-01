using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Runtime;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackAdmissionPolicySurface
{
    public string SchemaVersion { get; init; } = "runtime-pack-admission-policy.v1";

    public string SurfaceId { get; init; } = "runtime-pack-admission-policy";

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public RuntimePackAdmissionPolicyArtifact CurrentPolicy { get; init; } = RuntimePackAdmissionPolicyArtifact.CreateDefault(string.Empty);

    public string[] Notes { get; init; } = [];
}

public sealed record RuntimePackAdmissionPolicyArtifact
{
    public int SchemaVersion { get; init; } = RuntimeProtocol.ArtifactSchemaVersion;

    public string PolicyId { get; init; } = "runtime-pack-admission-policy-default";

    public string PolicyMode { get; init; } = "local_runtime_default";

    public string RuntimeStandardVersion { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedChannels { get; init; } = [];

    public IReadOnlyList<string> AllowedPackTypes { get; init; } = [];

    public bool RequireSignature { get; init; } = true;

    public bool RequireProvenance { get; init; } = true;

    public string Summary { get; init; } = string.Empty;

    public string[] ChecksPassed { get; init; } = [];

    public bool AllowsChannel(string channel)
    {
        return AllowedChannels.Contains(channel, StringComparer.Ordinal);
    }

    public bool AllowsPackType(string packType)
    {
        return AllowedPackTypes.Contains(packType, StringComparer.Ordinal);
    }

    public static RuntimePackAdmissionPolicyArtifact CreateDefault(string runtimeStandardVersion)
    {
        return new RuntimePackAdmissionPolicyArtifact
        {
            RuntimeStandardVersion = runtimeStandardVersion,
            AllowedChannels = ["stable", "candidate"],
            AllowedPackTypes = ["runtime_pack", "vertical_runtime_pack"],
            RequireSignature = true,
            RequireProvenance = true,
            Summary = "Runtime-local pack admission policy accepts only stable/candidate runtime pack lines with signature and provenance present.",
            ChecksPassed =
            [
                "policy remains local-runtime scoped",
                "preview and canary channels stay closed by default",
                "signature and provenance remain required before local admission"
            ],
        };
    }
}
