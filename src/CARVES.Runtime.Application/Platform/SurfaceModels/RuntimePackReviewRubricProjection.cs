namespace Carves.Runtime.Application.Platform.SurfaceModels;

public sealed class RuntimePackReviewRubricProjection
{
    public string PackId { get; init; } = string.Empty;

    public string PackVersion { get; init; } = string.Empty;

    public string Channel { get; init; } = string.Empty;

    public string ManifestPath { get; init; } = string.Empty;

    public int RubricCount { get; init; }

    public int ChecklistItemCount { get; init; }

    public IReadOnlyList<RuntimePackReviewRubricProjectionEntry> Rubrics { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string[] Notes { get; init; } = [];
}

public sealed class RuntimePackReviewRubricProjectionEntry
{
    public string RubricId { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<RuntimePackReviewRubricChecklistItem> ChecklistItems { get; init; } = [];
}

public sealed class RuntimePackReviewRubricChecklistItem
{
    public string ChecklistItemId { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;
}
