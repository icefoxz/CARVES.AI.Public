using Carves.Runtime.Domain.Platform;
namespace Carves.Runtime.Application.Platform.SurfaceModels;

public enum CodexToolActionClass
{
    PlannerOnly,
    WorkerAllowed,
    LocalEphemeral,
}

public enum CodexToolAvailability
{
    Available,
    Partial,
    Planned,
}

public sealed class CodexToolDescriptor
{
    public string ToolId { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public CodexToolActionClass ActionClass { get; init; } = CodexToolActionClass.WorkerAllowed;

    public CodexToolAvailability Availability { get; init; } = CodexToolAvailability.Planned;

    public bool TruthAffecting { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string[] CurrentCommandMappings { get; init; } = [];

    public string[] DependsOnCards { get; init; } = [];

    public string[] Notes { get; init; } = [];
}

public sealed class CodexToolSurfaceSnapshot
{
    public string SchemaVersion { get; init; } = "codex-tool-surface.v1";

    public string SurfaceId { get; init; } = "codex-tool-surface";

    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Summary { get; init; } = string.Empty;

    public CodexToolDescriptor[] Tools { get; init; } = [];
}
