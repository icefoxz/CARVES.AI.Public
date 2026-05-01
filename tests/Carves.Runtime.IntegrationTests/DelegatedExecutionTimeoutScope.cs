namespace Carves.Runtime.IntegrationTests;

internal sealed class DelegatedExecutionTimeoutScope : IDisposable
{
    private const string WorkerRequestTimeoutCapEnvironmentVariable = "CARVES_WORKER_REQUEST_TIMEOUT_CAP_SECONDS";
    private const string AgentRequestTimeoutCapEnvironmentVariable = "CARVES_AGENT_REQUEST_TIMEOUT_CAP_SECONDS";

    private readonly string? originalWorkerRequestTimeoutCap;
    private readonly string? originalAgentRequestTimeoutCap;

    private DelegatedExecutionTimeoutScope(
        string? originalWorkerRequestTimeoutCap,
        string? originalAgentRequestTimeoutCap)
    {
        this.originalWorkerRequestTimeoutCap = originalWorkerRequestTimeoutCap;
        this.originalAgentRequestTimeoutCap = originalAgentRequestTimeoutCap;
    }

    public static DelegatedExecutionTimeoutScope Apply(
        int? workerRequestTimeoutCapSeconds = null,
        int? agentRequestTimeoutCapSeconds = null)
    {
        if (workerRequestTimeoutCapSeconds is null && agentRequestTimeoutCapSeconds is null)
        {
            throw new ArgumentException("At least one delegated execution timeout cap must be configured.");
        }

        if (workerRequestTimeoutCapSeconds is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(workerRequestTimeoutCapSeconds));
        }

        if (agentRequestTimeoutCapSeconds is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(agentRequestTimeoutCapSeconds));
        }

        var originalWorkerRequestTimeoutCap = Environment.GetEnvironmentVariable(WorkerRequestTimeoutCapEnvironmentVariable);
        var originalAgentRequestTimeoutCap = Environment.GetEnvironmentVariable(AgentRequestTimeoutCapEnvironmentVariable);

        if (workerRequestTimeoutCapSeconds is not null)
        {
            Environment.SetEnvironmentVariable(
                WorkerRequestTimeoutCapEnvironmentVariable,
                workerRequestTimeoutCapSeconds.Value.ToString());
        }

        if (agentRequestTimeoutCapSeconds is not null)
        {
            Environment.SetEnvironmentVariable(
                AgentRequestTimeoutCapEnvironmentVariable,
                agentRequestTimeoutCapSeconds.Value.ToString());
        }

        return new DelegatedExecutionTimeoutScope(
            originalWorkerRequestTimeoutCap,
            originalAgentRequestTimeoutCap);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(
            WorkerRequestTimeoutCapEnvironmentVariable,
            originalWorkerRequestTimeoutCap);
        Environment.SetEnvironmentVariable(
            AgentRequestTimeoutCapEnvironmentVariable,
            originalAgentRequestTimeoutCap);
    }
}
