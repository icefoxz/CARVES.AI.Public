using Carves.Runtime.Domain.Platform;
using Carves.Runtime.Domain.Tasks;

namespace Carves.Runtime.Application.Platform.SurfaceModels;

public enum ExecutionContractAvailability
{
    Defined,
    Partial,
    Planned,
}

public sealed class ExecutionContractDescriptor
{
    public string ContractId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public ExecutionContractAvailability Availability { get; init; } = ExecutionContractAvailability.Planned;

    public string SchemaPath { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string[] CurrentTruthLineage { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class ExecutionContractSurfaceSnapshot
{
    public string SchemaVersion { get; init; } = "execution-contract-surface.v1";

    public string SurfaceId { get; init; } = "execution-contract-surface";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public ExecutionContractDescriptor[] Contracts { get; init; } = [];

    public PlannerVerdictContract[] PlannerVerdicts { get; init; } = [];
}
