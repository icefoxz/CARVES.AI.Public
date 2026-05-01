namespace Carves.Runtime.Application.Platform;

public interface IAIProvider
{
    string ProviderId { get; }

    Task<string> CompleteAsync(string request, CancellationToken cancellationToken);

    Task<string> InvokeToolsAsync(string request, CancellationToken cancellationToken);
}
