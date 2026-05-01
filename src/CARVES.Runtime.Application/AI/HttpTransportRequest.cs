namespace Carves.Runtime.Application.AI;

public sealed class HttpTransportRequest
{
    public string Method { get; init; } = "POST";

    public string Url { get; init; } = string.Empty;

    public string? Body { get; init; }

    public string ContentType { get; init; } = "application/json";

    public int TimeoutSeconds { get; init; } = 30;

    public int MaxRetries { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
