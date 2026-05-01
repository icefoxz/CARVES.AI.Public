using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.ControlPlane;

internal static partial class OperatorSurfaceFormatter
{
    public static OperatorCommandResult RuntimeRepoAuthoredGateLoop(RuntimeRepoAuthoredGateLoopSurface surface)
    {
        var policy = surface.Policy;
        var lines = new List<string>
        {
            "Runtime repo-authored gate loop",
            $"Surface id: {surface.SurfaceId}",
            $"Generated at: {surface.GeneratedAt:O}",
            $"Policy path: {surface.PolicyPath}",
            $"Policy version: {policy.PolicyVersion}",
            $"Summary: {policy.Summary}",
            "Extraction boundary:",
            $"- direct absorptions: {string.Join(" | ", policy.ExtractionBoundary.DirectAbsorptions)}",
            $"  translated into CARVES: {string.Join(" | ", policy.ExtractionBoundary.TranslatedIntoCarves)}",
            $"  rejected anchors: {string.Join(" | ", policy.ExtractionBoundary.RejectedAnchors)}",
            "Definition kernel:",
            $"- summary: {policy.DefinitionKernel.Summary}",
            $"  machine truth path: {policy.DefinitionKernel.MachineTruthPath}",
            $"  projection paths: {(policy.DefinitionKernel.ProjectionPaths.Length == 0 ? "(none)" : string.Join(" | ", policy.DefinitionKernel.ProjectionPaths))}",
            $"  discovery modes: {(policy.DefinitionKernel.DiscoveryModes.Length == 0 ? "(none)" : string.Join(" | ", policy.DefinitionKernel.DiscoveryModes))}",
            $"  unit families: {(policy.DefinitionKernel.UnitFamilies.Length == 0 ? "(none)" : string.Join(" | ", policy.DefinitionKernel.UnitFamilies))}",
            $"  non-goals: {(policy.DefinitionKernel.NonGoals.Length == 0 ? "(none)" : string.Join(" | ", policy.DefinitionKernel.NonGoals))}",
            "Gate execution:",
            $"- summary: {policy.GateExecution.Summary}",
            $"  unit kinds: {(policy.GateExecution.ExecutionUnitKinds.Length == 0 ? "(none)" : string.Join(" | ", policy.GateExecution.ExecutionUnitKinds))}",
            $"  result states: {(policy.GateExecution.ResultStates.Length == 0 ? "(none)" : string.Join(" | ", policy.GateExecution.ResultStates))}",
            $"  artifact families: {(policy.GateExecution.ArtifactFamilies.Length == 0 ? "(none)" : string.Join(" | ", policy.GateExecution.ArtifactFamilies))}",
            $"  review boundaries: {(policy.GateExecution.ReviewBoundaries.Length == 0 ? "(none)" : string.Join(" | ", policy.GateExecution.ReviewBoundaries))}",
            $"  non-goals: {(policy.GateExecution.NonGoals.Length == 0 ? "(none)" : string.Join(" | ", policy.GateExecution.NonGoals))}",
            "Workflow projections:",
        };

        foreach (var projection in policy.WorkflowProjections.OrderBy(item => item.ProjectionId, StringComparer.Ordinal))
        {
            lines.Add($"- {projection.ProjectionId} [{projection.Context}]");
            lines.Add($"  summary: {projection.Summary}");
            lines.Add($"  triggers: {(projection.TriggerSurfaces.Length == 0 ? "(none)" : string.Join(" | ", projection.TriggerSurfaces))}");
            lines.Add($"  results: {(projection.ResultSurfaces.Length == 0 ? "(none)" : string.Join(" | ", projection.ResultSurfaces))}");
            if (projection.Notes.Length > 0)
            {
                lines.Add($"  notes: {string.Join(" | ", projection.Notes)}");
            }
        }

        lines.Add("Concern families:");
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
