using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeGitNativeCodingLoop(RuntimeGitNativeCodingLoopSurface surface)
    {
        var policy = surface.Policy;
        var lines = new List<string>
        {
            "Runtime git-native coding loop",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {policy.PolicyVersion}",
            $"Summary: {policy.Summary}",
            "Extraction boundary:",
            $"- direct absorptions: {string.Join(" | ", policy.ExtractionBoundary.DirectAbsorptions)}",
            $"  translated into CARVES: {string.Join(" | ", policy.ExtractionBoundary.TranslatedIntoCarves)}",
            $"  rejected anchors: {string.Join(" | ", policy.ExtractionBoundary.RejectedAnchors)}",
            "Repo-map projection:",
            $"- summary: {policy.RepoMapProjection.Summary}",
            $"  source capabilities: {(policy.RepoMapProjection.SourceCapabilities.Length == 0 ? "(none)" : string.Join(" | ", policy.RepoMapProjection.SourceCapabilities))}",
            $"  CARVES translations: {(policy.RepoMapProjection.CarvesTranslations.Length == 0 ? "(none)" : string.Join(" | ", policy.RepoMapProjection.CarvesTranslations))}",
            $"  stronger truth dependencies: {(policy.RepoMapProjection.StrongerTruthDependencies.Length == 0 ? "(none)" : string.Join(" | ", policy.RepoMapProjection.StrongerTruthDependencies))}",
            $"  non-goals: {(policy.RepoMapProjection.NonGoals.Length == 0 ? "(none)" : string.Join(" | ", policy.RepoMapProjection.NonGoals))}",
            "Patch-first interaction:",
            $"- summary: {policy.PatchFirstInteraction.Summary}",
            $"  interaction steps: {(policy.PatchFirstInteraction.InteractionSteps.Length == 0 ? "(none)" : string.Join(" | ", policy.PatchFirstInteraction.InteractionSteps))}",
            $"  required gates: {(policy.PatchFirstInteraction.RequiredGates.Length == 0 ? "(none)" : string.Join(" | ", policy.PatchFirstInteraction.RequiredGates))}",
            $"  review expectations: {(policy.PatchFirstInteraction.ReviewExpectations.Length == 0 ? "(none)" : string.Join(" | ", policy.PatchFirstInteraction.ReviewExpectations))}",
            $"  rejected drift: {(policy.PatchFirstInteraction.RejectedDrift.Length == 0 ? "(none)" : string.Join(" | ", policy.PatchFirstInteraction.RejectedDrift))}",
            "Evidence loop:",
            $"- summary: {policy.EvidenceLoop.Summary}",
            $"  evidence kinds: {(policy.EvidenceLoop.EvidenceKinds.Length == 0 ? "(none)" : string.Join(" | ", policy.EvidenceLoop.EvidenceKinds))}",
            $"  supporting uses: {(policy.EvidenceLoop.SupportingUses.Length == 0 ? "(none)" : string.Join(" | ", policy.EvidenceLoop.SupportingUses))}",
            $"  non-truth uses: {(policy.EvidenceLoop.NonTruthUses.Length == 0 ? "(none)" : string.Join(" | ", policy.EvidenceLoop.NonTruthUses))}",
            $"  notes: {(policy.EvidenceLoop.Notes.Length == 0 ? "(none)" : string.Join(" | ", policy.EvidenceLoop.Notes))}",
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
        lines.Add($"  success criteria: {(policy.Qualification.SuccessCriteria.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.SuccessCriteria))}");
        lines.Add($"  rejected directions: {(policy.Qualification.RejectedDirections.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.RejectedDirections))}");
        lines.Add($"  deferred directions: {(policy.Qualification.DeferredDirections.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.DeferredDirections))}");
        lines.Add($"  stop conditions: {(policy.Qualification.StopConditions.Length == 0 ? "(none)" : string.Join(" | ", policy.Qualification.StopConditions))}");

        lines.Add("Readiness map:");
        foreach (var entry in policy.ReadinessMap.OrderBy(item => item.SemanticId, StringComparer.Ordinal))
        {
            lines.Add($"- {entry.SemanticId} => {entry.Readiness}");
            lines.Add($"  summary: {entry.Summary}");
            lines.Add($"  governed follow-up: {(entry.GovernedFollowUp.Length == 0 ? "(none)" : string.Join(" | ", entry.GovernedFollowUp))}");
        }

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
