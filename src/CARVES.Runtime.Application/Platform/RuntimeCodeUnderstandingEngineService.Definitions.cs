using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed partial class RuntimeCodeUnderstandingEngineService
{
    private static bool NeedsRefresh(CodeUnderstandingEnginePolicy policy)
    {
        return policy.PolicyVersion < 2
               || policy.ConcernFamilies.Length < 3
               || policy.PrecisionTiers.Length < 3
               || policy.SemanticPathPilots.Length < 1
               || policy.TruthRoots.Length < 3
               || policy.BoundaryRules.Length < 4
               || policy.GovernedReadPaths.Length < 4
               || string.IsNullOrWhiteSpace(policy.Qualification.Summary)
               || !policy.PrecisionTiers.Any(item => string.Equals(item.TierId, "search_grade", StringComparison.Ordinal))
               || !policy.PrecisionTiers.Any(item => string.Equals(item.TierId, "impact_grade", StringComparison.Ordinal))
               || !policy.PrecisionTiers.Any(item => string.Equals(item.TierId, "governance_grade", StringComparison.Ordinal))
               || !policy.SemanticPathPilots.Any(item => string.Equals(item.PilotId, "bounded_csharp_semantic_path", StringComparison.Ordinal))
               || !policy.ExtractionBoundary.RejectedAnchors.Contains(
                   "the local D:/Projects/CARVES/scip-master checkout is the SCIP optimization solver, not the code-index protocol target",
                   StringComparer.Ordinal);
    }

    private static RuntimeKernelTruthRootDescriptor Root(
        string rootId,
        string classification,
        string summary,
        string[] pathRefs,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelTruthRootDescriptor
        {
            RootId = rootId,
            Classification = classification,
            Summary = summary,
            PathRefs = pathRefs,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelBoundaryRule Rule(
        string ruleId,
        string summary,
        string[] allowedRefs,
        string[] forbiddenRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelBoundaryRule
        {
            RuleId = ruleId,
            Summary = summary,
            AllowedRefs = allowedRefs,
            ForbiddenRefs = forbiddenRefs,
            Notes = notes ?? [],
        };
    }

    private static RuntimeKernelGovernedReadPath ReadPath(
        string pathId,
        string entryPoint,
        string summary,
        string[] truthRefs,
        string[]? notes = null)
    {
        return new RuntimeKernelGovernedReadPath
        {
            PathId = pathId,
            EntryPoint = entryPoint,
            Summary = summary,
            TruthRefs = truthRefs,
            Notes = notes ?? [],
        };
    }
}
