using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeCodeUnderstandingEngine(RuntimeCodeUnderstandingEngineSurface surface)
    {
        var policy = surface.Policy;
        var lines = new List<string>
        {
            "Runtime code-understanding engine",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {policy.PolicyVersion}",
            $"Summary: {policy.Summary}",
            $"Strengthens truth root: {policy.StrengthensTruthRoot}",
            "Extraction boundary:",
            $"- direct absorptions: {string.Join(" | ", policy.ExtractionBoundary.DirectAbsorptions)}",
            $"  translated into CARVES: {string.Join(" | ", policy.ExtractionBoundary.TranslatedIntoCarves)}",
            $"  rejected anchors: {string.Join(" | ", policy.ExtractionBoundary.RejectedAnchors)}",
            "Concern families:",
        };

        foreach (var family in policy.ConcernFamilies.OrderBy(item => item.FamilyId, StringComparer.Ordinal))
        {
            lines.Add($"- {family.FamilyId} [{family.Layer}]");
            lines.Add($"  summary: {family.Summary}");
            lines.Add($"  status: {family.CurrentStatus}");
            lines.Add($"  source projects: {(family.SourceProjects.Length == 0 ? "(none)" : string.Join(" | ", family.SourceProjects))}");
            lines.Add($"  direct absorptions: {(family.DirectAbsorptions.Length == 0 ? "(none)" : string.Join(" | ", family.DirectAbsorptions))}");
            lines.Add($"  translation targets: {(family.TranslationTargets.Length == 0 ? "(none)" : string.Join(" | ", family.TranslationTargets))}");
            lines.Add($"  governed entry points: {(family.GovernedEntryPoints.Length == 0 ? "(none)" : string.Join(" | ", family.GovernedEntryPoints))}");
            lines.Add($"  review boundaries: {(family.ReviewBoundaries.Length == 0 ? "(none)" : string.Join(" | ", family.ReviewBoundaries))}");
            lines.Add($"  out of scope: {(family.OutOfScope.Length == 0 ? "(none)" : string.Join(" | ", family.OutOfScope))}");
            if (family.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", family.Notes)}");
            }
        }

        lines.Add("Precision tiers:");
        foreach (var tier in policy.PrecisionTiers.OrderBy(item => item.TierId, StringComparer.Ordinal))
        {
            lines.Add($"- {tier.TierId}");
            lines.Add($"  summary: {tier.Summary}");
            lines.Add($"  primary read path: {tier.PrimaryReadPath}");
            lines.Add($"  escalation trigger: {tier.EscalationTrigger}");
            lines.Add($"  evidence refs: {(tier.EvidenceRefs.Length == 0 ? "(none)" : string.Join(" | ", tier.EvidenceRefs))}");
            lines.Add($"  non-claims: {(tier.NonClaims.Length == 0 ? "(none)" : string.Join(" | ", tier.NonClaims))}");
        }

        lines.Add("Semantic path pilots:");
        foreach (var pilot in policy.SemanticPathPilots.OrderBy(item => item.PilotId, StringComparer.Ordinal))
        {
            lines.Add($"- {pilot.PilotId} [{pilot.Language}]");
            lines.Add($"  status: {pilot.CurrentStatus}");
            lines.Add($"  target precision tiers: {(pilot.TargetPrecisionTiers.Length == 0 ? "(none)" : string.Join(" | ", pilot.TargetPrecisionTiers))}");
            lines.Add($"  summary: {pilot.Summary}");
            lines.Add($"  scope roots: {(pilot.ScopeRoots.Length == 0 ? "(none)" : string.Join(" | ", pilot.ScopeRoots))}");
            lines.Add($"  governed entry points: {(pilot.GovernedEntryPoints.Length == 0 ? "(none)" : string.Join(" | ", pilot.GovernedEntryPoints))}");
            lines.Add($"  evidence refs: {(pilot.EvidenceRefs.Length == 0 ? "(none)" : string.Join(" | ", pilot.EvidenceRefs))}");
            lines.Add($"  guardrails: {(pilot.Guardrails.Length == 0 ? "(none)" : string.Join(" | ", pilot.Guardrails))}");
            lines.Add($"  non-claims: {(pilot.NonClaims.Length == 0 ? "(none)" : string.Join(" | ", pilot.NonClaims))}");
        }

        lines.Add("Truth roots:");
        foreach (var root in policy.TruthRoots.OrderBy(item => item.RootId, StringComparer.Ordinal))
        {
            lines.Add($"- {root.RootId} [{root.Classification}]");
            lines.Add($"  summary: {root.Summary}");
            lines.Add($"  paths: {(root.PathRefs.Length == 0 ? "(none)" : string.Join(" | ", root.PathRefs))}");
            lines.Add($"  truth refs: {(root.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", root.TruthRefs))}");
        }

        lines.Add("Boundary rules:");
        foreach (var rule in policy.BoundaryRules.OrderBy(item => item.RuleId, StringComparer.Ordinal))
        {
            lines.Add($"- {rule.RuleId}");
            lines.Add($"  summary: {rule.Summary}");
            lines.Add($"  allowed: {(rule.AllowedRefs.Length == 0 ? "(none)" : string.Join(" | ", rule.AllowedRefs))}");
            lines.Add($"  forbidden: {(rule.ForbiddenRefs.Length == 0 ? "(none)" : string.Join(" | ", rule.ForbiddenRefs))}");
        }

        lines.Add("Governed read paths:");
        foreach (var readPath in policy.GovernedReadPaths.OrderBy(item => item.PathId, StringComparer.Ordinal))
        {
            lines.Add($"- {readPath.PathId}");
            lines.Add($"  entry: {readPath.EntryPoint}");
            lines.Add($"  summary: {readPath.Summary}");
            lines.Add($"  truth refs: {(readPath.TruthRefs.Length == 0 ? "(none)" : string.Join(" | ", readPath.TruthRefs))}");
        }

        lines.Add("Qualification:");
        lines.Add($"- summary: {policy.Qualification.Summary}");
        lines.Add($"  proven layers: {(policy.Qualification.ProvenLayers.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.ProvenLayers))}");
        lines.Add($"  first response rules: {(policy.Qualification.FirstResponseRules.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.FirstResponseRules))}");
        lines.Add($"  remaining gaps: {(policy.Qualification.RemainingGaps.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.RemainingGaps))}");
        lines.Add($"  success criteria: {(policy.Qualification.SuccessCriteria.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.SuccessCriteria))}");
        lines.Add($"  stop conditions: {(policy.Qualification.StopConditions.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.StopConditions))}");

        if (policy.Notes.Length > 0)
        {
            lines.Add("Notes:");
            foreach (var note in policy.Notes)
            {
                lines.Add($"- {note}");
            }
        }

        return OperatorCommandResult.Success(lines.ToArray());
    }
}
