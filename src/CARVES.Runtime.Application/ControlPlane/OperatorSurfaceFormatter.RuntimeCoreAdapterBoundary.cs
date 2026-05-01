using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeCoreAdapterBoundary(RuntimeCoreAdapterBoundarySurface surface)
    {
        var lines = new List<string>
        {
            "Runtime core/adapter boundary",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Summary: {surface.Summary}",
            "Families:",
        };

        foreach (var family in surface.Families.OrderBy(item => item.Classification, StringComparer.Ordinal).ThenBy(item => item.FamilyId, StringComparer.Ordinal))
        {
            lines.Add($"- {family.FamilyId} [{family.Classification}] lifecycle={family.LifecycleClass}");
            lines.Add($"  summary: {family.Summary}");
            lines.Add($"  paths: {(family.PathRefs.Length == 0 ? "(none)" : string.Join(" | ", family.PathRefs))}");
            lines.Add($"  truth refs: {(family.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", family.TruthRefs))}");
            if (family.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", family.Notes)}");
            }
        }

        lines.Add("Neutral core contracts:");
        foreach (var contract in surface.CoreContracts.OrderBy(item => item.ContractId, StringComparer.Ordinal))
        {
            lines.Add($"- {contract.ContractId}");
            lines.Add($"  schema: {contract.SchemaPath}");
            lines.Add($"  summary: {contract.Summary}");
            lines.Add($"  provider boundary: {contract.ProviderExtensionBoundary}");
            lines.Add($"  truth refs: {(contract.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", contract.TruthRefs))}");
            lines.Add($"  allowed refs: {(contract.AllowedExtensionReferences.Length == 0 ? "(none)" : string.Join(" | ", contract.AllowedExtensionReferences))}");
            lines.Add($"  forbidden embedded payloads: {(contract.ForbiddenEmbeddedPayloads.Length == 0 ? "(none)" : string.Join(" | ", contract.ForbiddenEmbeddedPayloads))}");
        }

        if (surface.Notes.Length > 0)
        {
            lines.Add("Notes:");
            foreach (var note in surface.Notes)
            {
                lines.Add($"- {note}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
