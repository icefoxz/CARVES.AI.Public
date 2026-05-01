using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class MemoryDriftOpportunityDetector : IOpportunityDetector
{
    private readonly MemoryAuditService memoryAuditService;

    public MemoryDriftOpportunityDetector(MemoryAuditService memoryAuditService)
    {
        this.memoryAuditService = memoryAuditService;
    }

    public string Name => "memory-audit";

    public IReadOnlyList<OpportunityObservation> Detect()
    {
        var driftPolicyService = new MemoryDriftSeverityPolicyService();
        var missingModulePolicy = driftPolicyService.GetEntry("missing_module_memory");
        return memoryAuditService.DetectMissingModuleMemory()
            .Take(5)
            .Select(module => new OpportunityObservation(
                OpportunitySource.MemoryDrift,
                $"memory:{module}",
                $"Audit runtime memory for {module}",
                $"CodeGraph knows module '{module}' but no module memory document was found.",
                "module memory is missing for an active codegraph module",
                missingModulePolicy.Severity,
                0.75,
                [$".ai/memory/modules/{module}.md"],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["module"] = module,
                    ["drift_behavior"] = missingModulePolicy.Behavior,
                }))
            .ToArray();
    }
}
