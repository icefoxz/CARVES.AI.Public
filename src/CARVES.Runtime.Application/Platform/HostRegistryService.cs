using Carves.Runtime.Domain.Platform;

namespace Carves.Runtime.Application.Platform;

public sealed class HostRegistryService
{
    private readonly IHostRegistryRepository repository;

    public HostRegistryService(IHostRegistryRepository repository)
    {
        this.repository = repository;
    }

    public HostRegistry GetRegistry()
    {
        return repository.Load();
    }

    public IReadOnlyList<HostInstance> List()
    {
        return repository.Load().Items
            .OrderBy(item => item.HostId, StringComparer.Ordinal)
            .ToArray();
    }

    public HostInstance Upsert(string hostId, string machineId, string endpoint, HostInstanceStatus status, DateTimeOffset? observedAt = null)
    {
        var when = observedAt ?? DateTimeOffset.UtcNow;
        var registry = repository.Load();
        var existing = registry.Items.FirstOrDefault(item => string.Equals(item.HostId, hostId, StringComparison.Ordinal));
        var host = existing is null
            ? new HostInstance
            {
                HostId = hostId,
                MachineId = machineId,
                Endpoint = endpoint,
                Status = status,
                RegisteredAt = when,
                LastSeen = when,
                UpdatedAt = when,
            }
            : new HostInstance
            {
                SchemaVersion = existing.SchemaVersion,
                HostId = existing.HostId,
                MachineId = string.IsNullOrWhiteSpace(machineId) ? existing.MachineId : machineId,
                Endpoint = endpoint,
                Status = status,
                RegisteredAt = existing.RegisteredAt,
                LastSeen = when,
                UpdatedAt = when,
            };

        var items = registry.Items
            .Where(item =>
                !string.Equals(item.HostId, host.HostId, StringComparison.Ordinal)
                && !ShouldPruneMachineSibling(item, host))
            .Append(host)
            .OrderBy(item => item.HostId, StringComparer.Ordinal)
            .ToArray();
        repository.Save(new HostRegistry
        {
            SchemaVersion = registry.SchemaVersion,
            Items = items,
        });
        return host;
    }

    private static bool ShouldPruneMachineSibling(HostInstance existing, HostInstance current)
    {
        return current.Status == HostInstanceStatus.Active
            && !string.IsNullOrWhiteSpace(current.MachineId)
            && string.Equals(existing.MachineId, current.MachineId, StringComparison.Ordinal)
            && existing.Status == HostInstanceStatus.Unknown;
    }
}
