using Carves.Runtime.Application.Refactoring;
using Carves.Runtime.Domain.Planning;

namespace Carves.Runtime.Application.Planning;

public sealed class RefactoringOpportunityDetector : IOpportunityDetector
{
    private readonly IRefactoringService refactoringService;

    public RefactoringOpportunityDetector(IRefactoringService refactoringService)
    {
        this.refactoringService = refactoringService;
    }

    public string Name => "refactoring";

    public IReadOnlyList<OpportunityObservation> Detect()
    {
        var snapshot = refactoringService.DetectAndStore();
        return snapshot.Items
            .Where(item => item.Status == RefactoringBacklogStatus.Open)
            .Select(item => new OpportunityObservation(
                OpportunitySource.Refactoring,
                item.Fingerprint,
                $"Refactor {Path.GetFileNameWithoutExtension(item.Path)}",
                item.Reason,
                item.Reason,
                MapSeverity(item.Priority),
                string.Equals(item.Severity, "error", StringComparison.OrdinalIgnoreCase) ? 0.9 : 0.7,
                [item.Path],
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["backlog_item_id"] = item.ItemId,
                    ["refactoring_kind"] = item.Kind,
                    ["priority"] = item.Priority,
                }))
            .ToArray();
    }

    private static OpportunitySeverity MapSeverity(string priority)
    {
        return priority switch
        {
            "P0" or "P1" => OpportunitySeverity.High,
            "P2" => OpportunitySeverity.Medium,
            _ => OpportunitySeverity.Low,
        };
    }
}
