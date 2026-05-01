using Carves.Runtime.Domain.Failures;

namespace Carves.Runtime.Application.Failures;

public sealed class FailureContextService
{
    private readonly IFailureReportRepository repository;

    public FailureContextService(IFailureReportRepository repository)
    {
        this.repository = repository;
    }

    public IReadOnlyList<FailureReport> LoadAll()
    {
        return repository.LoadAll();
    }

    public IReadOnlyList<FailureReport> GetTaskFailures(string taskId, int limit = int.MaxValue)
    {
        return repository.LoadAll()
            .Where(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
            .OrderByDescending(item => item.Timestamp)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();
    }

    public IReadOnlyDictionary<string, int> GetRepeatedFileFailures(string taskId)
    {
        return repository.LoadAll()
            .Where(item => string.Equals(item.TaskId, taskId, StringComparison.Ordinal))
            .SelectMany(item => item.InputSummary.FilesInvolved)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .GroupBy(path => path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    public double GetProviderFailureRate(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return 0;
        }

        var providerFailures = repository.LoadAll()
            .Where(item => !string.IsNullOrWhiteSpace(item.Provider))
            .ToArray();
        if (providerFailures.Length == 0)
        {
            return 0;
        }

        var matching = providerFailures.Count(item => string.Equals(item.Provider, providerId, StringComparison.OrdinalIgnoreCase));
        return matching / (double)providerFailures.Length;
    }
}
