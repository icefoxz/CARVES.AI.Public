using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeCoreAdapterBoundarySurface
{
    public string SchemaVersion { get; init; } = "runtime-core-adapter-boundary.v1";

    public string SurfaceId { get; init; } = "runtime-core-adapter-boundary";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public RuntimeBoundaryFamilyDescriptor[] Families { get; init; } = [];

    public RuntimeNeutralCoreContractDescriptor[] CoreContracts { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimeBoundaryFamilyDescriptor
{
    public string FamilyId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Classification { get; init; } = string.Empty;

    public string LifecycleClass { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] PathRefs { get; init; } = [];

    public string[] TruthRefs { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimeNeutralCoreContractDescriptor
{
    public string ContractId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string SchemaPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string ProviderExtensionBoundary { get; init; } = string.Empty;

    public string[] TruthRefs { get; init; } = [];

    public string[] AllowedExtensionReferences { get; init; } = [];

    public string[] ForbiddenEmbeddedPayloads { get; init; } = [];

    public string[] Notes { get; init; } = [];
}
