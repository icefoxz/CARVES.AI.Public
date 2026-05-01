using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public interface IRoutingValidationRepository
{
    RoutingValidationCatalog? LoadCatalog();

    void SaveCatalog(RoutingValidationCatalog catalog);

    RoutingValidationTrace? LoadTrace(string traceId);

    IReadOnlyList<RoutingValidationTrace> LoadTraces(string? runId = null);

    void SaveTrace(RoutingValidationTrace trace);

    RoutingValidationSummary? LoadSummary(string runId);

    IReadOnlyList<RoutingValidationSummary> LoadSummaries(int? limit = null);

    void SaveSummary(RoutingValidationSummary summary);

    RoutingValidationSummary? LoadLatestSummary();

    void SaveLatestSummary(RoutingValidationSummary summary);
}
