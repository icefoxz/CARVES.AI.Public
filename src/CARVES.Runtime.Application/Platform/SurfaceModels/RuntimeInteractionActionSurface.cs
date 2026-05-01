namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimeInteractionActionSurface
{
    public string ActionId { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;

    public string Command { get; init; } = string.Empty;

    public string ActionMode { get; init; } = "direct";

    public string Summary { get; init; } = string.Empty;

    public bool Enabled { get; init; } = true;

    public string? DisabledReason { get; init; }
}
