namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeGovernanceArchiveStatus(RuntimeGovernanceArchiveStatusSurface surface)
    {
        var lines = new List<string>
        {
            "Runtime governance archive status",
            $"Role: {surface.SurfaceRole}; primary={surface.PrimarySurfaceId}; successor={surface.SuccessorSurfaceId ?? "(self)"}; retirement={surface.RetirementPosture}",
            $"Legacy argument: {surface.LegacyArgument ?? "(none)"}",
            surface.Summary,
            $"Exposure: default_visible={surface.DefaultVisibleSurfaceCount}/{surface.DefaultVisibleBudget}; primary_surfaces={surface.PrimarySurfaceCount}; compatibility_aliases={surface.CompatibilityAliasCount}",
            $"Output budget: inspect_max_lines={surface.OutputBudget.InspectMaxLines}; aliases={surface.OutputBudget.AliasEntryCount}; historical_document_bodies_embedded={surface.OutputBudget.HistoricalDocumentBodiesEmbedded}",
            $"Consumer inventory: roots={string.Join(", ", surface.ConsumerInventory.ScannedRoots)}; files={surface.ConsumerInventory.ScannedFileCount}; reference_files={surface.ConsumerInventory.ReferenceFileCount}; skipped={surface.ConsumerInventory.SkippedFileCount}",
            $"Compatibility classes: {string.Join(", ", surface.CompatibilityClassCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value}"))}",
            "Compatibility aliases:",
        };

        foreach (var alias in surface.LegacyAliases)
        {
            lines.Add($"- {alias.SurfaceId} -> {alias.SuccessorSurfaceId}; class={alias.CompatibilityClass}; json_field_consumers={alias.JsonFieldConsumerReferenceCount}; evidence={alias.CompatibilityEvidenceRefs.Count}; usage={alias.InspectUsage}");
        }

        lines.Add("Expansion pointers:");
        foreach (var pointer in surface.ExpansionPointers)
        {
            lines.Add($"- {pointer.SurfaceId}: {pointer.Path}; mode={pointer.ReadMode}");
        }

        lines.AddRange(surface.NonClaims.Select(item => $"- {item}"));
        lines.Add($"Validation: valid={surface.IsValid}; errors={surface.Errors.Count}; warnings={surface.Warnings.Count}");
        lines.AddRange(surface.Errors.Select(error => $"- error: {error}"));
        lines.AddRange(surface.Warnings.Take(3).Select(warning => $"- warning: {warning}"));
        return new OperatorCommandResult(surface.IsValid ? 0 : 1, lines);
    }
}
