using Carves.Runtime.Application.Configuration;
using System.Diagnostics;
using Carves.Runtime.Application.AI;
using Carves.Runtime.Application.Workers;
using Carves.Runtime.Domain.Execution;
using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class ProviderHealthMonitorService
{
    private readonly IProviderHealthRepository repository;
    private readonly ProviderRegistryService providerRegistryService;
    private readonly WorkerAdapterRegistry workerAdapterRegistry;
    private readonly WorkerOperationalPolicyService operationalPolicyService;

    public ProviderHealthMonitorService(
        IProviderHealthRepository repository,
        ProviderRegistryService providerRegistryService,
        WorkerAdapterRegistry workerAdapterRegistry,
        WorkerOperationalPolicyService operationalPolicyService)
    {
        this.repository = repository;
        this.providerRegistryService = providerRegistryService;
        this.workerAdapterRegistry = workerAdapterRegistry;
        this.operationalPolicyService = operationalPolicyService;
    }

    public ProviderHealthMonitorService(
        IProviderHealthRepository repository,
        ProviderRegistryService providerRegistryService,
        WorkerAdapterRegistry workerAdapterRegistry)
        : this(repository, providerRegistryService, workerAdapterRegistry, new WorkerOperationalPolicyService(WorkerOperationalPolicy.CreateDefault()))
    {
    }

    public ProviderHealthSnapshot Load()
    {
        return repository.Load();
    }

    public ProviderHealthSnapshot Refresh()
    {
        var degradedLatencyThresholdMs = Math.Max(0, operationalPolicyService.GetPolicy().Observability.ProviderDegradedLatencyMs);
        var previous = repository.Load().Entries.ToDictionary(item => item.BackendId, StringComparer.Ordinal);
        var records = providerRegistryService.ListWorkerBackends()
            .Select(backend =>
            {
                var adapter = workerAdapterRegistry.TryGetByBackendId(backend.BackendId);
                var stopwatch = Stopwatch.StartNew();
                var summary = adapter?.CheckHealth() ?? backend.Health;
                stopwatch.Stop();

                var latencyMs = summary.LatencyMs ?? stopwatch.ElapsedMilliseconds;
                var state = summary.State;
                var degradationReason = summary.DegradationReason;
                if (state == WorkerBackendHealthState.Healthy && latencyMs >= degradedLatencyThresholdMs)
                {
                    state = WorkerBackendHealthState.Degraded;
                    degradationReason = "high_latency";
                }

                previous.TryGetValue(backend.BackendId, out var existing);
                return new ProviderHealthRecord
                {
                    BackendId = backend.BackendId,
                    ProviderId = backend.ProviderId,
                    AdapterId = adapter?.AdapterId ?? backend.AdapterId,
                    State = state,
                    Summary = summary.Summary,
                    LatencyMs = latencyMs,
                    DegradationReason = degradationReason,
                    ConsecutiveFailureCount = state == WorkerBackendHealthState.Healthy ? 0 : (existing?.ConsecutiveFailureCount ?? 0) + 1,
                    LastFailureKind = existing?.LastFailureKind ?? WorkerFailureKind.None,
                    CheckedAt = DateTimeOffset.UtcNow,
                };
            })
            .OrderBy(item => item.BackendId, StringComparer.Ordinal)
            .ToArray();

        var snapshot = new ProviderHealthSnapshot
        {
            Entries = records,
        };
        repository.Save(snapshot);
        return snapshot;
    }

    public ProviderHealthRecord? GetHealth(string backendId)
    {
        return Load().Entries.FirstOrDefault(item => string.Equals(item.BackendId, backendId, StringComparison.Ordinal));
    }
}
